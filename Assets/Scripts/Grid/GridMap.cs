using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using Unity.Mathematics;

public class GridMap : MonoBehaviour
{
    [SerializeField]
    private Tilemap _tilemap;

    [SerializeField]
    private TerrainTileMap _terrainTileMap;

    [SerializeField]
    private BuildingCatalog _buildingCatalog;
    private Dictionary<Vector3Int, GridCell> _cells = new();
    
    public Action<GridCell> OnCellChanged; // 그리드 셀의 상태 변경 이벤트 - 건물 배치 / 건물 파괴 / 적 진입

    private void Awake()
    {
        GenerateGridFromTilemap();
        Debug.Log($"[GridMap] 셀 개수: {_cells.Count}");
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

            Debug.Log($"[GirdMap] {pos} 타일의 터레인타입: {terrain}");

            _cells[pos] = new GridCell(pos, terrain, canConstruct);

            Debug.Log($"[GridMap] 셀 현재 상태: {_cells[pos].CurrentState}, CanConstruct {_cells[pos].CanConstruct}");
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

    public ExistTypeOnCell ExamExist(Vector3Int coord) =>
        _cells.TryGetValue(coord, out var cell) ? cell.ExistTypeOnCell : ExistTypeOnCell.None;


    public void ConstructBuilding(Building prefab, Vector3Int anchor)
    {
        if (prefab == null || !TryGetFootprint(anchor, prefab.FootprintShape, out List<GridCell> footprint))
            return;

        Vector3 worldPos = GetFootprintCenterWorld(anchor, prefab.FootprintShape) + prefab.transform.localPosition;
        Building building = Instantiate(
            prefab,
            worldPos,
            prefab.transform.rotation,
            transform);

        foreach (GridCell cell in footprint)
        {
            cell.PlaceBuilding(building);
            Debug.Log($"[GridMap] 셀 위치: {cell.Coord} - 셀에 건물 존재 여부: {cell.HasBuilding}");
            OnCellChanged?.Invoke(cell);
        }
    }

    public void ConstructBuilding<T>(Vector3Int anchor) where T : Building
    {
        ConstructBuilding(_buildingCatalog.GetPrefab<T>(), anchor);
    }

    public bool TryGetFootprint(Vector3Int anchor, FootprintShape shape, out List<GridCell> footprint)
    {
        footprint = new List<GridCell>();
        foreach (Vector3Int coord in GetFootprintCoords(anchor, shape))
        {
            if (!CanConstructBuilding(coord) || !_cells.TryGetValue(coord, out GridCell cell))
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

    public bool CanConstructFootPrint(Vector3Int anchor, FootprintShape shape)
    {
        foreach (Vector3Int coord in GetFootprintCoords(anchor, shape))
        {
            if (!CanConstructBuilding(coord))
                return false;
        }

        return true;
    }

    public void RemoveBuilding(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out var cell) || !cell.HasBuilding)
            return;

        cell.RemoveBuilding();
        OnCellChanged?.Invoke(cell);
    }
}
