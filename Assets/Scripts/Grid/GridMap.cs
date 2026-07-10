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

    private Dictionary<Vector3Int, GridCell> _cells = new();
    private Dictionary<Building, List<GridCell>> _buildingFootprintCells = new();

    // 그리드 셀의 상태 변경 이벤트 - 건물 배치, 건물 파괴, 적 진입
    public Action<GridCell> OnCellChanged;

    private void Awake()
    {
        GenerateGridFromTilemap();
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
}
