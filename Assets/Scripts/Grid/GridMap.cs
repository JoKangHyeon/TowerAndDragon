using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;

public class GridMap : MonoBehaviour
{
    [SerializeField]
    private Tilemap _tilemap;

    [SerializeField]
    private TerrainTileMap _terrainTileMap;

    [SerializeField]
    private ResourceNodeAreaTable _resourceNodeAreaTable;

    // 청크 단위 연구 해금 조회 - 연구 시스템이 아직 없는 씬에서는 null로 두면 항상 터레인 기본값만으로 판정된다.
    public IChunkResearchUnlockQuery ResearchUnlockQuery { get; set; }

    // 전체 맵
    private Dictionary<Vector3Int, GridCell> _cells = new();

    // 건물이 차지하는 타일 맵
    private Dictionary<Building, List<GridCell>> _buildingFootprintCells = new();

    // 청크
    private Dictionary<Vector2Int, Chunk> _chunks = new();

    // 그리드 셀의 상태 변경 이벤트 - 건물 배치, 건물 파괴, 적 진입
    public event Action<GridCell> OnCellChanged;

    // 청크 상태 변경 이벤트 - 점령/시야 확장 등 청크 단위 상태 전환 시에만 발생 (OnCellChanged보다 드묾)
    public event Action OnChunkStateChanged;

    // 그리드에 건물이 등록되거나 제거되기 직전임을 외부 시스템에 알린다.
    // GridMap은 건물별 후속 처리 내용을 알지 않고 생명주기 시점만 전달한다.
    public event Action<Building> OnBuildingAdded;
    public event Action<Building> OnBuildingRemoving;

    private void Awake()
    {
        GenerateGridFromTilemap();
        GenerateChunks();
        ApplyResourceNodeAreas();
    }

    private void GenerateGridFromTilemap()
    {
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
        }

        Debug.Log($"[GridMap] 그리드맵 생성 완료 - 셀의 개수: {_cells.Count}");
    }

    // 터레인 기본값(GenerateGridFromTilemap) 다음 단계 - 수기 지정 영역을 추가로 누적 적용한다.
    private void ApplyResourceNodeAreas()
    {
        if (_resourceNodeAreaTable != null)
            _resourceNodeAreaTable.ApplyToGrid(_cells);
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

    public Vector3Int ConvertWorldToGrid(Vector3 worldCoord) => _tilemap.WorldToCell(worldCoord);

    // 지형 타일에 심어둔 고저차(Y 오프셋)를 읽어온다 - Isometric Z As Y 레이아웃에서 셀의 Z좌표는
    // 정렬용으로만 쓰이고 높이는 SetTransformMatrix로 부여한 타일별 렌더 오프셋으로 표현된다.
    public float GetHeightOffset(Vector3Int cellCoord) => _tilemap.GetTransformMatrix(cellCoord).GetColumn(3).y;

    // 전장의 안개(FogOfWarRenderer)가 지형 타일 자체를 SetColor로 어둡게 틴트하기 위해 참조한다.
    public Tilemap TerrainTilemap => _tilemap;
    public bool CanConstructBuilding(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) && cell.CanConstruct && cell.ExistTypeOnCell == ExistTypeOnCell.None &&
        IsChunkConquered(coord);

    public bool CanConstructBuilding(Vector3Int coord, Building ignoreBuilding) =>
        _cells.TryGetValue(coord, out var cell) && cell.CanConstruct &&
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

    public Building GetBuildingAt(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.OccupantBuilding : null;

     public List<Vector3Int> GetAllOccupiedCoords() =>
        _cells.Values.Where(cell => cell.HasBuilding).Select(cell => cell.Coord).ToList();

    public Chunk GetChunkAt(Vector3Int cellCoord)
    {
        Vector2Int chunkCoord = ToChunkCoord(cellCoord);
        return _chunks.TryGetValue(chunkCoord, out Chunk chunk) ? chunk : null;
    }

    public IEnumerable<Chunk> GetAllChunks() => _chunks.Values;

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

    public void SetChunkState(Vector3Int cellCoord, ChunkState newState)
    {
        Chunk chunk = GetChunkAt(cellCoord);
        if (chunk != null)
            SetChunkState(chunk.ChunkCoord, newState);
    }

    public Chunk GetChunk(Vector2Int chunkCoord) =>
        _chunks.TryGetValue(chunkCoord, out Chunk chunk) ? chunk : null;

    public Vector3 GetChunkCenterWorld(Vector2Int chunkCoord)
    {
        Chunk chunk = GetChunk(chunkCoord);
        if (chunk == null || chunk.Cells.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (GridCell cell in chunk.Cells)
            sum += ConvertGridToWorld(cell.Coord);

        return sum / chunk.Cells.Count;
    }

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

    public void ConstructBuilding(Building prefab, Vector3Int anchor)
    {
        if (prefab == null || !TryGetFootprint(anchor, prefab.FootprintShape, out List<GridCell> footprint))
            return;

        Vector3 offset = prefab.transform.localPosition;
        Vector3 worldPos = GetFootprintCenterWorld(anchor, prefab.FootprintShape) + offset;
        Building building = Instantiate(
            prefab,
            worldPos,
            prefab.transform.rotation,
            transform);
        building.SetPlacementOffset(offset);

        foreach (GridCell cell in footprint)
        {
            cell.PlaceBuilding(building);
            OnCellChanged?.Invoke(cell);
        }

        _buildingFootprintCells[building] = footprint;
        OnBuildingAdded?.Invoke(building);
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
            : CanConstructFootPrint(footprint, ignoreBuilding);

    public List<Vector3Int> GetOccupiedCoords(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out GridCell cell) || !cell.HasBuilding)
            return new List<Vector3Int>();

        return _buildingFootprintCells[cell.OccupantBuilding]
            .Select(footprintCell => footprintCell.Coord)
            .ToList();
    }

    public void RemoveBuilding(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out var cell) || !cell.HasBuilding)
            return;

        Building building = cell.OccupantBuilding;

        if (!building.IsRemoveable)
            return;

        OnBuildingRemoving?.Invoke(building);

        List<GridCell> footprint = _buildingFootprintCells[building];

        foreach (GridCell footprintCell in footprint)
        {
            footprintCell.RemoveBuilding();
            OnCellChanged?.Invoke(footprintCell);
        }

        Debug.Log($"[GridMap] RemoveBuilding - 해제된 칸: {footprint.Count}, 건물의 실제 footprint 칸: {building.FootprintShape.GetOccupiedOffsets().Count()}");
        _buildingFootprintCells.Remove(building);
        Destroy(building.gameObject);
    }

    public bool MoveBuilding(Vector3Int prevCoord, Vector3Int nextCoord)
    {
        if (!_cells.TryGetValue(prevCoord, out GridCell cell) || !cell.HasBuilding)
            return false;

        Building building = cell.OccupantBuilding;

        if (!building.IsMoveable)
            return false;

        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> oldFootprint))
            return false;

        // TryGetFootprint가 기본 배치 가능 여부(CanConstruct·점유·점령)를 이미 전부 검사하므로 별도로 재검사하지 않는다.
        // Factory는 이미 확보한 셀 목록에 대해 자원 플래그 요구사항만 추가로 검사한다(좌표 재계산 없음).
        if (!TryGetFootprint(nextCoord, building.FootprintShape, building, out List<GridCell> newFootprint))
            return false;

        if (building is Factory factory && !AllCellsSatisfyResourceRequirement(newFootprint, factory.RequiredResourceNode))
            return false;

        foreach (GridCell footprintCell in oldFootprint)
        {
            footprintCell.RemoveBuilding();
            OnCellChanged?.Invoke(footprintCell);
        }

        building.transform.position = GetFootprintCenterWorld(nextCoord, building.FootprintShape) + building.PlacementOffset;

        foreach (GridCell footprintCell in newFootprint)
        {
            footprintCell.PlaceBuilding(building);
            OnCellChanged?.Invoke(footprintCell);
        }

        Debug.Log($"[GridMap] MoveBuilding - {prevCoord} -> {nextCoord}, 칸 수: {newFootprint.Count}");
        _buildingFootprintCells[building] = newFootprint;
        return true;
    }

}
