using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 인접한 Conquered 청크들을 하나의 영역으로 보고 바깥 경계에만 녹색 테두리를 그린다
// (청크끼리 맞닿은 내부 경계선은 그리지 않음).
// 셀 단위로 이웃 셀의 점령 여부를 검사해 바깥 경계 변(edge)을 모으고, 변끼리 공유하는
// 꼭짓점을 이어붙여 폐곡선(loop)을 구성한 뒤 LineRenderer로 그린다.
[RequireComponent(typeof(GridMap))]
public class ConqueredChunkBorderRenderer : MonoBehaviour
{
    private const float CORNER_MIDPOINT_FACTOR = 0.5f;

    [SerializeField]
    private LineRenderer _borderLineRendererPrefab;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private Color _borderColor = Color.green;

    [SerializeField]
    private float _lineWidth = 0.05f;

    private GridMap _gridMap;
    private readonly List<LineRenderer> _borderPool = new();

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _gridMap.OnChunkStateChanged += RefreshBorders;
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
            _gridMap.OnChunkStateChanged -= RefreshBorders;
    }

    private void RefreshBorders()
    {
        List<List<Vector2Int>> loops = BuildConqueredBorderLoops();

        for (int i = 0; i < loops.Count; i++)
        {
            LineRenderer lineRenderer = GetPooledLineRenderer(i);
            SetLoopPositions(lineRenderer, loops[i]);
        }

        for (int i = loops.Count; i < _borderPool.Count; i++)
        {
            _borderPool[i].gameObject.SetActive(false);
        }
    }

    private void SetLoopPositions(LineRenderer lineRenderer, List<Vector2Int> loop)
    {
        lineRenderer.gameObject.SetActive(true);
        lineRenderer.loop = true;
        lineRenderer.startColor = _borderColor;
        lineRenderer.endColor = _borderColor;
        lineRenderer.startWidth = _lineWidth;
        lineRenderer.endWidth = _lineWidth;
        lineRenderer.positionCount = loop.Count;

        for (int i = 0; i < loop.Count; i++)
        {
            lineRenderer.SetPosition(i, GetCornerWorldPosition(loop[i]));
        }
    }

    private LineRenderer GetPooledLineRenderer(int index)
    {
        if (index >= _borderPool.Count)
        {
            _borderPool.Add(Instantiate(_borderLineRendererPrefab, transform));
        }

        LineRenderer pooled = _borderPool[index];
        pooled.gameObject.SetActive(true);
        return pooled;
    }

    // 청크 경계와 무관하게, 점령된 셀 전체를 하나의 영역으로 보고 바깥 경계 변만 수집한다.
    private List<List<Vector2Int>> BuildConqueredBorderLoops()
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

        List<(Vector2Int A, Vector2Int B)> borderEdges = CollectBorderEdges(conqueredCells);
        return TraceLoops(borderEdges);
    }

    // 셀 하나의 네 변(동서남북) 중 이웃 셀이 점령 상태가 아닌 변만 바깥 경계 변으로 수집한다.
    // 꼭짓점은 셀 좌표계의 정수 격자 인덱스로 표현한다 - 셀 (x,y)의 네 꼭짓점은
    // (x,y) / (x+1,y) / (x,y+1) / (x+1,y+1).
    private static List<(Vector2Int A, Vector2Int B)> CollectBorderEdges(HashSet<Vector3Int> conqueredCells)
    {
        var edges = new List<(Vector2Int A, Vector2Int B)>();

        foreach (Vector3Int coord in conqueredCells)
        {
            var bottomLeft = new Vector2Int(coord.x, coord.y);
            var bottomRight = new Vector2Int(coord.x + 1, coord.y);
            var topLeft = new Vector2Int(coord.x, coord.y + 1);
            var topRight = new Vector2Int(coord.x + 1, coord.y + 1);

            if (!conqueredCells.Contains(coord + Vector3Int.down))
                edges.Add((bottomLeft, bottomRight));

            if (!conqueredCells.Contains(coord + Vector3Int.up))
                edges.Add((topLeft, topRight));

            if (!conqueredCells.Contains(coord + Vector3Int.left))
                edges.Add((bottomLeft, topLeft));

            if (!conqueredCells.Contains(coord + Vector3Int.right))
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
        worldPos.y += GetOverlayYOffset();
        return worldPos;
    }

    private float GetOverlayYOffset() => _mouseSelectController != null ? _mouseSelectController.YOffset : 0f;
}
