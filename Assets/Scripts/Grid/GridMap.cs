using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;

// --- 청크 디버깅용 ---
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;
#endif

public class GridMap : MonoBehaviour
{
    [SerializeField]
    private Tilemap _tilemap;

    [SerializeField]
    private TerrainTileMap _terrainTileMap;

    [SerializeField]
    private bool _showChunkGizmos = true;

// --- 청크 디버깅용 ---
#if UNITY_EDITOR
    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private SpriteRenderer _chunkOverlaySpritePrefab;

    private readonly List<SpriteRenderer> _chunkOverlayPool = new();
#endif

    // 전체 맵
    private Dictionary<Vector3Int, GridCell> _cells = new();

    // 건물이 차지하는 타일 맵
    private Dictionary<Building, List<GridCell>> _buildingFootprintCells = new();

    // 청크
    private Dictionary<Vector2Int, Chunk> _chunks = new();

    // 그리드 셀의 상태 변경 이벤트 - 건물 배치, 건물 파괴, 적 진입
    public Action<GridCell> OnCellChanged;

    private void Awake()
    {
        GenerateGridFromTilemap();
        GenerateChunks();

// --- 청크 디버깅용 ---
#if UNITY_EDITOR
        RefreshChunkOverlay();
#endif
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

            _cells[pos] = new GridCell(pos, terrain, canConstruct);
        }
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

    private static Vector2Int ToChunkCoord(Vector3Int cellCoord) => 
        new Vector2Int(
            FloorDiv(cellCoord.x, Chunk.CHUNK_SIZE),
            FloorDiv(cellCoord.y, Chunk.CHUNK_SIZE)
        );

    
    private static int FloorDiv(int value, int divisor) => 
        (int)Mathf.Floor((float)value / divisor);

    public State GetCellState(Vector3Int coord)
    {
        if (_cells.TryGetValue(coord, out var cell))
            return cell.CurrentState;

        return State.Unknown;
    }

    public Vector3 ConvertGridToWorld(Vector3Int cellCoord) => _tilemap.GetCellCenterWorld(cellCoord);
    public Vector3Int ConvertWorldToGrid(Vector3 worldCoord) => _tilemap.WorldToCell(worldCoord);
    public bool CanConstructBuilding(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) && cell.CanConstruct && cell.ExistTypeOnCell == ExistTypeOnCell.None;

    public bool CanConstructBuilding(Vector3Int coord, Building ignoreBuilding) =>
        _cells.TryGetValue(coord, out var cell) && cell.CanConstruct &&
        (cell.ExistTypeOnCell == ExistTypeOnCell.None || cell.OccupantBuilding == ignoreBuilding);

    public ExistTypeOnCell ExamExist(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.ExistTypeOnCell : ExistTypeOnCell.None;

    public Building GetBuildingAt(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.OccupantBuilding : null;

     public List<Vector3Int> GetAllOccupiedCoords() =>
        _cells.Values.Where(cell => cell.HasBuilding).Select(cell => cell.Coord).ToList();

    public Chunk GetChunkAt(Vector3Int cellCoord)
    {
        Vector2Int chunkCoord = ToChunkCoord(cellCoord);
        return _chunks.TryGetValue(chunkCoord, out Chunk chunk) ? chunk : null;
    }

    public void SetChunkState(Vector3Int cellCoord, State newState)
    {
        Chunk chunk = GetChunkAt(cellCoord);
        if (chunk == null) 
            return;
        
        chunk.SetState(newState);

        foreach (GridCell cell in chunk.Cells)
        {
            OnCellChanged?.Invoke(cell);
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

    public bool CanConstructFootPrint(Vector3Int anchor, FootprintShape shape, Building ignoreBuilding)
    {
        foreach (Vector3Int coord in GetFootprintCoords(anchor, shape))
        {
            if (!CanConstructBuilding(coord, ignoreBuilding))
                return false;
        }

        return true;
    }

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

        if (!_buildingFootprintCells.TryGetValue(building, out List<GridCell> oldFootprint))
            return false;

        if (!TryGetFootprint(nextCoord, building.FootprintShape, building, out List<GridCell> newFootprint))
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

// --- 청크 디버깅용 (에디터 전용, 빌드 미포함) ---
// 1. 그룹핑/상태 육안 확인: _showChunkGizmos 켜고 Play 모드 진입
//    - Scene 뷰: 청크별 색이 다른 스프라이트로 그룹핑 확인, 초록/빨강/회색 구슬로 셀 상태(Active/Inactive/Unknown) 확인
//    - 청크 라벨에 "청크좌표 (셀개수) - 상태" 표시
// 2. 셀/청크 정보 확인: Play 모드에서 타일 좌클릭
//    - 인스펙터의 _debugSelectedCellCoord/State, _debugSelectedChunkCoord/State에 반영, 콘솔에도 로그
// 3. 청크->셀 상태 전파 테스트: 위 2번으로 셀을 먼저 선택한 뒤,
//    GridMap 컴포넌트 우클릭 > "디버그 - 선택 청크 상태 사이클 (청크 -> 셀)" 실행
//    - 선택된 청크 상태가 Unknown -> Inactive -> Active 순으로 바뀌며 소속 셀 전체 반영 여부를 PASS/FAIL로 로그
#if UNITY_EDITOR
    // 개발용 디버그 기즈모 - 청크별 소속 셀 상태를 색깔 있는 구슬로 표시 (그룹핑 자체는 RefreshChunkOverlay 스프라이트로 확인)
    private void OnDrawGizmos()
    {
        if (!_showChunkGizmos || _tilemap == null)
            return;

        float yOffset = GetOverlayYOffset();

        foreach (Chunk chunk in _chunks.Values)
        {
            foreach (GridCell cell in chunk.Cells)
            {
                Vector3 worldPos = _tilemap.GetCellCenterWorld(cell.Coord);
                worldPos.y += yOffset;

                Gizmos.color = GetStateGizmoColor(cell.CurrentState);
                Gizmos.DrawWireSphere(worldPos, CHUNK_GIZMO_STATE_RADIUS);
            }

            if (chunk.Cells.Count > 0)
            {
                Vector3 labelPos = _tilemap.GetCellCenterWorld(chunk.Cells[0].Coord);
                labelPos.y += yOffset;
                Handles.Label(labelPos, $"{chunk.ChunkCoord} ({chunk.Cells.Count}) - {chunk.CurrentState}");
            }
        }
    }

    private float GetOverlayYOffset() => _mouseSelectController != null ? _mouseSelectController.YOffset : 0f;

    // 마우스 셀렉트와 동일한 스프라이트 풀링 방식으로, 실제 렌더링되는 타일 위치(Y 오프셋 반영)에 맞춰
    // 청크별로 다른 색 스프라이트를 깔아 그룹핑을 확인
    private void RefreshChunkOverlay()
    {
        if (_chunkOverlaySpritePrefab == null)
            return;

        float yOffset = GetOverlayYOffset();
        int index = 0;

        foreach (Chunk chunk in _chunks.Values)
        {
            Color color = GetChunkGizmoColor(chunk.ChunkCoord);

            foreach (GridCell cell in chunk.Cells)
            {
                SpriteRenderer overlay = GetPooledChunkOverlay(index);
                Vector3 worldPos = _tilemap.GetCellCenterWorld(cell.Coord);
                worldPos.y += yOffset;
                overlay.transform.position = worldPos;
                overlay.color = color;
                index++;
            }
        }

        for (int i = index; i < _chunkOverlayPool.Count; i++)
        {
            _chunkOverlayPool[i].gameObject.SetActive(false);
        }
    }

    private SpriteRenderer GetPooledChunkOverlay(int index)
    {
        if (index >= _chunkOverlayPool.Count)
        {
            _chunkOverlayPool.Add(Instantiate(_chunkOverlaySpritePrefab, transform));
        }

        SpriteRenderer overlay = _chunkOverlayPool[index];
        overlay.gameObject.SetActive(_showChunkGizmos);
        return overlay;
    }

    private const int CHUNK_GIZMO_HASH_PRIME_X = 92821;
    private const int CHUNK_GIZMO_HASH_PRIME_Y = 68917;
    private const int CHUNK_GIZMO_HUE_STEPS = 360;
    private const float CHUNK_GIZMO_SATURATION = 1f;
    private const float CHUNK_GIZMO_VALUE = 1f;
    private const float CHUNK_GIZMO_ALPHA = 0.85f;
    private const float CHUNK_GIZMO_STATE_RADIUS = 0.15f;

    private static Color GetChunkGizmoColor(Vector2Int chunkCoord)
    {
        int hash = chunkCoord.x * CHUNK_GIZMO_HASH_PRIME_X + chunkCoord.y * CHUNK_GIZMO_HASH_PRIME_Y;
        float hue = Mathf.Abs(hash % CHUNK_GIZMO_HUE_STEPS) / (float)CHUNK_GIZMO_HUE_STEPS;
        Color color = Color.HSVToRGB(hue, CHUNK_GIZMO_SATURATION, CHUNK_GIZMO_VALUE);
        color.a = CHUNK_GIZMO_ALPHA;
        return color;
    }

    // Active=초록, Inactive=빨강, Unknown=회색 작은 구슬로 셀 상태 표시 (청크 상태 변경 시 한번에 바뀌는지 확인용)
    private static Color GetStateGizmoColor(State state) => state switch
    {
        State.Active => Color.green,
        State.Inactive => Color.red,
        State.Unknown => Color.gray,
        _ => Color.white,
    };

    [Header("디버그 - 마우스로 선택한 셀/청크 정보")]
    [SerializeField]
    private Vector3Int _debugSelectedCellCoord;

    [SerializeField]
    private State _debugSelectedCellState;

    [SerializeField]
    private Vector2Int _debugSelectedChunkCoord;

    [SerializeField]
    private State _debugSelectedChunkState;

    // Play 모드에서 마우스 좌클릭한 셀의 상태와 소속 청크 상태를 인스펙터/콘솔로 확인
    private void Update()
    {
        if (!Application.isPlaying || _mouseSelectController == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector3Int coord = _mouseSelectController.GetHoveredCell();
        if (!_cells.TryGetValue(coord, out GridCell cell))
            return;

        Chunk chunk = GetChunkAt(coord);

        _debugSelectedCellCoord = coord;
        _debugSelectedCellState = cell.CurrentState;
        _debugSelectedChunkCoord = chunk != null ? chunk.ChunkCoord : default;
        _debugSelectedChunkState = chunk != null ? chunk.CurrentState : State.Unknown;

        Debug.Log($"[GridMap] 선택 셀 {coord} - 셀 상태: {_debugSelectedCellState}, " +
            $"소속 청크: {_debugSelectedChunkCoord} - 청크 상태: {_debugSelectedChunkState}");
    }

    // 테스트용 - 마우스로 셀을 먼저 선택한 뒤 실행. 선택된 셀이 속한 청크의 상태를
    // Unknown -> Inactive -> Active 순으로 사이클하며, 소속 셀 전체에 한번에 반영되는지(청크->셀) 확인
    [ContextMenu("디버그 - 선택 청크 상태 사이클 (청크 -> 셀)")]
    private void DebugCycleSelectedChunkState()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[GridMap] 테스트 실패 - Play 모드에서만 실행 가능");
            return;
        }

        Chunk chunk = GetChunkAt(_debugSelectedCellCoord);
        if (chunk == null)
        {
            Debug.Log($"[GridMap] 테스트 실패 - {_debugSelectedCellCoord} 위치에 청크 없음 (먼저 마우스로 타일을 클릭하세요)");
            return;
        }

        State prevState = chunk.CurrentState;
        State nextState = GetNextDebugState(prevState);
        SetChunkState(_debugSelectedCellCoord, nextState);

        bool allCellsMatch = chunk.Cells.All(cell => cell.CurrentState == nextState);

        _debugSelectedCellState = chunk.Cells.First(cell => cell.Coord == _debugSelectedCellCoord).CurrentState;
        _debugSelectedChunkState = chunk.CurrentState;

        Debug.Log($"[GridMap] 청크 {chunk.ChunkCoord} 상태 {prevState} -> {nextState}, " +
            $"소속 셀 {chunk.Cells.Count}개 전체 반영: {(allCellsMatch ? "PASS" : "FAIL")}");
    }

    private static State GetNextDebugState(State current) => current switch
    {
        State.Unknown => State.Inactive,
        State.Inactive => State.Active,
        State.Active => State.Unknown,
        _ => State.Unknown,
    };
#endif
}
