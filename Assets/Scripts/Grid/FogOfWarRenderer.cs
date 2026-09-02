using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
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

    // [임시 계측] 밤→낮 전환 프리즈 조사용. CycleManager.cs의 TND. 명명 규칙을 그대로 따른다.
    private const string REPAINT_MARKER_NAME = "TND.Fog.Repaint";
    private static readonly ProfilerMarker REPAINT_MARKER = new(REPAINT_MARKER_NAME);

    private GridMap _gridMap;
    private Tilemap _terrainTilemap;

    // 안개 밝기에 곱할 셀별 색조 - 점령 모드가 "아직 못 가는 지역"을 표시하는 데 쓴다.
    // 지형 스프라이트 자체를 물들이므로, 셀마다 평면 조각을 얹을 때 생기는 고저차 틈이 존재하지 않는다.
    private readonly Dictionary<Vector3Int, Color> _overlayTints = new();
    private readonly HashSet<Vector3Int> _overlayRepaintBuffer = new();

    // 그라데이션 이웃 오프셋(dx, dy, 체비셰프 거리) - _gradientBandWidth로 정해지는 모양은 게임 내내
    // 바뀌지 않으므로, 셀마다 Mathf.Abs/Max를 다시 계산하지 않고 한 번만 만들어 재사용한다.
    // dx 바깥/dy 안쪽 순회 순서를 그대로 보존해야 한다 - GetGradientTintColor의 동률 처리(같은 거리에서
    // 나중에 검사한 이웃이 이긴다)가 이 순서에 의존한다.
    private (int dx, int dy, int distance)[] _gradientOffsets;

    // 증분 갱신 전용 버퍼. 오버레이 갱신(_overlayRepaintBuffer)과는 반드시 분리한다 - 같은 프레임에
    // 섞이면 서로의 대상 집합을 지운다. 둘 다 최종적으로 GetGradientTintColor가 "현재 상태 + 현재
    // 오버레이"를 다시 계산해 수렴하므로 처리 순서가 뒤바뀌어도 최종 색은 항상 같다.
    private readonly HashSet<Vector3Int> _dirtyCells = new();
    private readonly HashSet<Vector3Int> _repaintTargets = new();
    private bool _isRepaintQueued;

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
        UnlockTileColors();

        HashSet<Vector3Int> scanNeeded = BuildScanNeededCells();

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                _terrainTilemap.SetColor(cell.Coord, GetGradientTintColor(cell.Coord, scanNeeded));
            }
        }
    }

    // PaintAllCells 전용 사전 필터 - 시작 시점엔 정복·가시 셀(밝은 쪽)이 극소수이고 나머지 수만 셀은
    // 전부 Hidden이다. 그 밝은 셀들만 밴드 폭만큼 부풀려("dilate") 닿는 좌표만 모으면, 그 밖의 셀은
    // 밴드 안이 전부 자기와 같거나 더 어두운 상태뿐이라는 뜻이 되어 원래 25칸 탐색도 항상
    // "이웃 없음"으로 끝난다 - 그 결론을 미리 알고 있으니 탐색 자체를 건너뛴다.
    // 과다 포함(밝은 셀 옆의 밝은 셀 등)은 안전하다 - 그 좌표는 원래 알고리즘대로 다시 스캔될 뿐이다.
    private HashSet<Vector3Int> BuildScanNeededCells()
    {
        var scanNeeded = new HashSet<Vector3Int>();
        if (_gradientBandWidth <= 0)
            return scanNeeded;

        EnsureGradientOffsets();

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                // HIDDEN_FOG_ORDER보다 옅은 셀(Conquered·Visible)만 누군가에게 빛을 줄 수 있다.
                if (GetFogOrder(cell.CurrentState) >= HIDDEN_FOG_ORDER)
                    continue;

                for (int i = 0; i < _gradientOffsets.Length; i++)
                {
                    (int dx, int dy, _) = _gradientOffsets[i];
                    scanNeeded.Add(cell.Coord + new Vector3Int(dx, dy, 0));
                }
            }
        }

        return scanNeeded;
    }

    // 지형 타일 에셋은 전부 TileFlags.LockColor가 걸려 있어(Imported 타일팩 검수 결과 1260개 중
    // 1259개), 좌표별로 플래그를 풀지 않으면 SetColor가 경고 없이 무시된다. 플래그는 좌표별 상태로
    // 남고 런타임에 이 타일맵에 SetTile을 하는 코드가 없으므로, 매 재도색마다 부르지 않고 시작 시
    // 한 번만 풀어 둔다. 청크 셀만이 아니라 cellBounds 전체를 도는 이유는, 증분 갱신 경로(9x9 창,
    // ApplyOverlayTints)가 청크 밖 좌표도 HasTile을 통과하면 칠하기 때문이다.
    private void UnlockTileColors()
    {
        foreach (Vector3Int coord in _terrainTilemap.cellBounds.allPositionsWithin)
        {
            if (_terrainTilemap.HasTile(coord))
                _terrainTilemap.SetTileFlags(coord, TileFlags.None);
        }
    }

    // 점령 등으로 셀 상태가 바뀔 때마다 GridMap이 즉시 호출된다 - 바뀐 셀 주변 그라데이션 폭만큼도
    // 다시 칠해야 하지만(이웃 셀의 그라데이션이 바뀐 셀의 상태를 참조), 청크 하나가 열리면 이 이벤트가
    // 청크 크기만큼(많게는 수백 회) 연달아 온다. 셀마다 즉시 9x9 창을 다시 칠하면 인접한 변경 셀끼리
    // 창이 크게 겹쳐 같은 좌표를 수십 번 다시 칠하게 되므로, 좌표만 모아 두고 프레임당 한 번만 처리한다.
    private void HandleCellChanged(GridCell cell)
    {
        _dirtyCells.Add(cell.Coord);
        QueueRepaint();
    }

    private void QueueRepaint()
    {
        if (_isRepaintQueued)
            return;

        _isRepaintQueued = true;
        RepaintQueuedAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // Defines.FOG_REPAINT_COALESCE_TIMING(렌더링 직전)에서 처리한다 - 다음 프레임으로 미루면
    // "점령 직후 한 프레임 옛 안개가 보인다"는 시각 지연이 생기지만, 같은 프레임 렌더링 전에
    // 처리하면 합치기 이득은 그대로 유지하면서 지연은 없다.
    private async UniTaskVoid RepaintQueuedAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(Defines.FOG_REPAINT_COALESCE_TIMING, cancellationToken);

        _isRepaintQueued = false;
        FlushDirtyCells();
    }

    // 이번 프레임에 바뀐 셀들의 9x9 창을 합집합으로 모아 좌표당 딱 한 번씩만 칠한다.
    private void FlushDirtyCells()
    {
        if (_dirtyCells.Count == 0)
            return;

        using (REPAINT_MARKER.Auto())
        {
            EnsureGradientOffsets();
            _repaintTargets.Clear();

            foreach (Vector3Int coord in _dirtyCells)
            {
                // _gradientOffsets에는 (0,0)이 없으므로 변경된 셀 자신은 별도로 더한다.
                _repaintTargets.Add(coord);

                for (int i = 0; i < _gradientOffsets.Length; i++)
                {
                    (int dx, int dy, _) = _gradientOffsets[i];
                    _repaintTargets.Add(coord + new Vector3Int(dx, dy, 0));
                }
            }

            _dirtyCells.Clear();
            RepaintCells(_repaintTargets);
        }
    }

    // 지형 타일 색상 전용 - PaintAllCells의 UnlockTileColors()가 시작 시 이미 좌표별 LockColor를
    // 풀어 뒀으므로, 여기서 매 셀 SetTileFlags를 다시 부르지 않아도 SetColor가 바로 먹는다.
    private void PaintCellAt(Vector3Int coord)
    {
        _terrainTilemap.SetColor(coord, GetGradientTintColor(coord, null));
    }

    // scanNeeded가 null이면(증분 갱신 경로 - 셀 몇 개뿐이라 사전 필터링이 필요 없다) 항상 이웃을
    // 탐색한다. scanNeeded가 있는데 coord가 그 안에 없으면(PaintAllCells 경로) BuildScanNeededCells가
    // 이미 "밴드 안에 더 옅은 이웃이 있을 수 없다"고 확인한 것이므로 25칸 탐색 없이 곧장 평평한 알파를 쓴다.
    private Color GetGradientTintColor(Vector3Int coord, HashSet<Vector3Int> scanNeeded)
    {
        ChunkState state = _gridMap.GetCellState(coord);
        int order = GetFogOrder(state);
        float alpha = GetOrderAlpha(order);

        bool mayHaveLighterNeighbor = scanNeeded == null || scanNeeded.Contains(coord);

        if (mayHaveLighterNeighbor && _gradientBandWidth > 0 && order > CONQUERED_FOG_ORDER)
        {
            EnsureGradientOffsets();

            int bestDistance = int.MaxValue;
            int bestOrder = order;

            for (int i = 0; i < _gradientOffsets.Length; i++)
            {
                (int dx, int dy, int distance) = _gradientOffsets[i];
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

    private void EnsureGradientOffsets()
    {
        if (_gradientOffsets != null)
            return;

        var offsets = new List<(int, int, int)>();
        for (int dx = -_gradientBandWidth; dx <= _gradientBandWidth; dx++)
        {
            for (int dy = -_gradientBandWidth; dy <= _gradientBandWidth; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                offsets.Add((dx, dy, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))));
            }
        }

        _gradientOffsets = offsets.ToArray();
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
