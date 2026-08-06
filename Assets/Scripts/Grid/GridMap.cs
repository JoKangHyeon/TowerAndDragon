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

    private Dictionary<Vector2Int, Vector3> _chunkCenterWorldCache = new();
    private Dictionary<Vector2Int, int> _chunkBaseYieldCache = new();

    private readonly Dictionary<Vector2Int, int> _chunkYieldBuffer = new();

    // 그리드 셀의 상태 변경 이벤트 - 건물 배치, 건물 파괴, 적 진입
    public UnityEvent<GridCell> OnCellChanged;

    // 청크 상태 변경 이벤트 - 점령/시야 확장 등 청크 단위 상태 전환 시에만 발생 (OnCellChanged보다 드묾)
    public UnityEvent OnChunkStateChanged;

    // 그리드에 건물이 등록되거나 제거되기 직전임을 외부 시스템에 알린다.
    // GridMap은 건물별 후속 처리 내용을 알지 않고 생명주기 시점만 전달한다.
    public UnityEvent<Building> OnBuildingAdded;
    public UnityEvent<Building> OnBuildingRemoving;

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
    private const int BASELINE_YIELD = 5;
    private const float DIAGONAL_BONUS_FACTOR = 0.4f;
    private const float DEGREES_PER_QUADRANT = 90f;
    private const float DEGREES_PER_DIAGONAL_STEP = 45f;

    // 청크 생성 다음 단계 - 셀마다 성으로부터의 거리·대각선 정도로 원시 생산량을 계산하고,
    // 그 셀이 보유한 자원노드에 한해 청크(바이옴) 단위 자원별 배율을 곱한다.
    // 거리는 셀의 정확한 좌표로 연속적으로 계산되므로 같은 청크 안에서도 셀마다 값이 자연히 달라진다.
    private void ApplyCellYields()
    {
        foreach (Chunk chunk in _chunks.Values)
        {
            int chunkBaseYieldSum = 0;

            foreach (GridCell cell in chunk.Cells)
            {
                int baseYield = CalculateDistanceYield(cell.Coord);
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

    private static int CalculateDistanceYield(Vector3Int cellCoord)
    {
        Vector2 offset = new Vector2(cellCoord.x, cellCoord.y) - CASTLE_POSITION;
        float distance = offset.magnitude;
        if (distance < Mathf.Epsilon)
            return BASELINE_YIELD;

        float angleFromCardinal = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, DEGREES_PER_QUADRANT));
        if (angleFromCardinal > DEGREES_PER_DIAGONAL_STEP)
            angleFromCardinal = DEGREES_PER_QUADRANT - angleFromCardinal;

        float diagonalFactor = angleFromCardinal / DEGREES_PER_DIAGONAL_STEP;
        int bonus = Mathf.RoundToInt(diagonalFactor * distance * DIAGONAL_BONUS_FACTOR);

        return BASELINE_YIELD + bonus;
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
        var grouped = new Dictionary<Vector2Int, List<GridCell>>();

        foreach (GridCell cell in _cells.Values)
        {
            Vector2Int chunkCoord = ToChunkCoord(cell.Coord);

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

        Debug.Log($"[GridMap] 청크 생성 완료 - 청크 개수: {_chunks.Count}");
    }

    private const int CHUNK_ORIGIN_OFFSET = ((Chunk.CHUNK_SIZE - 1) / 2);

    private static Vector2Int ToChunkCoord(Vector3Int cellCoord) => 
        new Vector2Int(
            FloorDiv(cellCoord.x + CHUNK_ORIGIN_OFFSET, Chunk.CHUNK_SIZE),
            FloorDiv(cellCoord.y + CHUNK_ORIGIN_OFFSET, Chunk.CHUNK_SIZE)
        );

    
    private static int FloorDiv(int value, int divisor) =>
       (value >= 0) ? value / divisor : (value - divisor + 1) / divisor;


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

    // 청크 전체를 스프라이트 한 장으로 표현하는 렌더러(FogCloudRenderer 등)가 참조 - 청크의 정중앙 셀 좌표를
    // 반환한다. ToChunkCoord(CHUNK_ORIGIN_OFFSET 기반)와 대응하는 역연산이며, CHUNK_SIZE가 홀수이므로
    // 정중앙 셀이 항상 정확히 존재한다.
    public Vector3Int GetChunkAnchorCell(Vector2Int chunkCoord) =>
        new Vector3Int(chunkCoord.x * Chunk.CHUNK_SIZE, chunkCoord.y * Chunk.CHUNK_SIZE, 0);

    // 지형상 건설 불가 셀이라도 해제 조회원이 허용하면 건설 가능으로 취급한다
    private bool IsCellConstructible(GridCell cell) =>
        cell.CanConstruct ||
        (ConstructionOverrideQuery != null &&
        ConstructionOverrideQuery.IsConstructionAllowed(cell.Coord, cell.TerrainType));

    public bool CanConstructBuilding(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) && 
        IsCellConstructible(cell) && 
        cell.ExistTypeOnCell == 
        ExistTypeOnCell.None &&
        IsChunkConquered (coord);

    public bool CanConstructBuilding(Vector3Int coord, Building ignoreBuilding) =>
        _cells.TryGetValue(coord, out var cell) && IsCellConstructible(cell) &&
        (cell.ExistTypeOnCell == ExistTypeOnCell.None || cell.OccupantBuilding == ignoreBuilding) &&
        IsChunkConquered(coord);

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

    public bool IsNatuallyConstructible (Vector3Int coord) =>
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

    public Chunk GetChunkAt(Vector3Int cellCoord)
    {
        Vector2Int chunkCoord = ToChunkCoord(cellCoord);
        return _chunks.TryGetValue(chunkCoord, out Chunk chunk) ? chunk : null;
    }

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

    public IEnumerable<Chunk> GetAdjacentChunks(Vector2Int chunkCoord)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                Vector2Int neighborCoord = chunkCoord + new Vector2Int(dx, dy);
                if (_chunks.TryGetValue(neighborCoord, out Chunk neighbor))
                    yield return neighbor;
            }
        }
    }

    // 점령 출격 가능 여부 판정 전용 - 상하좌우 4방향만 인접으로 취급한다 (시야 확장의 8방향 GetAdjacentChunks와는 별개).
    private static readonly Vector2Int[] ORTHOGONAL_CHUNK_DIRECTIONS =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    public IEnumerable<Chunk> GetOrthogonalAdjacentChunks(Vector2Int chunkCoord)
    {
        foreach (Vector2Int direction in ORTHOGONAL_CHUNK_DIRECTIONS)
        {
            Vector2Int neighborCoord = chunkCoord + direction;
            if (_chunks.TryGetValue(neighborCoord, out Chunk neighbor))
                yield return neighbor;
        }
    }

    public void ConstructBuilding(Building prefab, Vector3Int anchor, int rotationSteps)
    {
        if (prefab == null)
            return;

        FootprintShape rotatedShape = prefab.BaseFootprintShape.Rotated(rotationSteps);

        if (!TryGetFootprint(anchor, rotatedShape, out List<GridCell> footprint))
            return;

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
        building.SetDepthSortOrder(IsometricMath.ComputeDepthSortOrder(anchor));

        foreach (GridCell cell in footprint)
            cell.PlaceBuilding(building);

        _buildingFootprintCells[building] = footprint;

        foreach (GridCell cell in footprint)
            OnCellChanged?.Invoke(cell);

        OnBuildingAdded?.Invoke(building);
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
        building.SetDepthSortOrder(IsometricMath.ComputeDepthSortOrder(anchor));
        OnBuildingAdded?.Invoke(building);
        return true;
    }

    public bool TryGetFootprint(Vector3Int anchor, FootprintShape shape, out List<GridCell> footprint) =>
        TryGetFootprint(anchor, shape, null, out footprint);

    public bool TryGetFootprint(Vector3Int anchor, FootprintShape shape, Building ignoreBuilding, out List<GridCell> footprint)
    {
        footprint = new List<GridCell>();
        foreach (Vector3Int coord in GetFootprintCoords(anchor, shape))
        {
            if (!CanConstructBuilding(coord, ignoreBuilding) || !_cells.TryGetValue(coord, out GridCell cell))
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

    // 단일 셀이 자원 요건을 만족하는지 외부에서 조회할 수 있도록 공개한 버전(MouseSelectController 풋프린트 미리보기에서 셀별 색상 구분에 사용).
    public bool CellSatisfiesResourceRequirement(Vector3Int coord, ResourceType requiredResourceNode) =>
        _cells.TryGetValue(coord, out GridCell cell) && SatisfiesResourceRequirement(cell, requiredResourceNode);

    private bool SatisfiesResourceRequirement(GridCell cell, ResourceType requiredResourceNode) =>
        cell.HasResourceNode(requiredResourceNode) ||
        (ResearchUnlockQuery != null && ResearchUnlockQuery.IsUnlocked(ToChunkCoord(cell.Coord), requiredResourceNode));

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
        building is Factory factory
            ? CanConstructResourceFootprint(footprint, factory.RequiredResourceNode, ignoreBuilding)
            : building is SealStone
                ? CanConstructSealStoneFootprint(footprint, ignoreBuilding)
                : CanConstructFootPrint(footprint, ignoreBuilding);

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

    // 단일 셀이 봉인석 영역에 속하는지 외부에서 조회할 수 있도록 공개한 버전
    // (CellSatisfiesResourceRequirement와 같은 목적 - MouseSelectController 미리보기 셀별 색상 구분에 사용).
    public bool CellIsSealSite(Vector3Int coord) =>
        SealStonePlacementQuery != null && SealStonePlacementQuery.CellIsSealSite(coord);

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

    // 생산시설의 실제 생산량 - 청크별 셀 생산량 소계에 연구 배율을 적용한 뒤 합산한다.
    // 청크별로 반올림하므로 여러 청크에 걸친 footprint도 각 지역 연구 효과를 정확히 반영한다.
    public int GetFootprintYield(Building building, ResourceType resourceType)
    {
        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> footprint) || footprint.Count == 0)
            return 0;

        int total = 0;
        _chunkYieldBuffer.Clear();

        foreach (GridCell cell in footprint)
        {
            Vector2Int chunkCoord = ToChunkCoord(cell.Coord);
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
