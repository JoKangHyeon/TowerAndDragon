using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 인접한 Conquered 청크들을 하나의 영역으로 보고 바깥 경계에만 녹색 테두리를, 원정 중인(점령
// 진행 중) 청크들에는 같은 방식으로 회색 테두리를 그린다 (청크끼리 맞닿은 내부 경계선은 그리지 않음).
// 셀 단위로 이웃 셀의 소속 여부를 검사해 바깥 경계 변(edge)을 모으고, 변끼리 공유하는
// 꼭짓점을 이어붙여 폐곡선(loop)을 구성한 뒤 LineRenderer로 그린다.
[RequireComponent(typeof(GridMap))]
public class ConqueredChunkBorderRenderer : MonoBehaviour
{
    private const float CORNER_MIDPOINT_FACTOR = 0.5f;

    // 점령지 경계가 항상 원정 중 경계보다 위에 그려지도록 고정 - 두 선이 맞닿는 경계에서
    // 어느 쪽이 위로 오는지가 렌더러 순서에 따라 불안정하게 결정되는 것을 방지한다.
    // 두 값 모두 같은 정렬 레이어(Default)에 있는 지형 타일맵보다 커야 한다
    // (Ground 타일맵 order 0 · 폴리지 스프라이트 order 0 · Structures 타일맵 order 1).
    // 지형과 order가 같으면 z가 전부 0이라 정렬 키가 동률이 되고, Ground 타일맵이 Individual
    // 모드라 타일마다 위아래가 제각각 결정되어 선이 지형에 파묻힌 것처럼 일부만 보인다.
    private const int CONQUERED_BORDER_SORTING_ORDER = 3;
    private const int IN_PROGRESS_BORDER_SORTING_ORDER = 2;

    [SerializeField]
    private LineRenderer _borderLineRendererPrefab;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private ConquestManager _conquestManager;

    [SerializeField]
    private Color _borderColor = Color.green;

    [SerializeField]
    private Color _inProgressBorderColor = Color.gray;

    [SerializeField]
    private float _lineWidth = 0.05f;

    private GridMap _gridMap;
    private ComponentPool<LineRenderer> _borderPool;
    private ComponentPool<LineRenderer> _inProgressBorderPool;
    private HashSet<Vector2Int> _lastInProgressChunkCoords = new();

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _borderPool = new ComponentPool<LineRenderer>(_borderLineRendererPrefab, transform);
        _inProgressBorderPool = new ComponentPool<LineRenderer>(_borderLineRendererPrefab, transform);
        _gridMap.OnChunkStateChanged.AddListener(RefreshBorders);

        // ConquestManager는 Grid.prefab을 쓰는 씬(다른 팀원 테스트 씬 등)에 항상 있는 게 아니므로,
        // 없는 씬에서는 회색(원정 중) 테두리 기능만 조용히 비활성화한다.
        
        _conquestManager.OnExpeditionsChanged.AddListener(RefreshInProgressBorders);
        RefreshInProgressBorders();
        
    }

    // Castle.SetUpInitialTerritory()도 Start()에서 성 주변 청크를 Conquered로 세팅하는데,
    // 서로 다른 스크립트의 Start() 호출 순서는 보장되지 않는다(성보다 먼저 실행되면 최초 점령 청크를
    // 놓친 채로 갱신되어 게임 시작 시 테두리가 보이지 않음). 한 프레임 뒤로 미뤄 모든 Start()가
    // 끝난 다음에 갱신되도록 한다.
    private void Start()
    {
        RefreshBordersNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RefreshBordersNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        RefreshBorders();
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnChunkStateChanged.RemoveListener(RefreshBorders);

        if (_conquestManager != null)
            _conquestManager.OnExpeditionsChanged.RemoveListener(RefreshInProgressBorders);
    }

    private void RefreshBorders()
    {
        List<List<Vector2Int>> loops = BuildBorderLoops(CollectConqueredCells());

        for (int i = 0; i < loops.Count; i++)
        {
            LineRenderer lineRenderer = _borderPool.Get(i);
            SetLoopPositions(lineRenderer, loops[i], _borderColor, CONQUERED_BORDER_SORTING_ORDER);
        }

        _borderPool.DeactivateFrom(loops.Count);
    }

    // 원정 세트가 실제로 바뀌지 않은 날(날짜만 하루 늘어난 경우)에는 경계 재계산을 건너뛴다.
    private void RefreshInProgressBorders()
    {
        HashSet<Vector2Int> chunkCoords = CollectInProgressChunkCoords();
        if (chunkCoords.SetEquals(_lastInProgressChunkCoords))
            return;

        _lastInProgressChunkCoords = chunkCoords;

        List<List<Vector2Int>> loops = BuildBorderLoops(CollectCellsForChunks(chunkCoords));

        for (int i = 0; i < loops.Count; i++)
        {
            LineRenderer lineRenderer = _inProgressBorderPool.Get(i);
            SetLoopPositions(lineRenderer, loops[i], _inProgressBorderColor, IN_PROGRESS_BORDER_SORTING_ORDER);
        }

        _inProgressBorderPool.DeactivateFrom(loops.Count);
    }

    private void SetLoopPositions(LineRenderer lineRenderer, List<Vector2Int> loop, Color color, int sortingOrder)
    {
        lineRenderer.gameObject.SetActive(true);
        lineRenderer.loop = true;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = _lineWidth;
        lineRenderer.endWidth = _lineWidth;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.positionCount = loop.Count;

        for (int i = 0; i < loop.Count; i++)
        {
            lineRenderer.SetPosition(i, GetCornerWorldPosition(loop[i]));
        }
    }

    private List<List<Vector2Int>> BuildBorderLoops(HashSet<Vector3Int> cells)
    {
        List<(Vector2Int A, Vector2Int B)> borderEdges = CollectBorderEdges(cells);
        return TraceLoops(borderEdges);
    }

    // 청크 경계와 무관하게, 점령된 셀 전체를 하나의 영역으로 보고 바깥 경계 변만 수집한다.
    private HashSet<Vector3Int> CollectConqueredCells()
    {
        var conqueredCells = new HashSet<Vector3Int>();

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            if (chunk.CurrentState != ChunkState.Conquered)
                continue;

            foreach (GridCell cell in chunk.Cells)
            {
                conqueredCells.Add(cell.Coord);
            }
        }

        return conqueredCells;
    }

    // 원정을 보낸(아직 완료되지 않은) 청크 좌표만 모은다 - 캐시와 비교해 재계산 여부를 판단하는 용도.
    private HashSet<Vector2Int> CollectInProgressChunkCoords()
    {
        var chunkCoords = new HashSet<Vector2Int>();

        foreach (ConquestExpedition expedition in _conquestManager.ActiveExpeditions)
        {
            chunkCoords.Add(expedition.TargetChunkCoord);
        }

        return chunkCoords;
    }

    // 주어진 청크들에 속한 셀 전체를 수집한다.
    private HashSet<Vector3Int> CollectCellsForChunks(HashSet<Vector2Int> chunkCoords)
    {
        var cells = new HashSet<Vector3Int>();

        foreach (Vector2Int chunkCoord in chunkCoords)
        {
            Chunk chunk = _gridMap.GetChunk(chunkCoord);
            if (chunk == null)
                continue;

            foreach (GridCell cell in chunk.Cells)
            {
                cells.Add(cell.Coord);
            }
        }

        return cells;
    }

    // 셀 하나의 네 변(동서남북) 중 이웃 셀이 점령 상태가 아닌 변만 바깥 경계 변으로 수집한다.
    // 꼭짓점은 셀 좌표계의 정수 격자 인덱스로 표현한다 - 셀 (x,y)의 네 꼭짓점은
    // (x,y) / (x+1,y) / (x,y+1) / (x+1,y+1).
    private static List<(Vector2Int A, Vector2Int B)> CollectBorderEdges(HashSet<Vector3Int> cells)
    {
        var edges = new List<(Vector2Int A, Vector2Int B)>();

        foreach (Vector3Int coord in cells)
        {
            var bottomLeft = new Vector2Int(coord.x, coord.y);
            var bottomRight = new Vector2Int(coord.x + 1, coord.y);
            var topLeft = new Vector2Int(coord.x, coord.y + 1);
            var topRight = new Vector2Int(coord.x + 1, coord.y + 1);

            if (!cells.Contains(coord + Vector3Int.down))
                edges.Add((bottomLeft, bottomRight));

            if (!cells.Contains(coord + Vector3Int.up))
                edges.Add((topLeft, topRight));

            if (!cells.Contains(coord + Vector3Int.left))
                edges.Add((bottomLeft, topLeft));

            if (!cells.Contains(coord + Vector3Int.right))
                edges.Add((bottomRight, topRight));
        }

        return edges;
    }

    // 경계 변들을 꼭짓점 기준으로 이어붙여 폐곡선(들)을 구성한다.
    private static List<List<Vector2Int>> TraceLoops(List<(Vector2Int A, Vector2Int B)> edges)
    {
        var incidentEdges = new Dictionary<Vector2Int, List<int>>();

        for (int i = 0; i < edges.Count; i++)
        {
            AddIncidentEdge(incidentEdges, edges[i].A, i);
            AddIncidentEdge(incidentEdges, edges[i].B, i);
        }

        var isEdgeUsed = new bool[edges.Count];
        var loops = new List<List<Vector2Int>>();

        for (int startEdgeIndex = 0; startEdgeIndex < edges.Count; startEdgeIndex++)
        {
            if (isEdgeUsed[startEdgeIndex])
                continue;

            var loop = new List<Vector2Int>();
            Vector2Int startCorner = edges[startEdgeIndex].A;
            Vector2Int currentCorner = startCorner;
            Vector2Int nextCorner = edges[startEdgeIndex].B;
            isEdgeUsed[startEdgeIndex] = true;
            loop.Add(currentCorner);

            while (nextCorner != startCorner)
            {
                loop.Add(nextCorner);
                currentCorner = nextCorner;

                int nextEdgeIndex = FindUnusedIncidentEdge(incidentEdges, currentCorner, isEdgeUsed);
                if (nextEdgeIndex < 0)
                    break;

                isEdgeUsed[nextEdgeIndex] = true;
                nextCorner = edges[nextEdgeIndex].A == currentCorner ? edges[nextEdgeIndex].B : edges[nextEdgeIndex].A;
            }

            loops.Add(loop);
        }

        return loops;
    }

    private static void AddIncidentEdge(Dictionary<Vector2Int, List<int>> incidentEdges, Vector2Int corner, int edgeIndex)
    {
        if (!incidentEdges.TryGetValue(corner, out List<int> edgeIndices))
        {
            edgeIndices = new List<int>();
            incidentEdges[corner] = edgeIndices;
        }

        edgeIndices.Add(edgeIndex);
    }

    private static int FindUnusedIncidentEdge(Dictionary<Vector2Int, List<int>> incidentEdges, Vector2Int corner, bool[] isEdgeUsed)
    {
        foreach (int edgeIndex in incidentEdges[corner])
        {
            if (!isEdgeUsed[edgeIndex])
                return edgeIndex;
        }

        return -1;
    }

    // 격자 꼭짓점 (x,y)는 셀 (x-1,y-1)과 셀 (x,y)의 중심을 잇는 대각선의 중점과 같다
    // (아이소메트릭 격자는 두 기저벡터로 이루어진 평행사변형 격자이기 때문).
    private Vector3 GetCornerWorldPosition(Vector2Int corner)
    {
        Vector3 diagonalCellCenter = _gridMap.ConvertGridToWorld(new Vector3Int(corner.x - 1, corner.y - 1, 0));
        Vector3 cellCenter = _gridMap.ConvertGridToWorld(new Vector3Int(corner.x, corner.y, 0));
        Vector3 worldPos = (diagonalCellCenter + cellCenter) * CORNER_MIDPOINT_FACTOR;
        worldPos.y += MouseSelectController.GetYOffsetOrZero(_mouseSelectController);
        return worldPos;
    }
}
