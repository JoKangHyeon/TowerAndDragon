using System.Collections.Generic;
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

    [Tooltip("Hidden/Visible/Conquered 경계에서 안개가 부드럽게 옅어지는 폭(셀 단위) - 0이면 경계가 한 번에 바뀐다.")]
    [SerializeField, Range(0, 4)]
    private int _gradientBandWidth = 2;

    // Conquered < Visible < Hidden 순으로 안개가 짙어지는 순서값 - 그라데이션 시 "더 옅은 이웃"을 찾는 기준.
    private const int CONQUERED_FOG_ORDER = 0;
    private const int VISIBLE_FOG_ORDER = 1;
    private const int HIDDEN_FOG_ORDER = 2;

    private GridMap _gridMap;
    private Tilemap _terrainTilemap;

    // 안개 밝기에 곱할 셀별 색조 - 점령 모드가 "아직 못 가는 지역"을 표시하는 데 쓴다.
    // 지형 스프라이트 자체를 물들이므로, 셀마다 평면 조각을 얹을 때 생기는 고저차 틈이 존재하지 않는다.
    private readonly Dictionary<Vector3Int, Color> _overlayTints = new();
    private readonly HashSet<Vector3Int> _overlayRepaintBuffer = new();

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

    // 점령 등으로 셀 상태가 바뀔 때마다 GridMap이 즉시 호출 - 바뀐 셀 주변 그라데이션 폭만큼도
    // 다시 칠한다(이웃 셀의 그라데이션이 바뀐 셀의 상태를 참조하므로 함께 갱신해야 함).
    private void HandleCellChanged(GridCell cell) => RepaintAround(cell.Coord);

    private void RepaintAround(Vector3Int center)
    {
        for (int dx = -_gradientBandWidth; dx <= _gradientBandWidth; dx++)
        {
            for (int dy = -_gradientBandWidth; dy <= _gradientBandWidth; dy++)
            {
                Vector3Int coord = center + new Vector3Int(dx, dy, 0);
                if (_terrainTilemap.HasTile(coord))
                    PaintCellAt(coord);
            }
        }
    }

    private void PaintCell(GridCell cell) => PaintCellAt(cell.Coord);

    private void PaintCellAt(Vector3Int coord)
    {
        _terrainTilemap.SetTileFlags(coord, TileFlags.None);
        _terrainTilemap.SetColor(coord, GetGradientTintColor(coord));
    }

    // 지형 타일 전용 - 경계 근처 셀은 가장 가까운 "더 옅은" 이웃 셀의 색으로 부드럽게 섞는다.
    private Color GetGradientTintColor(Vector3Int coord)
    {
        ChunkState state = _gridMap.GetCellState(coord);
        int order = GetFogOrder(state);
        float alpha = GetOrderAlpha(order);

        if (_gradientBandWidth > 0 && order > CONQUERED_FOG_ORDER)
        {
            int bestDistance = int.MaxValue;
            int bestOrder = order;

            for (int dx = -_gradientBandWidth; dx <= _gradientBandWidth; dx++)
            {
                for (int dy = -_gradientBandWidth; dy <= _gradientBandWidth; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (distance >= bestDistance)
                        continue;

                    Vector3Int neighborCoord = coord + new Vector3Int(dx, dy, 0);
                    int neighborOrder = GetFogOrder(_gridMap.GetCellState(neighborCoord));

                    if (neighborOrder < order)
                    {
                        bestDistance = distance;
                        bestOrder = neighborOrder;
                    }
                }
            }

            if (bestDistance <= _gradientBandWidth)
            {
                float blendFactor = 1f - (float)(bestDistance - 1) / _gradientBandWidth;
                alpha = Mathf.Lerp(alpha, GetOrderAlpha(bestOrder), blendFactor);
            }
        }

        float brightness = 1f - alpha;
        var fogColor = new Color(brightness, brightness, brightness, 1f);

        // 안개는 밝기(그레이스케일)만 쓰므로 색조는 비어 있다 - 등록된 색조를 곱하면 안개가 전달하는
        // 밝기는 그대로 보존되고 색만 바뀐다.
        return _overlayTints.TryGetValue(coord, out Color overlayTint) ? fogColor * overlayTint : fogColor;
    }

    // 지형 타일 색상은 이 렌더러가 단독으로 쓴다(프로젝트 내 SetColor 호출부가 여기뿐).
    // 다른 시스템이 지형에 색을 입히려면 따로 SetColor를 부르지 말고 여기에 색조를 등록해야 한다 -
    // 안개가 셀 상태 변화로 재도색할 때 그 색을 덮어버리기 때문이다.
    // 색조는 밝기에 곱해지므로 알파는 1로 두고 RGB로만 표현한다.
    // 셀마다 색이 다를 수 있어(점령 가능은 노랑, 아직 못 가는 곳은 붉은색) 좌표별 색을 받는다.
    public void ApplyOverlayTints(IReadOnlyDictionary<Vector3Int, Color> tintsByCell)
    {
        // 색조가 사라진 셀도 안개 색으로 되돌려야 하므로, 이전 대상과 새 대상을 합쳐 다시 칠한다.
        _overlayRepaintBuffer.Clear();
        _overlayRepaintBuffer.UnionWith(_overlayTints.Keys);

        _overlayTints.Clear();
        foreach (KeyValuePair<Vector3Int, Color> pair in tintsByCell)
        {
            _overlayTints[pair.Key] = pair.Value;
            _overlayRepaintBuffer.Add(pair.Key);
        }

        RepaintCells(_overlayRepaintBuffer);
    }

    public void ClearOverlayTint()
    {
        if (_overlayTints.Count == 0)
            return;

        _overlayRepaintBuffer.Clear();
        _overlayRepaintBuffer.UnionWith(_overlayTints.Keys);
        _overlayTints.Clear();

        RepaintCells(_overlayRepaintBuffer);
    }

    private void RepaintCells(HashSet<Vector3Int> cellCoords)
    {
        if (_terrainTilemap == null)
            return;

        foreach (Vector3Int coord in cellCoords)
        {
            if (_terrainTilemap.HasTile(coord))
                PaintCellAt(coord);
        }
    }

    private int GetFogOrder(ChunkState state) => state switch
    {
        ChunkState.Conquered => CONQUERED_FOG_ORDER,
        ChunkState.Visible => VISIBLE_FOG_ORDER,
        _ => HIDDEN_FOG_ORDER,
    };

    private float GetOrderAlpha(int order) => order switch
    {
        CONQUERED_FOG_ORDER => 0f,
        VISIBLE_FOG_ORDER => _visibleFogAlpha,
        _ => _hiddenFogAlpha,
    };

    // 몬스터 등 지형 위 오브젝트를 같은 방식·같은 밝기로 틴트하는 FogTintReceiver가 재사용한다.
    // (오브젝트는 그라데이션 없이 자기 셀의 상태값 그대로 적용)
    public Color GetTintColor(ChunkState state)
    {
        float alpha = GetOrderAlpha(GetFogOrder(state));
        float brightness = 1f - alpha;
        return new Color(brightness, brightness, brightness, 1f);
    }
}
