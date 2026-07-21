using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

// 전장의 안개 렌더러 - GridCell.CurrentState(ChunkState)를 그대로 시각화한다.
// Hidden = 짙은 반투명(지형 형태만 어렴풋이 비침), Visible = 옅은 반투명(부분 노출·미점령), Conquered = 타일 제거(안개 없음).
// 지형 타일맵과 같은 Grid 아래 자식 타일맵에 그려 셀 좌표계를 그대로 공유하고,
// 최상위 정렬 레이어("Fog")에 배치해 아이소메트릭 특성상 안개 뒤로 물러나는 오브젝트까지 함께 가린다.
// 안개 타일은 지형의 고저차(SetTransformMatrix 높이 오프셋)를 따라가지 않는다 - 정렬 레이어가
// 이미 Z 위치와 무관하게 항상 위에 그려지므로 굳이 띄울 필요가 없고, 오히려 인접 셀끼리 다른
// 높이로 띄우면 그 사이에 빈틈/겹침이 생긴다. 대신 Conquered와 맞닿은 아주 높은 지형(화산 등)의
// 꼭대기가 안개선 위로 살짝 비칠 수 있음 - 눈에 띄면 몬스터 poke-through와 함께 후속으로 재검토.
[RequireComponent(typeof(GridMap))]
public class FogOfWarRenderer : MonoBehaviour
{
    [Tooltip("안개를 그릴 타일맵. 지형 타일맵과 같은 Grid 아래, 셀 좌표계가 동일해야 한다.")]
    [SerializeField]
    private Tilemap _fogTilemap;

    [Tooltip("안개 셀에 채울 불투명 타일(솔리드 아이소메트릭 다이아몬드).")]
    [SerializeField]
    private TileBase _fogTile;

    [SerializeField]
    private Color _fogColor = Color.black;

    [Tooltip("Hidden(시야 밖) 상태일 때 안개의 불투명도 - 낮을수록 지형 형태가 더 비쳐 보인다.")]
    [SerializeField, Range(0f, 1f)]
    private float _hiddenFogAlpha = 0.5f;

    [Tooltip("Visible(시야 안·미점령) 상태일 때 안개의 불투명도 - 낮을수록 더 흐릿하게 비쳐 보인다.")]
    [SerializeField, Range(0f, 1f)]
    private float _visibleFogAlpha = 0.15f;

    private GridMap _gridMap;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
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
        _gridMap.OnCellChanged += HandleCellChanged;
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged -= HandleCellChanged;
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
        if (cell.CurrentState == ChunkState.Conquered)
        {
            _fogTilemap.SetTile(cell.Coord, null);
            return;
        }

        float alpha = cell.CurrentState == ChunkState.Visible ? _visibleFogAlpha : _hiddenFogAlpha;

        _fogTilemap.SetTile(cell.Coord, _fogTile);
        _fogTilemap.SetTileFlags(cell.Coord, TileFlags.None);
        _fogTilemap.SetColor(cell.Coord, WithAlpha(_fogColor, alpha));
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
