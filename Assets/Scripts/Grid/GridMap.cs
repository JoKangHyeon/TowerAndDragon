using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

public class GridMap : MonoBehaviour
{
    [SerializeField]
    private Tilemap _tilemap;

    [SerializeField]
    private TerrainTileMap _terrainTileMap;

    [SerializeField]
    private ResourceNodeAreaTable _resourceNodeAreaTable;

    // 청크 단위 기본 생산량 테이블 - 점령/생산시설 판정과 동일한 granularity(청크)로 생산량을 정의한다.
    [SerializeField]
    private ChunkYieldTable _chunkYieldTable;

    // 청크 기본값을 셀에 구운 다음, 특정 셀만 자원별 생산량을 따로 지정하고 싶을 때 쓰는 오버라이드 테이블(선택 사항).
    [SerializeField]
    private CellYieldOverrideTable _cellYieldOverrideTable;

    // 셀 기본 생산량의 기준선·대각선 계수. 밸런싱 대상이라 상수가 아니라 에셋에서 읽는다.
    // 미연결이면 모든 셀의 생산량이 0이 되므로 필수 참조로 다룬다(WiringGuard.Require).
    [SerializeField]
    private EconomyBalanceData _economyBalance;

    // 셀→청크 소속 레이아웃 - 청크를 임의 모양으로 정의한다(ChunkLayoutEditorWindow로 저작).
    // 다른 테이블들과 달리 선택이 아니라 필수다: 이게 비면 청크가 하나도 생기지 않아
    // 초기 영토·점령·안개·건설 판정이 전부 무너진다.
    [SerializeField]
    private ChunkLayoutTable _chunkLayoutTable;

    // 청크 단위 연구 해금 조회 - 연구 시스템이 아직 없는 씬에서는 null로 두면 항상 터레인 기본값만으로 판정된다.
    public IChunkResearchUnlockQuery ResearchUnlockQuery { get; set; }

    // 청크 단위 생산량 배율 조회 - 연구 시스템이 없는 씬에서는 null로 두면 기본 배율 1을 적용한다.
    public IChunkYieldMultiplierQuery YieldMultiplierQuery { get; set; }

    // 봉인석 배치 판정 조회 - PortalSealManager가 Awake에 자신을 등록한다.
    // null이면 봉인석을 어디에도 지을 수 없다(fail-closed) - 영역을 모르는 채 아무데나 짓게 두면 안 되기 때문
    // (해금 여부의 null 기본값이 fail-open인 것과 방향이 반대이며, 의도된 것이다).
    public ISealStonePlacementQuery SealStonePlacementQuery { get; set; }

    //지형 기반 건설 제한을 예외적으로 해제하는 조외원 (얼음 새끼용 버프)
    // null 이면 해제 없음 (fail-closed) 미배선 씬은 기존 도작 그대로
    public IConstructionOverrideQuery ConstructionOverrideQuery {get; set;}

    // 몬스터 진격 경로 조회 - MonsterPathMap이 OnEnable에 자신을 등록한다.
    // null이면 임시 장애물(방벽)을 어디에도 세울 수 없다(fail-closed) - 경로를 모르는 채
    // 아무데나 세우게 두면 "길을 막는다"는 전제가 무너지기 때문(봉인석 조회원과 같은 방향).
    public IMonsterPathQuery MonsterPathQuery { get; set; }

    // 전체 맵
    private Dictionary<Vector3Int, GridCell> _cells = new();

    // 고저차 역변환(PickCellAtWorldPoint)에서 쓰는 탐색 파라미터 - CacheHeightSearchRange가 채운다.
    private const int HEIGHT_SEARCH_RANGE_UNINITIALIZED = -1;

    // 절벽 옆면을 짚었을 때 위쪽 타일을 찾아 올라가는 간격을 셀 한 칸의 몇 분의 1로 할지.
    // 촘촘할수록 얇은 다이아몬드 모서리를 건너뛰지 않는다.
    private const int SKIRT_PROBE_SUBDIVISION = 4;

    private int _heightSearchStepCount = HEIGHT_SEARCH_RANGE_UNINITIALIZED;
    private float _maxHeightOffset;
    private float _skirtProbeStep;

    // 건물이 차지하는 타일 맵
    private Dictionary<Building, List<GridCell>> _buildingFootprintCells = new();

    // 청크
    private Dictionary<Vector2Int, Chunk> _chunks = new();

    // 셀→청크 역인덱스. 청크가 직사각형이 아니므로 좌표 계산으로는 소속을 구할 수 없다 -
    // 예전의 정수 나눗셈(ToChunkCoord)을 대체한다. ChunkLayoutTable이 채우고,
    // 레이아웃이 빠뜨린 셀은 AssignUnmappedCellsToNearestChunk가 근접 청크에 흡수시킨다.
    private readonly Dictionary<Vector3Int, Vector2Int> _cellToChunk = new();

    // 청크 인접 그래프 - 셀이 실제로 맞닿아 있는지에서 파생한다(좌표 ±1 산술을 대체).
    // Bordering: 변을 공유 / Surrounding: 변 또는 꼭짓점을 공유.
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _borderingChunks = new();
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _surroundingChunks = new();

    // CollectChunksWithinDepth의 BFS 방문 집합 - 호출마다 새로 할당하지 않도록 재사용한다.
    private readonly HashSet<Vector2Int> _depthVisitedBuffer = new();

    private Dictionary<Vector2Int, Vector3> _chunkCenterWorldCache = new();
    private Dictionary<Vector2Int, int> _chunkBaseYieldCache = new();

    private readonly Dictionary<Vector2Int, int> _chunkYieldBuffer = new();

    // 좌표 목록으로 들어온 풋프린트를 셀로 되짚을 때 쓰는 재사용 버퍼(배치 미리보기는 커서를 옮길 때마다 조회한다).
    private readonly List<GridCell> _footprintCellBuffer = new();

    // 그리드 셀의 상태 변경 이벤트 - 건물 배치, 건물 파괴, 적 진입
    public UnityEvent<GridCell> OnCellChanged;

    // 청크 상태 변경 이벤트 - 점령/시야 확장 등 청크 단위 상태 전환 시에만 발생 (OnCellChanged보다 드묾)
    public UnityEvent OnChunkStateChanged;

    // 그리드에 건물이 등록되거나 제거되기 직전임을 외부 시스템에 알린다.
    // GridMap은 건물별 후속 처리 내용을 알지 않고 생명주기 시점만 전달한다.
    public UnityEvent<Building> OnBuildingAdded;
    public UnityEvent<Building> OnBuildingRemoving;

    /// <summary>
    /// 가장 최근 그리드에 등록된 건물. 건설 직후 이어지는 안내가 같은 종류의 기존 건물 대신
    /// 방금 지은 대상을 정확히 조명하는 데 쓴다.
    /// </summary>
    public Building LastAddedBuilding { get; private set; }

    // 건물이 같은 인스턴스를 유지한 채 좌표만 옮겨졌음을 알린다(MoveBuilding 전용).
    // OnBuildingAdded/OnBuildingRemoving은 발행하지 않으므로, 위치 기반 판정(예: 새끼용 버프 범위)을
    // 이동 시에도 갱신해야 하는 구독자는 이 이벤트를 따로 구독해야 한다.
    public UnityEvent<Building> OnBuildingMoved;

    private void Awake()
    {
        GenerateGridFromTilemap();
        GenerateChunks();
        ApplyResourceNodeAreas();
        ApplyCellYields();
        ApplyCellYieldOverrides();
    }

    private void GenerateGridFromTilemap()
    {
        float maxHeightOffset = 0f;

        foreach (var pos in _tilemap.cellBounds.allPositionsWithin)
        {
            if (!_tilemap.HasTile(pos))
                continue;

            TileBase tile = _tilemap.GetTile(pos);
            TerrainType terrain = _terrainTileMap.Resolve(tile);
            bool canConstruct = _terrainTileMap.ResolveCanConstruct(tile);

            GridCell cell = new GridCell(pos, terrain, canConstruct);
            cell.AddResourceNodes(_terrainTileMap.ResolveDefaultResourceNodes(tile));
            _cells[pos] = cell;

            maxHeightOffset = Mathf.Max(maxHeightOffset, GetHeightOffset(pos));
        }

        CacheHeightSearchRange(maxHeightOffset);

        Debug.Log($"[GridMap] 그리드맵 생성 완료 - 셀의 개수: {_cells.Count}, 최대 고저차: {maxHeightOffset}");
    }

    // PickCellAtWorldPoint가 후보 셀을 몇 칸까지 거슬러 검사할지 결정한다.
    // 셀 한 칸(그리드 +Y 1)이 화면에서 올라가는 높이로 최대 고저차를 나누면, 가장 높은 타일이
    // 자기 자리에서 몇 칸 뒤쪽까지 겹쳐 보이는지가 나온다. 타일 고저차는 런타임에 바뀌지 않으므로 한 번만 계산한다.
    // 오브젝트 간 Awake 순서는 보장되지 않으므로, GridMap.Awake보다 먼저 도는 쪽이 PickCellAtWorldPoint를
    // 부를 수 있다(PropFogTintController가 Awake에서 프롭 셀을 캐싱하는 경우 등). 그때 탐색 범위가 비어 있으면
    // 단차를 무시한 좌표가 조용히 나와버리므로, 아직 계산 전이면 여기서 한 번 직접 훑어 채운다.
    private void EnsureHeightSearchRange()
    {
        if (_heightSearchStepCount != HEIGHT_SEARCH_RANGE_UNINITIALIZED)
            return;

        float maxHeightOffset = 0f;

        foreach (var pos in _tilemap.cellBounds.allPositionsWithin)
        {
            if (_tilemap.HasTile(pos))
                maxHeightOffset = Mathf.Max(maxHeightOffset, GetHeightOffset(pos));
        }

        CacheHeightSearchRange(maxHeightOffset);
    }

    private void CacheHeightSearchRange(float maxHeightOffset)
    {
        float worldYPerCellStep =
            _tilemap.GetCellCenterWorld(new Vector3Int(0, 1, 0)).y - _tilemap.GetCellCenterWorld(Vector3Int.zero).y;

        _maxHeightOffset = maxHeightOffset;

        _heightSearchStepCount = worldYPerCellStep > 0f
            ? Mathf.CeilToInt(maxHeightOffset / worldYPerCellStep)
            : 0;

        _skirtProbeStep = worldYPerCellStep / SKIRT_PROBE_SUBDIVISION;
    }

    // 터레인 기본값(GenerateGridFromTilemap) 다음 단계 - 수기 지정 영역을 추가로 누적 적용한다.
    private void ApplyResourceNodeAreas()
    {
        if (_resourceNodeAreaTable != null)
            _resourceNodeAreaTable.ApplyToGrid(_cells);
    }

    // 성 좌표 기준 대각선·거리 공식 상수 - "대각선 지역은 경로에서 멀어 진격·방어 이점이 없는 대신
    // 자원이 풍부하다(리스크→리워드)"는 기획(Docs/기획종합_v2.md 8장)을 셀 좌표 단위로 직접 계산한다.
    private static readonly Vector2 CASTLE_POSITION = new Vector2(3f, 3f);
    private const float DEGREES_PER_QUADRANT = 90f;
    private const float DEGREES_PER_DIAGONAL_STEP = 45f;

    // 청크 생성 다음 단계 - 셀마다 성으로부터의 거리·대각선 정도로 원시 생산량을 계산하고,
    // 그 셀이 보유한 자원노드에 한해 청크(바이옴) 단위 자원별 배율을 곱한다.
    // 거리는 셀의 정확한 좌표로 연속적으로 계산되므로 같은 청크 안에서도 셀마다 값이 자연히 달라진다.
    private void ApplyCellYields()
    {
        if (!WiringGuard.Require(_economyBalance, nameof(_economyBalance), this))
        {
            return;
        }

        int baselineYield = _economyBalance.BaselineCellYield;
        float diagonalBonusFactor = _economyBalance.DiagonalBonusFactor;

        foreach (Chunk chunk in _chunks.Values)
        {
            int chunkBaseYieldSum = 0;

            foreach (GridCell cell in chunk.Cells)
            {
                int baseYield = CalculateDistanceYield(cell.Coord, baselineYield, diagonalBonusFactor);
                cell.SetBaseYield(baseYield);
                chunkBaseYieldSum += baseYield;

                foreach (ResourceType resourceType in EnumerateResourceFlags(cell.AvailableResourceNodes))
                {
                    float multiplier = _chunkYieldTable != null
                        ? _chunkYieldTable.ResolveResourceMultiplier(chunk.ChunkCoord, resourceType)
                        : 1f;

                    cell.SetYield(resourceType, Mathf.RoundToInt(baseYield * multiplier));
                }
            }

            _chunkBaseYieldCache[chunk.ChunkCoord] = chunkBaseYieldSum;
        }
    }

    // 기준선·대각선 계수는 밸런싱 대상이라 상수가 아니라 EconomyBalanceData에서 받아온다
    // (호출부인 ApplyCellYields가 에셋에서 한 번 읽어 넘긴다 - 셀마다 에셋을 다시 뒤지지 않기 위함).
    private static int CalculateDistanceYield(Vector3Int cellCoord, int baselineYield, float diagonalBonusFactor)
    {
        Vector2 offset = new Vector2(cellCoord.x, cellCoord.y) - CASTLE_POSITION;
        float distance = offset.magnitude;
        if (distance < Mathf.Epsilon)
            return baselineYield;

        float angleFromCardinal = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, DEGREES_PER_QUADRANT));
        if (angleFromCardinal > DEGREES_PER_DIAGONAL_STEP)
            angleFromCardinal = DEGREES_PER_QUADRANT - angleFromCardinal;

        float diagonalFactor = angleFromCardinal / DEGREES_PER_DIAGONAL_STEP;
        int bonus = Mathf.RoundToInt(diagonalFactor * distance * diagonalBonusFactor);

        return baselineYield + bonus;
    }

    private static readonly ResourceType[] ALL_RESOURCE_FLAGS =
        (ResourceType[])Enum.GetValues(typeof(ResourceType));

    // AvailableResourceNodes([Flags])에 실제로 켜진 개별 자원 비트만 순회한다.
    // Factory가 풋프린트에 걸친 여러 자원(슬라임 하위 타입 등)을 각각 정산할 때도 재사용한다 - Factory.OnSettlement 참고.
    public static IEnumerable<ResourceType> EnumerateResourceFlags(ResourceType flags)
    {
        foreach (ResourceType value in ALL_RESOURCE_FLAGS)
        {
            if (value != ResourceType.None && (flags & value) == value)
                yield return value;
        }
    }

    // 청크 기본값 적용 다음 단계 - 지정된 영역의 셀만 자원별 생산량을 덮어쓴다(선택 사항).
    private void ApplyCellYieldOverrides()
    {
        if (_cellYieldOverrideTable != null)
            _cellYieldOverrideTable.ApplyToGrid(_cells);
    }

    private void GenerateChunks()
    {
        BuildCellToChunkIndex();
        AssignUnmappedCellsToNearestChunk();

        var grouped = new Dictionary<Vector2Int, List<GridCell>>();

        foreach (GridCell cell in _cells.Values)
        {
            if (!_cellToChunk.TryGetValue(cell.Coord, out Vector2Int chunkCoord))
                continue;

            if (!grouped.TryGetValue(chunkCoord, out List<GridCell> cellsInChunk))
            {
                cellsInChunk = new List<GridCell>();
                grouped[chunkCoord] = cellsInChunk;
            }

            cellsInChunk.Add(cell);
        }

        foreach (var pair in grouped)
        {
            _chunks[pair.Key] = new Chunk(pair.Key, pair.Value);
        }

        foreach (var pair in _chunks)
        {
            Chunk chunk = pair.Value;
            if (chunk.Cells.Count == 0)
                continue;

            Vector3 sum = Vector3.zero;
            foreach (GridCell cell in chunk.Cells)
                sum += ConvertGridToWorld(cell.Coord);
            _chunkCenterWorldCache[pair.Key] = sum / chunk.Cells.Count;
        }

        BuildChunkAdjacency();

        Debug.Log($"[GridMap] 청크 생성 완료 - 청크 개수: {_chunks.Count}");
    }

    // 레이아웃 에셋에서 셀→청크 소속을 읽어온다. 그리드에 실제로 존재하지 않는 좌표(타일이 지워진
    // 자리에 지정만 남은 경우 등)는 버린다 - 유령 셀을 남기면 인접 그래프가 실재하지 않는 접촉을
    // 만들어내고, 청크가 자기 셀 목록에 없는 좌표를 소유한 것처럼 보인다.
    private void BuildCellToChunkIndex()
    {
        _cellToChunk.Clear();

        if (!WiringGuard.Require(_chunkLayoutTable, nameof(_chunkLayoutTable), this))
            return;

        _chunkLayoutTable.BuildCellToChunkIndex(_cellToChunk);

        var phantomCoords = new List<Vector3Int>();
        foreach (Vector3Int coord in _cellToChunk.Keys)
        {
            if (!_cells.ContainsKey(coord))
                phantomCoords.Add(coord);
        }

        foreach (Vector3Int coord in phantomCoords)
        {
            _cellToChunk.Remove(coord);
        }

        if (phantomCoords.Count > 0)
            Debug.LogWarning($"[GridMap] 타일이 없는 좌표 지정 {phantomCoords.Count}개를 무시했습니다.");
    }

    // 레이아웃이 지정하지 않은 셀을, 이미 지정된 셀에서 상하좌우로 퍼져나가며 가장 가까운 청크에 흡수시킨다.
    // 레거시 9×9 공식으로 폴백하지 않는 이유: 그러면 사라졌어야 할 정사각형 규칙이 런타임에 영원히 남고,
    // 디자이너가 청크를 옮긴 자리마다 유령 청크가 새로 생긴다.
    // 한 셀에 여러 청크가 같은 거리로 닿으면 좌표가 작은 청크가 이긴다 - 결과가 딕셔너리 순회 순서에
    // 좌우되지 않아야 같은 레이아웃이 항상 같은 맵을 만든다.
    private void AssignUnmappedCellsToNearestChunk()
    {
        int tableAssignedCount = _cellToChunk.Count;

        var unassignedCoords = new List<Vector3Int>();
        foreach (Vector3Int coord in _cells.Keys)
        {
            if (!_cellToChunk.ContainsKey(coord))
                unassignedCoords.Add(coord);
        }

        var wave = new List<(Vector3Int Coord, Vector2Int ChunkCoord)>();
        int absorbedCount = 0;

        while (unassignedCoords.Count > 0)
        {
            wave.Clear();

            foreach (Vector3Int coord in unassignedCoords)
            {
                if (TryResolveSmallestAdjacentChunk(coord, out Vector2Int chunkCoord))
                    wave.Add((coord, chunkCoord));
            }

            // 남은 미지정 셀 중 지정된 셀과 맞닿은 것이 하나도 없다 = 완전히 고립된 섬이다.
            if (wave.Count == 0)
                break;

            foreach ((Vector3Int coord, Vector2Int chunkCoord) in wave)
            {
                _cellToChunk[coord] = chunkCoord;
            }

            absorbedCount += wave.Count;
            unassignedCoords.RemoveAll(coord => _cellToChunk.ContainsKey(coord));
        }

        Debug.Log($"[GridMap] 셀→청크 인덱스 - 테이블 지정 {tableAssignedCount}, 근접 폴백 {absorbedCount}");

        if (unassignedCoords.Count > 0)
            Debug.LogError($"[GridMap] 어느 청크와도 연결되지 않은 고립 셀 {unassignedCoords.Count}개 - 레이아웃을 확인하세요.");
    }

    // 이 셀과 상하좌우로 맞닿은 "이미 지정된" 셀들의 청크 중 좌표가 가장 작은 것.
    private bool TryResolveSmallestAdjacentChunk(Vector3Int cellCoord, out Vector2Int smallestChunkCoord)
    {
        smallestChunkCoord = default;
        bool hasCandidate = false;

        foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
        {
            if (!_cellToChunk.TryGetValue(cellCoord + direction, out Vector2Int candidate))
                continue;

            if (!hasCandidate || IsSmallerChunkCoord(candidate, smallestChunkCoord))
            {
                smallestChunkCoord = candidate;
                hasCandidate = true;
            }
        }

        return hasCandidate;
    }

    private static bool IsSmallerChunkCoord(Vector2Int left, Vector2Int right) =>
        left.x != right.x ? left.x < right.x : left.y < right.y;

    // 청크 인접을 셀 접촉에서 파생한다.
    //
    // 물(Default) 셀도 포함해 판정하는 것이 중요하다 - 예전의 좌표 ±1 산술에서는 바다를 사이에 둔
    // 청크도 이웃이었고, "실제 지형이 얼마나 맞닿았는가"는 ConquestManager가 CountLandBorderContact로
    // 따로 판정한다. 여기서 육지만으로 인접을 정하면 해협 건너 점령이 통째로 막히는 기획 변경이 된다.
    private void BuildChunkAdjacency()
    {
        _borderingChunks.Clear();
        _surroundingChunks.Clear();

        foreach (Vector2Int chunkCoord in _chunks.Keys)
        {
            _borderingChunks[chunkCoord] = new List<Vector2Int>();
            _surroundingChunks[chunkCoord] = new List<Vector2Int>();
        }

        var borderingPairs = new HashSet<(Vector2Int, Vector2Int)>();
        var surroundingPairs = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (KeyValuePair<Vector3Int, Vector2Int> cellEntry in _cellToChunk)
        {
            AccumulateChunkContacts(cellEntry, CellDirections.ORTHOGONAL, borderingPairs, _borderingChunks);
            AccumulateChunkContacts(cellEntry, CellDirections.ALL_EIGHT, surroundingPairs, _surroundingChunks);
        }

        Debug.Log($"[GridMap] 인접 그래프 - 변 공유 {borderingPairs.Count}변, 변+꼭짓점 {surroundingPairs.Count}변");
    }

    private void AccumulateChunkContacts(KeyValuePair<Vector3Int, Vector2Int> cellEntry, Vector3Int[] directions,
        HashSet<(Vector2Int, Vector2Int)> registeredPairs, Dictionary<Vector2Int, List<Vector2Int>> adjacency)
    {
        foreach (Vector3Int direction in directions)
        {
            if (!_cellToChunk.TryGetValue(cellEntry.Key + direction, out Vector2Int neighborChunkCoord))
                continue;

            if (neighborChunkCoord == cellEntry.Value)
                continue;

            // 같은 청크 쌍은 수많은 셀에서 반복해 발견되므로, 무향 쌍을 정규화해 한 번만 등록한다.
            if (!registeredPairs.Add(MakeChunkPair(cellEntry.Value, neighborChunkCoord)))
                continue;

            if (adjacency.TryGetValue(cellEntry.Value, out List<Vector2Int> ownNeighbors))
                ownNeighbors.Add(neighborChunkCoord);

            if (adjacency.TryGetValue(neighborChunkCoord, out List<Vector2Int> otherNeighbors))
                otherNeighbors.Add(cellEntry.Value);
        }
    }

    private static (Vector2Int, Vector2Int) MakeChunkPair(Vector2Int a, Vector2Int b) =>
        IsSmallerChunkCoord(a, b) ? (a, b) : (b, a);


    public ChunkState GetCellState(Vector3Int coord)
    {
        if (_cells.TryGetValue(coord, out var cell))
            return cell.CurrentState;

        return ChunkState.Hidden;
    }

    public Vector3 ConvertGridToWorld(Vector3Int cellCoord)
    {
        Vector3 worldPos = _tilemap.GetCellCenterWorld(cellCoord);
        worldPos.y += GetHeightOffset(cellCoord);
        return worldPos;
    }

    // Isometric Z As Y 레이아웃에서는 셀 중심 평면보다 살짝 뜬 위치(오브젝트 피벗 등)를 넣으면
    // WorldToCell이 z를 0이 아닌 값으로 돌려줄 때가 있다 - 이 프로젝트의 실제 셀 좌표는 항상 z=0이므로
    // (셀 딕셔너리도 그렇게 키가 잡혀 있다) 여기서 강제로 맞춰, Props 등이 엉뚱한 z 탓에 셀 조회에
    // 실패해 항상 Hidden으로 취급되는 문제를 막는다.
    public Vector3Int ConvertWorldToGrid(Vector3 worldCoord)
    {
        Vector3Int coord = _tilemap.WorldToCell(worldCoord);
        coord.z = 0;
        return coord;
    }

    // 격자 꼭짓점 (x,y)는 셀 (x-1,y-1)과 셀 (x,y)의 중심을 잇는 대각선의 중점과 같다
    // (아이소메트릭 격자는 두 기저벡터로 이루어진 평행사변형 격자이기 때문).
    // 표시용 y 오프셋을 더하지 않은 지면 좌표다 - 셀 중심과 같은 기준을 써야 하는 계산(경계선 인셋 방향 등)이
    // 있어서 오프셋은 호출자가 필요할 때만 더한다.
    public Vector3 GetCellCornerWorld(Vector2Int corner)
    {
        Vector3 diagonalCellCenter = ConvertGridToWorld(new Vector3Int(corner.x - 1, corner.y - 1, 0));
        Vector3 cellCenter = ConvertGridToWorld(new Vector3Int(corner.x, corner.y, 0));
        return (diagonalCellCenter + cellCenter) * CORNER_MIDPOINT_FACTOR;
    }

    private const float CORNER_MIDPOINT_FACTOR = 0.5f;

    // 지형 타일에 심어둔 고저차(Y 오프셋)를 읽어온다 - Isometric Z As Y 레이아웃에서 셀의 Z좌표는
    // 정렬용으로만 쓰이고 높이는 SetTransformMatrix로 부여한 타일별 렌더 오프셋으로 표현된다.
    public float GetHeightOffset(Vector3Int cellCoord) => _tilemap.GetTransformMatrix(cellCoord).GetColumn(3).y;

    // 화면상의 한 점(마우스 위치 등)이 실제로 어느 타일 위인지 고른다.
    // ConvertWorldToGrid는 고저차를 무시한 평면 역변환이라, ConvertGridToWorld가 더해준 Y 오프셋을
    // 되돌리지 못한다 - 그래서 단차가 높은 곳에서는 눈에 보이는 타일과 몇 칸씩 어긋난 셀이 나온다.
    // 여기서는 후보 셀마다 "그 셀의 고저차만큼 내려서 평면 역변환하면 자기 자신이 나오는가"를 직접
    // 검사해, 그 점을 실제로 덮고 있는 타일만 남긴다. 한 점을 여러 타일이 덮으면 더 높은 쪽이 더 앞쪽
    // (카메라에 가까운 셀)이라 화면에서도 그쪽이 위에 그려지므로, 가장 높은 타일을 고른다.
    public Vector3Int PickCellAtWorldPoint(Vector3 worldCoord)
    {
        EnsureHeightSearchRange();

        if (TryPickCoveringCell(worldCoord, out Vector3Int coveringCoord))
            return coveringCoord;

        // 어느 타일의 윗면도 이 점을 덮지 않는다 = 단차의 옆면(절벽면)을 가리키고 있다는 뜻이다.
        // 옆면은 바로 위 타일이 아래로 드리운 부분이므로, 조금씩 위로 올라가며 처음 만나는 윗면의
        // 타일을 고른다. 여기서 평면 역변환 결과를 그대로 쓰면 안 된다 - 그 셀은 절벽 뒤에 가려져
        // 보이지도 않는 데다 커서에서 최대 5칸 이상 떨어져 있어, 커서와 동떨어진 곳이 선택된다.
        // 탐침 간격이 0이면(고저차가 없는 그리드 등) 올라갈 이유도 없고 무한 루프가 되므로 건너뛴다.
        if (_skirtProbeStep > 0f)
        {
            for (float lift = _skirtProbeStep; lift <= _maxHeightOffset; lift += _skirtProbeStep)
            {
                if (TryPickCoveringCell(worldCoord + new Vector3(0f, lift, 0f), out Vector3Int aboveCoord))
                    return aboveCoord;
            }
        }

        return ConvertWorldToGrid(worldCoord);
    }

    // 이 점을 윗면으로 덮고 있는 타일을 찾는다. 후보 셀마다 "그 셀의 고저차만큼 내려서 평면 역변환하면
    // 자기 자신이 나오는가"를 검사하는 방식이라 판정이 정확하다. 한 점을 여러 타일이 덮으면 더 높은 쪽이
    // 더 앞쪽(카메라에 가까운 셀)이라 화면에서도 위에 그려지므로, 가장 높은 타일을 고른다.
    private bool TryPickCoveringCell(Vector3 worldCoord, out Vector3Int coveringCoord)
    {
        Vector3Int flatCoord = ConvertWorldToGrid(worldCoord);

        coveringCoord = flatCoord;
        bool hasCovering = false;
        float pickedHeight = 0f;

        for (int xStep = 0; xStep <= _heightSearchStepCount; xStep++)
        {
            for (int yStep = 0; xStep + yStep <= _heightSearchStepCount; yStep++)
            {
                Vector3Int candidateCoord = new Vector3Int(flatCoord.x - xStep, flatCoord.y - yStep, flatCoord.z);

                if (!_tilemap.HasTile(candidateCoord))
                    continue;

                // 이미 찾아둔 것보다 낮은(=뒤쪽) 후보는 그 타일에 가려지므로 검사할 필요가 없다.
                float candidateHeight = GetHeightOffset(candidateCoord);
                if (hasCovering && candidateHeight <= pickedHeight)
                    continue;

                if (ConvertWorldToGrid(worldCoord - new Vector3(0f, candidateHeight, 0f)) != candidateCoord)
                    continue;

                coveringCoord = candidateCoord;
                pickedHeight = candidateHeight;
                hasCovering = true;
            }
        }

        return hasCovering;
    }

    // 전장의 안개(FogOfWarRenderer)가 지형 타일 자체를 SetColor로 어둡게 틴트하기 위해 참조한다.
    public Tilemap TerrainTilemap => _tilemap;

    // 지형상 건설 불가 셀이라도 해제 조회원이 허용하면 건설 가능으로 취급한다
    private bool IsCellConstructible(GridCell cell) =>
        cell.CanConstruct ||
        (ConstructionOverrideQuery != null &&
        ConstructionOverrideQuery.IsConstructionAllowed(cell.Coord, cell.TerrainType));

    // 판정과 실패 사유 진단을 GetCellBlockReason 한 곳에서 낸다 - 조건을 양쪽에 따로 쓰면
    // 한쪽에만 조건이 추가됐을 때 안내가 조용히 엉뚱한 사유를 가리킨다.
    public bool CanConstructBuilding(Vector3Int coord) =>
        CanConstructBuilding(coord, null);

    // building: null - 생산시설의 자원 노드 요건은 보지 않는다(이 판정의 원래 동작 그대로).
    public bool CanConstructBuilding(Vector3Int coord, Building ignoreBuilding) =>
        GetCellBlockReason(coord, building: null, ignoreBuilding: ignoreBuilding, ignoresTerrain: false) ==
        PlacementBlockReason.None;

    // 성 같은 고정 구조물은 RegisterFootprint()로 이 검사를 우회해 배치한다(점령 상태와 무관하게 등록).
    private bool IsChunkConquered(Vector3Int coord)
    {
        Chunk chunk = GetChunkAt(coord);
        return chunk != null && chunk.CurrentState == ChunkState.Conquered;
    }

    public ExistTypeOnCell ExamExist(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.ExistTypeOnCell : ExistTypeOnCell.None;

    public ResourceType GetAvailableResourceNodes(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.AvailableResourceNodes : ResourceType.None;

    // 셀 하나의 지형. 지역 페널티(TerrainPenaltySystem)가 건물 풋프린트를 지형별로 세는 데 쓴다.
    // 그리드 밖 좌표는 물(Default)로 취급한다 - 지형이 없는 곳이므로 어떤 지역 효과도 받지 않는다.
    public TerrainType GetTerrainType(Vector3Int coord) =>
        _cells.TryGetValue(coord, out GridCell cell) ? cell.TerrainType : TerrainType.Default;

    public IEnumerable<Vector3Int> EnumerateAllCoords() => _cells.Keys;

    public bool IsNaturallyConstructible (Vector3Int coord) =>
        _cells.TryGetValue(coord, out GridCell cell) && cell.CanConstruct;

    // 디버그 오버레이/로그 전용 원시 지형 생산력 - 자원 종류·연구 강화와 무관한 순수 값이다.
    public int GetBaseYield(Vector3Int coord) =>
        _cells.TryGetValue(coord, out GridCell cell) ? cell.BaseYield : 0;

    // 디버그 전용 - 특정 셀의 자원별 실제 생산량(청크 배율 적용 후, 연구 강화 미적용).
    public int GetYield(Vector3Int coord, ResourceType resourceType) =>
        _cells.TryGetValue(coord, out GridCell cell) ? cell.GetYield(resourceType) : 0;

    public Building GetBuildingAt(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.OccupantBuilding : null;

    // 현재 그리드에 등록된 모든 건물(읽기 전용). 생산량 예측 등 건물 전체 순회에 쓴다.
    public IEnumerable<Building> Buildings => _buildingFootprintCells.Keys;

    public List<Vector3Int> GetAllOccupiedCoords()
    {
        var result = new List<Vector3Int>();
        foreach (List<GridCell> footprint in _buildingFootprintCells.Values)
            foreach (GridCell cell in footprint)
                result.Add(cell.Coord);
        return result;
    }

    public Chunk GetChunkAt(Vector3Int cellCoord) =>
        _cellToChunk.TryGetValue(cellCoord, out Vector2Int chunkCoord) &&
        _chunks.TryGetValue(chunkCoord, out Chunk chunk)
            ? chunk
            : null;

    /// <summary>셀이 속한 청크 좌표. 그리드 밖이거나 어느 청크에도 속하지 않으면 false.
    /// 실패했을 때 default(Vector2Int)를 그대로 쓰면 (0,0) 청크의 데이터가 잘못 적용되므로
    /// 호출자는 반드시 반환값을 확인해야 한다.</summary>
    public bool TryGetChunkCoord(Vector3Int cellCoord, out Vector2Int chunkCoord) =>
        _cellToChunk.TryGetValue(cellCoord, out chunkCoord);

    public IEnumerable<Chunk> GetAllChunks() => _chunks.Values;


    // 등록된 건물 중 T 하나를, 그 건물이 점유한 셀 하나와 함께 돌려준다. 좌표를 모르는 상태에서
    // 좌표 기반 API(SelectExistingBuildingAt 등)를 호출해야 할 때 쓴다.
    public T FindBuilding<T>(out Vector3Int occupiedCoord) where T : Building
    {
        foreach (KeyValuePair<Building, List<GridCell>> entry in _buildingFootprintCells)
        {
            if (entry.Key is T typedBuilding && entry.Value.Count > 0)
            {
                occupiedCoord = entry.Value[0].Coord;
                return typedBuilding;
            }
        }

        occupiedCoord = default;
        return null;
    }

    // 건물 인스턴스가 점유한 셀 하나. 좌표를 받는 API(BuildingPlacementController.SelectExistingBuildingAt 등)에
    // Building 객체를 넘겨야 할 때 쓰는, FindBuilding<T>의 "타입이 아니라 인스턴스로 찾는" 판이다.
    // GetOccupiedCoords/GetFootprintCoords로도 되짚을 수 있지만 리스트를 새로 할당하고 첫 원소만 쓰게 된다.
    public bool TryGetOccupiedCoord(Building building, out Vector3Int occupiedCoord)
    {
        if (building != null
            && _buildingFootprintCells.TryGetValue(building, out List<GridCell> footprint)
            && footprint.Count > 0)
        {
            occupiedCoord = footprint[0].Coord;
            return true;
        }

        occupiedCoord = default;
        return false;
    }

    public bool HasBuilding<T>() where T : Building
    {
        foreach (Building building in _buildingFootprintCells.Keys)
        {
            if (building is T)
            {
                return true;
            }
        }

        return false;
    }

    public void SetCellState(Vector3Int coord, ChunkState newState)
    {
        if (!_cells.TryGetValue(coord, out GridCell cell))
            return;
        
        cell.SetState(newState);
        OnCellChanged?.Invoke(cell);
    }

    public void SetChunkState(Vector2Int chunkCoord, ChunkState newState)
    {
        if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
            return;

        ChunkState previousState = chunk.CurrentState;
        chunk.SetState(newState);

        foreach (GridCell cell in chunk.Cells)
        {
            OnCellChanged?.Invoke(cell);
        }

        // 점령 여부와 무관한 상태 전환(예: Hidden -> Visible)은 점령 테두리 등
        // Conquered 집합에 의존하는 구독자에게 무의미하므로 이벤트를 생략한다.
        bool affectsConqueredSet = previousState == ChunkState.Conquered || newState == ChunkState.Conquered;
        if (affectsConqueredSet)
            OnChunkStateChanged?.Invoke();
    }

    // [테스트/일괄 처리 전용] 여러 청크의 상태를 한 번에 바꾼다.
    // SetChunkState를 청크마다 호출하면 OnChunkStateChanged 구독자(청크 테두리 렌더러 등)가
    // 매번 맵 전체를 다시 훑어 재계산하므로 청크 수가 많을 때 사실상 O(N^2)이 되어 프레임이 멈춘다.
    // 여기서는 상태만 먼저 전부 바꾸고, 점령 집합에 영향을 준 경우에 한해 이벤트를 마지막에 한 번만 쏜다.
    public void SetChunkStatesBulk(IReadOnlyList<Vector2Int> chunkCoords, ChunkState newState)
    {
        bool affectsConqueredSet = false;

        foreach (Vector2Int chunkCoord in chunkCoords)
        {
            if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
                continue;

            ChunkState previousState = chunk.CurrentState;
            chunk.SetState(newState);

            foreach (GridCell cell in chunk.Cells)
            {
                OnCellChanged?.Invoke(cell);
            }

            affectsConqueredSet |= previousState == ChunkState.Conquered || newState == ChunkState.Conquered;
        }

        if (affectsConqueredSet)
            OnChunkStateChanged?.Invoke();
    }

    public void SetChunkState(Vector3Int cellCoord, ChunkState newState)
    {
        Chunk chunk = GetChunkAt(cellCoord);
        if (chunk != null)
            SetChunkState(chunk.ChunkCoord, newState);
    }
    
    public Chunk GetChunk(Vector2Int chunkCoord) =>
        _chunks.TryGetValue(chunkCoord, out Chunk chunk) ? chunk : null;

    public Vector3 GetChunkCenterWorld(Vector2Int chunkCoord) =>
        _chunkCenterWorldCache.TryGetValue(chunkCoord, out Vector3 center) ? center : Vector3.zero;

    /// <summary>변 또는 꼭짓점을 공유하는 이웃 청크 - 시야 확장용(예전의 8방향에 대응).</summary>
    public IEnumerable<Chunk> GetSurroundingChunks(Vector2Int chunkCoord) =>
        EnumerateAdjacentChunks(_surroundingChunks, chunkCoord);

    /// <summary>변을 공유하는 직접 접경 청크 - 점령 출격 가능 여부 판정 전용(예전의 4방향에 대응).
    /// 꼭짓점만 스친 청크는 제외된다는 점이 GetSurroundingChunks와의 차이다.</summary>
    public IEnumerable<Chunk> GetBorderingChunks(Vector2Int chunkCoord) =>
        EnumerateAdjacentChunks(_borderingChunks, chunkCoord);

    private IEnumerable<Chunk> EnumerateAdjacentChunks(
        Dictionary<Vector2Int, List<Vector2Int>> adjacency, Vector2Int chunkCoord)
    {
        if (!adjacency.TryGetValue(chunkCoord, out List<Vector2Int> neighborCoords))
            yield break;

        foreach (Vector2Int neighborCoord in neighborCoords)
        {
            if (_chunks.TryGetValue(neighborCoord, out Chunk neighbor))
                yield return neighbor;
        }
    }

    /// <summary>origin에서 접경을 depth단계까지 넓혀 만나는 청크 좌표를 result에 채운다(origin 자신은 제외).
    /// 정사각형 반경을 대체한다 - 자유 형태 청크에서는 좌표 거리가 아니라 "몇 단계 접경인가"가 주변의 의미다.</summary>
    public void CollectChunksWithinDepth(Vector2Int origin, int depth, List<Vector2Int> result)
    {
        result.Clear();
        _depthVisitedBuffer.Clear();
        _depthVisitedBuffer.Add(origin);

        int frontierStart = 0;
        int frontierEnd = 0;

        for (int step = 0; step < depth; step++)
        {
            if (step == 0)
            {
                AppendUnvisitedNeighbors(origin, result);
            }
            else
            {
                for (int i = frontierStart; i < frontierEnd; i++)
                {
                    AppendUnvisitedNeighbors(result[i], result);
                }
            }

            frontierStart = frontierEnd;
            frontierEnd = result.Count;

            // 이번 단계에서 새로 넓어진 청크가 없으면 남은 단계를 돌 이유가 없다.
            if (frontierStart == frontierEnd)
                break;
        }
    }

    private void AppendUnvisitedNeighbors(Vector2Int chunkCoord, List<Vector2Int> result)
    {
        if (!_surroundingChunks.TryGetValue(chunkCoord, out List<Vector2Int> neighborCoords))
            return;

        foreach (Vector2Int neighborCoord in neighborCoords)
        {
            if (_depthVisitedBuffer.Add(neighborCoord))
                result.Add(neighborCoord);
        }
    }

    // 반환값은 생성된 인스턴스(실패 시 null) - 호출부가 GetBuildingAt(anchor)으로 되짚지 않아도 되고,
    // 앵커 칸이 비어 있는 모양(구멍 뚫린 footprint)에서 되짚기가 빗나가는 문제도 생기지 않는다.
    public Building ConstructBuilding(Building prefab, Vector3Int anchor, int rotationSteps)
    {
        if (prefab == null)
            return null;

        FootprintShape rotatedShape = prefab.BaseFootprintShape.Rotated(rotationSteps);

        if (!TryGetFootprint(anchor, rotatedShape, prefab, null, out List<GridCell> footprint))
            return null;

        Building building = CreatePlacedInstance(prefab, anchor, rotationSteps);
        building.SetPlacementAnchor(anchor);
        building.SetDepthSortOrder(IsometricMath.ComputeDepthSortOrder(anchor));

        foreach (GridCell cell in footprint)
            cell.PlaceBuilding(building);

        _buildingFootprintCells[building] = footprint;

        foreach (GridCell cell in footprint)
            OnCellChanged?.Invoke(cell);

        LastAddedBuilding = building;
        OnBuildingAdded?.Invoke(building);
        return building;
    }

    // 프리팹을 배치 위치·회전으로 인스턴스화하는 부분만 떼어낸 것 - 일반 건설과 세이브 복원이 공유한다.
    // 셀 점유·이벤트 발행은 하지 않으므로 호출부가 반드시 이어서 등록해야 한다.
    private Building CreatePlacedInstance(Building prefab, Vector3Int anchor, int rotationSteps)
    {
        FootprintShape rotatedShape = prefab.BaseFootprintShape.Rotated(rotationSteps);

        Vector3 baseOffset = prefab.transform.localPosition;
        Vector3 baseScale = prefab.transform.localScale;
        Vector3 worldPos = GetFootprintCenterWorld(anchor, rotatedShape)
            + prefab.ComputePlacementOffset(rotationSteps)
            + ComputeRotationCompensation(prefab.BaseFootprintShape, rotationSteps);

        Building building = Instantiate(
            prefab,
            worldPos,
            prefab.transform.rotation,
            transform);
        building.SetPlacementOffset(baseOffset);
        building.SetBaseScale(baseScale);
        building.SetRotation(rotationSteps);
        return building;
    }

    /// <summary>
    /// 세이브 복원 전용 배치. 일반 배치 판정(점령·지형·자원노드·봉인석 사이트)을 다시 묻지 않고,
    /// 셀이 존재하고 비어 있는지만 본다 - 성이 쓰는 RegisterFootprint와 같은 등급의 우회다.
    ///
    /// 재판정하지 않는 이유: 세이브에 남아 있다는 것 자체가 "합법적으로 놓였다"의 증거인 반면,
    /// 복원은 순서상 아직 되돌아오지 않은 조건이 있다. 얼음 새끼용 버프로 풀리는 용암 지대가
    /// 대표적이다 - 그 새끼용이 목록 뒤쪽이면 아직 배치 전이라, 다시 물으면 멀쩡한 건물이 조용히 사라진다.
    ///
    /// LastAddedBuilding은 복원 중 건물마다 덮어써진다 - 반환값을 쓰고 그 프로퍼티에 의존하지 말 것.
    /// </summary>
    public Building RestoreBuilding(Building prefab, Vector3Int anchor, int rotationSteps)
    {
        if (prefab == null)
            return null;

        Building building = CreatePlacedInstance(prefab, anchor, rotationSteps);

        // 실패 사유(셀 없음/이미 점유)는 RegisterFootprint가 이미 경고로 남긴다.
        if (!RegisterFootprint(building, anchor))
        {
            Destroy(building.gameObject);
            return null;
        }

        return building;
    }

    // 회전 스텝에 따라 가로/세로 축의 짝홀이 서로 바뀌면서 생기는 어긋남(FootprintShape.ParityMismatch 차이)을
    // 실제 월드 좌표(아이소메트릭 셀 크기 반영)로 환산한다 - 회전해도 시각적 중심이 유지되도록 하는 자동 보정.
    // 기준(회전 0)과의 차이이므로, 어떤 두 스텝 사이를 옮겨도(예: 1->3) 일관되게 성립한다.
    public Vector3 ComputeRotationCompensation(FootprintShape baseShape, int rotationSteps)
    {
        if (baseShape == null)
            return Vector3.zero;

        Vector2 mismatchDelta = baseShape.ParityMismatch - baseShape.Rotated(rotationSteps).ParityMismatch;

        Vector3 unitX = ConvertGridToWorld(Vector3Int.right) - ConvertGridToWorld(Vector3Int.zero);
        Vector3 unitY = ConvertGridToWorld(Vector3Int.up) - ConvertGridToWorld(Vector3Int.zero);

        return unitX * mismatchDelta.x + unitY * mismatchDelta.y;
    }

    // 타일맵 셀 좌표계의 정중앙 셀.
    public Vector3Int GetCenterCell()
    {
        BoundsInt bounds = _tilemap.cellBounds;
        return new Vector3Int(
            bounds.xMin + bounds.size.x / 2,
            bounds.yMin + bounds.size.y / 2,
            0);
    }

    // 이미 생성된 building 인스턴스의 footprint 셀을 점유 등록한다(생성은 호출자 담당).
    // 배치 규칙(CanConstruct)과 무관하게, 셀이 존재하고 비어 있으면 점유한다(성 같은 고정 구조물용).
    public bool RegisterFootprint(Building building, Vector3Int anchor)
    {
        if (building == null)
            return false;

        var footprint = new List<GridCell>();
        foreach (Vector3Int coord in GetFootprintCoords(anchor, building.FootprintShape))
        {
            if (!_cells.TryGetValue(coord, out GridCell cell) || cell.HasBuilding)
            {
                Debug.LogWarning($"[GridMap] RegisterFootprint 실패 - 셀 없음/이미 점유: {coord}");
                return false;
            }

            footprint.Add(cell);
        }

        foreach (GridCell cell in footprint)
        {
            cell.PlaceBuilding(building);
            OnCellChanged?.Invoke(cell);
        }

        _buildingFootprintCells[building] = footprint;
        building.SetPlacementAnchor(anchor);
        building.SetDepthSortOrder(IsometricMath.ComputeDepthSortOrder(anchor));
        LastAddedBuilding = building;
        OnBuildingAdded?.Invoke(building);
        return true;
    }

    public bool TryGetFootprint(Vector3Int anchor, FootprintShape shape, out List<GridCell> footprint) =>
        TryGetFootprint(anchor, shape, null, null, out footprint);

    public bool TryGetFootprint(Vector3Int anchor, FootprintShape shape, Building ignoreBuilding, out List<GridCell> footprint)
        => TryGetFootprint(anchor, shape, ignoreBuilding, ignoreBuilding, out footprint);

    private bool TryGetFootprint(
        Vector3Int anchor,
        FootprintShape shape,
        Building candidateBuilding,
        Building ignoreBuilding,
        out List<GridCell> footprint)
    {
        footprint = new List<GridCell>();
        List<Vector3Int> footprintCoords = GetFootprintCoords(anchor, shape);

        if (!CanConstructBuildingFootprint(footprintCoords, candidateBuilding, ignoreBuilding))
            return false;

        foreach (Vector3Int coord in footprintCoords)
        {
            if (!_cells.TryGetValue(coord, out GridCell cell))
                return false;

            footprint.Add(cell);
        }

        Debug.Log($"[GridMap] footprint 카운트: {footprint.Count}, 앵커 포스: {anchor}");
        return true;
    }

    public List<Vector3Int> GetFootprintCoords(Vector3Int anchor, FootprintShape shape)
    {
        var coords = new List<Vector3Int>();

        foreach (Vector2Int offset in shape.GetOccupiedOffsets())
        {
            coords.Add(anchor + new Vector3Int(offset.x, offset.y, 0));
        }

        return coords;
    }

    public Vector3 GetFootprintCenterWorld(Vector3Int anchor, FootprintShape shape)
    {
        Vector3Int farCorner = anchor + new Vector3Int(shape.Width - 1, shape.Height - 1, 0);
        return (ConvertGridToWorld(anchor) + ConvertGridToWorld(farCorner)) / 2f;
    }

    public bool CanConstructFootPrint(Vector3Int anchor, FootprintShape shape) =>
        CanConstructFootPrint(anchor, shape, null);

    public bool CanConstructFootPrint(Vector3Int anchor, FootprintShape shape, Building ignoreBuilding) =>
        CanConstructFootPrint(GetFootprintCoords(anchor, shape), ignoreBuilding);

    // 호출자가 이미 GetFootprintCoords로 footprint를 계산해 둔 경우, 재계산 없이 그 결과를 그대로 검사한다.
    public bool CanConstructFootPrint(List<Vector3Int> footprint, Building ignoreBuilding)
    {
        foreach (Vector3Int coord in footprint)
        {
            if (!CanConstructBuilding(coord, ignoreBuilding))
                return false;
        }

        return true;
    }

    // 전투 중 스킬로 설치되는 임시 장애물(암석 액티브의 방벽) 전용 판정.
    //
    // 일반 건설 판정(CanConstructBuildingFootprint)을 쓰면 안 된다 - 그쪽은 "지형이 건설 가능" +
    // "청크가 점령 완료"를 요구하는데, 방벽은 정의상 몬스터가 지나오는 길목에 세우는 물건이라
    // 그 두 조건이 거의 항상 거짓이다(길·미점령 지역). 실제로 그 판정을 쓰던 동안에는 메테오의
    // 피해만 들어가고 방벽은 한 번도 설치되지 않았다.
    //
    // 원하는 지점 근처에서 실제로 방벽을 세울 수 있는 칸을 찾아 준다.
    //
    // 경로는 스플라인 한 줄을 구운 것이라 폭이 1칸뿐인데, 화면에 보이는 길은 그보다 넓고
    // 플레이어는 길이 아니라 몬스터를 보고 조준한다 - 중심선을 정확히 찍으라고 요구하면
    // 사실상 쓸 수 없는 스킬이 된다. 그래서 가까운 순서로 훑어 경로 칸으로 스냅시킨다.
    // 스냅 대상이 CanPlaceTemporaryObstacle를 통과한 칸이므로, 결과는 반드시 경로 위다
    // (경로를 넓히는 방식은 길 옆에 세워져 아무것도 막지 못하는 경우가 생겨 택하지 않았다).
    public bool TryResolveTemporaryObstacleAnchor(
        Vector3Int desired, FootprintShape shape, int maxSnapDistance, out Vector3Int anchor)
    {
        for (int distance = 0; distance <= maxSnapDistance; distance++)
        {
            for (int offsetX = -distance; offsetX <= distance; offsetX++)
            {
                for (int offsetY = -distance; offsetY <= distance; offsetY++)
                {
                    // 이미 더 가까운 거리에서 검사한 안쪽은 건너뛰고 이번 링의 테두리만 본다.
                    if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) != distance)
                        continue;

                    var candidate = new Vector3Int(desired.x + offsetX, desired.y + offsetY, desired.z);

                    if (CanPlaceTemporaryObstacle(candidate, shape))
                    {
                        anchor = candidate;
                        return true;
                    }
                }
            }
        }

        anchor = desired;
        return false;
    }

    // 대신 성(Castle)이 RegisterFootprint로 우회할 때와 같은 최소 조건에, 방벽 고유의 조건인
    // "몬스터 진격 경로 위일 것"을 더한다 - 길을 막는 물건이므로 길 밖에 세우는 건 의미가 없다.
    public bool CanPlaceTemporaryObstacle(Vector3Int anchor, FootprintShape shape)
    {
        // 경로를 모르면 세우지 않는다(fail-closed) - 조회원이 없다고 아무데나 허용하면
        // 배선을 빠뜨린 씬에서 조용히 "어디에나 설치 가능"으로 되돌아간다.
        if (MonsterPathQuery == null)
            return false;

        foreach (Vector3Int coord in GetFootprintCoords(anchor, shape))
        {
            if (!_cells.TryGetValue(coord, out GridCell cell) || cell.HasBuilding)
                return false;

            if (!MonsterPathQuery.IsOnMonsterPath(coord))
                return false;
        }

        return true;
    }

    // 생산시설 전용 배치 판정 - 기존 CanConstructFootPrint에 더해, 풋프린트 전체 셀이 요구 자원 플래그를 가져야 한다.
    // 자원 플래그는 (터레인 기반 정적 플래그) 또는 (청크 단위 연구 해금) 둘 중 하나만 만족해도 된다.
    public bool CanConstructResourceFootprint(Vector3Int anchor, FootprintShape shape, ResourceType requiredResourceNode) =>
        CanConstructResourceFootprint(anchor, shape, requiredResourceNode, null);

    // ignoreBuilding - 재배치 시 자기 자신이 점유한 칸도 유효하게 판정하기 위함(CanConstructFootPrint와 동일한 용도).
    public bool CanConstructResourceFootprint(Vector3Int anchor, FootprintShape shape, ResourceType requiredResourceNode, Building ignoreBuilding) =>
        CanConstructResourceFootprint(GetFootprintCoords(anchor, shape), requiredResourceNode, ignoreBuilding);

    // 호출자가 이미 GetFootprintCoords로 footprint를 계산해 둔 경우, 재계산 없이 그 결과를 그대로 검사한다(CanConstructFootPrint의 List 오버로드와 동일한 목적).
    public bool CanConstructResourceFootprint(List<Vector3Int> footprint, ResourceType requiredResourceNode, Building ignoreBuilding)
    {
        if (!CanConstructFootPrint(footprint, ignoreBuilding))
            return false;

        foreach (Vector3Int coord in footprint)
        {
            if (!_cells.TryGetValue(coord, out GridCell cell) || !SatisfiesResourceRequirement(cell, requiredResourceNode))
                return false;
        }

        return true;
    }

    // 어느 청크에도 속하지 않은 셀은 청크 단위 연구 해금을 조회할 근거가 없으므로 정적 자원 플래그만 본다.
    private bool SatisfiesResourceRequirement(GridCell cell, ResourceType requiredResourceNode) =>
        cell.HasResourceNode(requiredResourceNode) ||
        (ResearchUnlockQuery != null &&
         TryGetChunkCoord(cell.Coord, out Vector2Int chunkCoord) &&
         ResearchUnlockQuery.IsUnlocked(chunkCoord, requiredResourceNode));

    private bool AllCellsSatisfyResourceRequirement(List<GridCell> cells, ResourceType requiredResourceNode)
    {
        foreach (GridCell cell in cells)
        {
            if (!SatisfiesResourceRequirement(cell, requiredResourceNode))
                return false;
        }

        return true;
    }

    // 건물 타입에 따라 판정을 분기 - Factory(생산시설)는 자원 플래그 판정, 그 외는 기존 풋프린트 판정.
    // 신규 배치, 미리보기, 재배치가 항상 같은 기준을 쓰도록 통합한 진입점.
    public bool CanConstructBuildingFootprint(Vector3Int anchor, FootprintShape shape, Building building, Building ignoreBuilding) =>
        CanConstructBuildingFootprint(GetFootprintCoords(anchor, shape), building, ignoreBuilding);

    // 호출자가 이미 footprint 좌표를 계산해 둔 경우, 재계산 없이 그 결과를 그대로 검사한다.
    public bool CanConstructBuildingFootprint(List<Vector3Int> footprint, Building building, Building ignoreBuilding) =>
        building is BabyDragonTower
            ? CanConstructBabyDragonFootprint(footprint, ignoreBuilding)
            : building is Factory factory
            ? CanConstructResourceFootprint(footprint, factory.RequiredResourceNode, ignoreBuilding)
            : building is SealStone
                ? CanConstructSealStoneFootprint(footprint, ignoreBuilding)
                : CanConstructFootPrint(footprint, ignoreBuilding);

    // 배치가 실패한 뒤 "왜 실패했는지"만 되묻는 진단. CanConstructBuildingFootprint가 false를 준
    // 다음에만 부른다 - 가능/불가 판정은 여전히 그쪽이 담당하고, 여기 결과는 문구를 고르는 데만 쓴다.
    // 그래서 이 함수가 사유를 못 짚어 None을 돌려줘도(봉인석 영역 등 여기서 모르는 조건) 배치 동작은
    // 달라지지 않고 안내만 생략된다.
    // blockedCoord - 사유를 만든 칸. 호출부가 그 칸의 지형을 되물어 문구를 나누는 데 쓴다
    // (여기서 이미 찾아낸 칸을 돌려주지 않으면 호출부가 풋프린트를 한 번 더 훑게 된다).
    public PlacementBlockReason GetPlacementBlockReason(
        List<Vector3Int> footprint, Building building, Building ignoreBuilding, out Vector3Int blockedCoord)
    {
        // 새끼용은 비행 개체라 지형 건설 가능 여부를 무시한다(CanConstructBabyDragonFootprint와 같은 기준).
        // 이걸 빼면 용암 위에 새끼용을 놓을 때 "건설할 수 없는 땅"이라는 거짓 안내가 나간다.
        bool ignoresTerrain = building is BabyDragonTower;
        var reason = PlacementBlockReason.None;
        blockedCoord = default;

        foreach (Vector3Int coord in footprint)
        {
            PlacementBlockReason cellReason =
                GetCellBlockReason(coord, building, ignoreBuilding, ignoresTerrain);

            if (IsHigherPriority(cellReason, reason))
            {
                reason = cellReason;
                blockedCoord = coord;
            }
        }

        // 봉인석의 자리 조건은 "풋프린트 전체가 같은 포탈 영역"이라 칸 단위로 나눠 볼 수 없다
        // (CanConstructSealStoneFootprint와 같은 이유로 TryResolveOpenSite에 통째로 넘긴다).
        // 칸 단위 사유가 하나도 없을 때만 본다 - 지형·점유가 이미 막았다면 그쪽이 더 앞선 안내다.
        // 해금 여부(IsUnlocked)는 건설창이 슬롯을 비활성화해 막으므로 여기서 보지 않는다.
        if (reason == PlacementBlockReason.None &&
            building is SealStone &&
            (SealStonePlacementQuery == null ||
             !SealStonePlacementQuery.TryResolveOpenSite(footprint, out _)))
        {
            reason = PlacementBlockReason.NotSealSite;
        }

        return reason;
    }

    // PlacementBlockReason의 선언 순서가 곧 안내 우선순위다(앞설수록 높음).
    // None은 "막힌 게 없다"라서 항상 가장 낮다.
    private static bool IsHigherPriority(PlacementBlockReason candidate, PlacementBlockReason current)
    {
        if (candidate == PlacementBlockReason.None)
            return false;

        return current == PlacementBlockReason.None || candidate < current;
    }

    // 배치 가능 판정(CanConstructBuilding)과 실패 사유 진단이 함께 쓰는 단일 출처.
    // building을 넘기면 그 건물 고유의 조건(생산시설의 자원 노드)까지 본다.
    private PlacementBlockReason GetCellBlockReason(
        Vector3Int coord, Building building, Building ignoreBuilding, bool ignoresTerrain)
    {
        if (!_cells.TryGetValue(coord, out GridCell cell))
            return PlacementBlockReason.OutOfGrid;

        // 새끼용은 지형 건설 가능 여부를 무시하지만 길(Road)만은 막힌다
        // (CanConstructBabyDragonFootprint와 같은 기준). 이 예외를 빼면 미점령 청크의 길에서
        // "먼저 점령하라"고 안내하게 되는데, 점령해도 끝내 놓을 수 없는 자리라 헛수고를 시킨다.
        bool terrainBlocks = ignoresTerrain
            ? cell.TerrainType == TerrainType.Road
            : !IsCellConstructible(cell);

        if (terrainBlocks)
            return PlacementBlockReason.BlockedByTerrain;

        if (cell.ExistTypeOnCell != ExistTypeOnCell.None && cell.OccupantBuilding != ignoreBuilding)
            return PlacementBlockReason.Occupied;

        if (!IsChunkConquered(coord))
            return PlacementBlockReason.NotConquered;

        if (building is Factory factory && !SatisfiesResourceRequirement(cell, factory.RequiredResourceNode))
            return PlacementBlockReason.MissingResourceNode;

        return PlacementBlockReason.None;
    }

    // 새끼용은 비행 개체이므로 지형의 일반 건설 가능 여부를 무시한다.
    // 다만 플레이어에게 보이는 통행로(Road), 맵 밖 좌표, 다른 건물 점유, 미점령 청크는 제한한다.
    // 몬스터 스플라인은 실제 이동 중심선이라 화면의 넓은 길 타일과 일치하지 않을 수 있으므로
    // 새끼용 배치 금지 기준으로 사용하지 않는다. MonsterPathQuery는 임시 방벽 판정에서만 사용한다.
    private bool CanConstructBabyDragonFootprint(List<Vector3Int> footprint, Building ignoreBuilding)
    {
        foreach (Vector3Int coord in footprint)
        {
            if (!_cells.TryGetValue(coord, out GridCell cell))
                return false;

            if (cell.TerrainType == TerrainType.Road)
                return false;

            if (cell.ExistTypeOnCell != ExistTypeOnCell.None && cell.OccupantBuilding != ignoreBuilding)
                return false;

            if (!IsChunkConquered(coord))
                return false;
        }

        return true;
    }

    // 봉인석 전용 배치 판정 - CanConstructResourceFootprint와 동일한 구조(기본 풋프린트 게이트 위에
    // 건물별 추가 조건을 얹는다). 포탈 봉인 영역 소속 + 아직 그 포탈에 봉인석이 없음 + 연구 해금을 모두 요구한다.
    private bool CanConstructSealStoneFootprint(List<Vector3Int> footprint, Building ignoreBuilding)
    {
        if (!CanConstructFootPrint(footprint, ignoreBuilding)) // 지형 · 점유 · 청크 점령
            return false;

        if (SealStonePlacementQuery == null) // fail-closed - 영역을 모르는 채 배치를 허용하지 않는다
            return false;

        // "같은 포탈 영역 안" + "그 포탈에 아직 봉인석 없음"을 한 번에 판정 - 두 조건을 따로 조회하면
        // 서로 다른 좌표를 볼 수 있어(anchor 한 점만 보는 등) 판정이 어긋날 여지가 생긴다.
        if (!SealStonePlacementQuery.TryResolveOpenSite(footprint, out _))
            return false;

        return SealStonePlacementQuery.IsUnlocked;
    }

    public List<Vector3Int> GetOccupiedCoords(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out GridCell cell) || !cell.HasBuilding)
            return new List<Vector3Int>();

        List<GridCell> footprint = _buildingFootprintCells[cell.OccupantBuilding];
        var result = new List<Vector3Int>(footprint.Count);
        foreach (GridCell footprintCell in footprint)
            result.Add(footprintCell.Coord);
        return result;
    }

    // Building 인스턴스로 footprint 좌표를 조회한다 - GetFootprintYield(:762)와 동일한 인덱싱을
    // 좌표만 필요한 호출자를 위해 얇게 뽑아낸 버전. OnBuildingAdded(UnityEvent<Building>)는 좌표를
    // 넘기지 않으므로, 건설 직후 좌표가 필요한 구독자(PortalSealManager 등)가 이걸로 되짚어 조회한다.
    public IReadOnlyList<Vector3Int> GetFootprintCoords(Building building)
    {
        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> footprint))
            return Array.Empty<Vector3Int>();

        var coords = new List<Vector3Int>(footprint.Count);
        foreach (GridCell cell in footprint)
            coords.Add(cell.Coord);
        return coords;
    }

    // 생산시설의 실제 생산량 - 이미 배치된 건물용. 좌표를 뽑아 아래 오버로드에 넘기는 얇은 래퍼다.
    public int GetFootprintYield(Building building, ResourceType resourceType)
    {
        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> footprint) || footprint.Count == 0)
            return 0;

        return AccumulateFootprintYield(footprint, resourceType);
    }

    // 아직 배치되지 않은 풋프린트의 생산량 - 배치 미리보기 전용 진입점이다.
    // 좌표 목록만 받으므로 고스트처럼 Building 인스턴스가 없는 대상도 실제 정산과 같은 값을 얻는다.
    public int GetFootprintYield(IReadOnlyList<Vector3Int> footprint, ResourceType resourceType)
    {
        if (footprint == null || footprint.Count == 0)
            return 0;

        _footprintCellBuffer.Clear();

        foreach (Vector3Int coord in footprint)
        {
            if (_cells.TryGetValue(coord, out GridCell cell))
                _footprintCellBuffer.Add(cell);
        }

        return AccumulateFootprintYield(_footprintCellBuffer, resourceType);
    }

    // 청크별 셀 생산량 소계에 연구 배율을 적용한 뒤 합산한다.
    // 청크별로 반올림하므로 여러 청크에 걸친 footprint도 각 지역 연구 효과를 정확히 반영한다.
    private int AccumulateFootprintYield(IReadOnlyList<GridCell> footprint, ResourceType resourceType)
    {
        int total = 0;
        _chunkYieldBuffer.Clear();

        foreach (GridCell cell in footprint)
        {
            // 어느 청크에도 속하지 않은 셀은 지역 배율을 적용할 근거가 없으므로 배율 1로 바로 더한다.
            // 여기서 default(Vector2Int)를 버킷 키로 쓰면 (0,0) 청크의 배율이 엉뚱한 셀에 적용된다.
            if (!TryGetChunkCoord(cell.Coord, out Vector2Int chunkCoord))
            {
                total += cell.GetYield(resourceType);
                continue;
            }

            _chunkYieldBuffer.TryGetValue(chunkCoord, out int chunkSubtotal);
            _chunkYieldBuffer[chunkCoord] = chunkSubtotal + cell.GetYield(resourceType);
        }

        foreach (KeyValuePair<Vector2Int, int> entry in _chunkYieldBuffer)
        {
            float multiplier = YieldMultiplierQuery != null
                ? YieldMultiplierQuery.GetYieldMultiplier(entry.Key, resourceType)
                : 1f;

            total += Mathf.RoundToInt(entry.Value * multiplier);
        }

        return total;
    }

    // 점령 UI 리워드 패널 등 표시 전용 - 자원별 배율을 적용하지 않은 청크 전체의 원시 생산력 합계다.
    public int GetChunkBaseYield(Vector2Int chunkCoord) =>
        _chunkBaseYieldCache.TryGetValue(chunkCoord, out int total) ? total : 0;

    // 실제로 제거됐는지 반환한다 - 호출자가 이 값을 몰라도 되면 부작용(환불 등)을 잘못된 시점에 실행하기 쉽다
    // (예: 제거 불가 건물에도 건설 비용을 환불해버리는 실수).
    public bool RemoveBuilding(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out var cell) || !cell.HasBuilding)
            return false;

        Building building = cell.OccupantBuilding;

        if (!building.IsRemoveable)
            return false;

        OnBuildingRemoving?.Invoke(building);

        List<GridCell> footprint = _buildingFootprintCells[building];
        _buildingFootprintCells.Remove(building);

        foreach (GridCell footprintCell in footprint)
            footprintCell.RemoveBuilding();

        foreach (GridCell footprintCell in footprint)
            OnCellChanged?.Invoke(footprintCell);

        Debug.Log($"[GridMap] RemoveBuilding - 해제된 칸: {footprint.Count}");
        Destroy(building.gameObject);
        return true;
    }

    public bool MoveBuilding(Vector3Int prevCoord, Vector3Int nextCoord) =>
        MoveBuilding(prevCoord, nextCoord, GetBuildingAt(prevCoord)?.RotationSteps ?? 0);

    public bool MoveBuilding(Vector3Int prevCoord, Vector3Int nextCoord, int rotationSteps)
    {
        if (!_cells.TryGetValue(prevCoord, out GridCell cell) || !cell.HasBuilding)
            return false;

        Building building = cell.OccupantBuilding;

        if (!building.IsMoveable)
            return false;

        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> oldFootprint))
            return false;

        FootprintShape newShape = building.BaseFootprintShape.Rotated(rotationSteps);

        // TryGetFootprint가 기본 배치 가능 여부(CanConstruct·점유·점령)를 이미 전부 검사하므로 별도로 재검사하지 않는다.
        // Factory는 이미 확보한 셀 목록에 대해 자원 플래그 요구사항만 추가로 검사한다(좌표 재계산 없음).
        if (!TryGetFootprint(nextCoord, newShape, building, out List<GridCell> newFootprint))
            return false;

        if (building is Factory factory && !AllCellsSatisfyResourceRequirement(newFootprint, factory.RequiredResourceNode))
            return false;

        foreach (GridCell footprintCell in oldFootprint)
            footprintCell.RemoveBuilding();

        building.transform.position = GetFootprintCenterWorld(nextCoord, newShape)
            + building.ComputePlacementOffset(rotationSteps)
            + ComputeRotationCompensation(building.BaseFootprintShape, rotationSteps);
        building.SetRotation(rotationSteps);
        building.SetPlacementAnchor(nextCoord);
        building.SetDepthSortOrder(IsometricMath.ComputeDepthSortOrder(nextCoord));

        foreach (GridCell footprintCell in newFootprint)
            footprintCell.PlaceBuilding(building);

        _buildingFootprintCells[building] = newFootprint;

        foreach (GridCell footprintCell in oldFootprint)
            OnCellChanged?.Invoke(footprintCell);

        foreach (GridCell footprintCell in newFootprint)
            OnCellChanged?.Invoke(footprintCell);

        Debug.Log($"[GridMap] MoveBuilding - {prevCoord} -> {nextCoord}, 칸 수: {newFootprint.Count}");
        OnBuildingMoved?.Invoke(building);
        return true;
    }

}
