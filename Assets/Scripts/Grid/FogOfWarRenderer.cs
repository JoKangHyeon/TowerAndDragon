using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

// 전장의 안개 렌더러 - GridCell.CurrentState(ChunkState)에 따라 지형 타일 자체를 어둡게 틴트한다.
// Hidden = 짙게 어둡게(지형 형태만 어렴풋이 비침), Visible = 옅게 어둡게(부분 노출·미점령), Conquered = 원래 색(안개 없음).
//
// 별도 오버레이 타일맵 + 스커트로 구현했던 이전 버전은 절벽 등 고저차 경계에서 반투명 조각끼리
// 화면상 겹치거나(과하게 어두워짐) 어긋나서(밝은 틈) 계속 실패했다 - 지형은 이미 셀마다 정확한
// 높이·절벽면이 그려진 스프라이트라, 그 색상 자체를 바꾸면 별도 지오메트리가 없으니 이 문제가
// 구조적으로 존재하지 않는다.
//
// 건물·자원노드·몬스터 등 지형 위 오브젝트는 이 렌더러가 가리지 않는다 - 각 오브젝트가 자기가 선
// 셀의 ChunkState에 맞춰 스스로 같은 방식으로 틴트하는 별도 컴포넌트가 필요하다(PropVisibilityController류).
[RequireComponent(typeof(GridMap))]
public class FogOfWarRenderer : MonoBehaviour
{
    [Tooltip("Hidden(시야 밖) 상태일 때 지형 어둡기 - 높을수록 더 어둡게(형태만 어렴풋이) 보인다.")]
    [SerializeField, Range(0f, 1f)]
    private float _hiddenFogAlpha = 0.85f;

    [Tooltip("Visible(시야 안·미점령) 상태일 때 지형 어둡기 - 높을수록 더 어둡게 보인다.")]
    [SerializeField, Range(0f, 1f)]
    private float _visibleFogAlpha = 0.45f;

    private GridMap _gridMap;
    private Tilemap _terrainTilemap;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _terrainTilemap = _gridMap.TerrainTilemap;
    }

    // GridMap.Awake()가 셀/청크 생성을 끝내고, Castle.SetUpInitialTerritory() 등 다른 스크립트의
    // Start()가 초기 시야를 세팅한 뒤에 칠해야 한다. Start() 호출 순서는 보장되지 않으므로
    // (ConqueredChunkBorderRenderer와 동일한 이유) 한 프레임 뒤로 미뤄 초기 도색한다.
    private void Start()
    {
        PaintAllCellsNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PaintAllCellsNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        PaintAllCells();
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    private void PaintAllCells()
    {
        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                PaintCell(cell);
            }
        }
    }

    // 점령 등으로 셀 상태가 바뀔 때마다 GridMap이 즉시 호출 - 해당 셀 하나만 다시 칠한다.
    private void HandleCellChanged(GridCell cell) => PaintCell(cell);

    private void PaintCell(GridCell cell)
    {
        _terrainTilemap.SetTileFlags(cell.Coord, TileFlags.None);
        _terrainTilemap.SetColor(cell.Coord, GetTintColor(cell.CurrentState));
    }

    // 몬스터 등 지형 위 오브젝트를 같은 방식·같은 밝기로 틴트하는 FogTintReceiver가 재사용한다.
    public Color GetTintColor(ChunkState state)
    {
        float alpha = state switch
        {
            ChunkState.Conquered => 0f,
            ChunkState.Visible => _visibleFogAlpha,
            _ => _hiddenFogAlpha,
        };

        float brightness = 1f - alpha;
        return new Color(brightness, brightness, brightness, 1f);
    }
}
