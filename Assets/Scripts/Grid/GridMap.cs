using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

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


    public void ConstructBuilding<T>(Vector3Int anchor) where T : Building
    {
        T prefab = _buildingCatalog.GetPrefab<T>();
        if (prefab == null || !TryGetFootprint(anchor, prefab.CellSize, out List<GridCell> footprint))
            return;

        Vector3Int farCorner = anchor + new Vector3Int(prefab.CellSize - 1, prefab.CellSize - 1, 0);
        Vector3 worldPos = (ConvertGridToWorld(anchor) + ConvertGridToWorld(farCorner)) / 2f;
        Building building = Instantiate(
            prefab,
            worldPos + prefab.transform.localPosition,
            prefab.transform.rotation,
            transform);

        foreach (GridCell cell in footprint)
        {
            cell.PlaceBuilding(building);
            Debug.Log($"[GridMap] 셀 위치: {cell.Coord} - 셀에 건물 존재 여부: {cell.HasBuilding}");
            OnCellChanged?.Invoke(cell);
        }
    }

    // --- 키보드로 건물 생성 디버깅용 메서드 ---
    public bool TryGetRandomValidCoord<T>(out Vector3Int coord) where T : Building
    {
        coord = default;

        T prefab = _buildingCatalog.GetPrefab<T>();
        if (prefab == null)
            return false;

        var validAnchors = new List<Vector3Int>();
        foreach (Vector3Int anchor in _cells.Keys)
        {
            if (TryGetFootprint(anchor, prefab.CellSize, out _))
                validAnchors.Add(anchor);
        }

        if (validAnchors.Count == 0)
            return false;

        coord = validAnchors[UnityEngine.Random.Range(0, validAnchors.Count)];
        return true;
    }

    private bool TryGetFootprint(Vector3Int anchor, int cellSize, out List<GridCell> footprint)
    {
        footprint = new List<GridCell>();
        for (int x = 0; x < cellSize; x++)
        {
            for (int y = 0; y < cellSize; y++)
            {
                Vector3Int coord = anchor + new Vector3Int(x, y, 0);
                if (!CanConstructBuilding(coord) || !_cells.TryGetValue(coord, out GridCell cell))
                    return false;

                footprint.Add(cell);
            }
        }
        Debug.Log($"[GridMap] footprint 카운트: {footprint.Count}, 앵커 포스: {anchor}");
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
