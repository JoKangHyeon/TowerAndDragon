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
            Debug.Log($"[GirdMap] {pos} 타일의 터레인타입: {terrain}");
            _cells[pos] = new GridCell(pos, terrain);
            Debug.Log($"[GridMap] 셀 현재 상태: {_cells[pos].CurrentState}");
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

    public void ConstructBuilding(Vector3Int coord)
    {
        if (!CanConstructBuilding(coord))
            return;

        // TODO: 건물 배치 시스템(어떤 Building을 생성할지) 확정 후 실제 배치 로직 구현
    }

    public void RemoveBuilding(Vector3Int coord)
    {
        if (!_cells.TryGetValue(coord, out var cell) || !cell.HasBuilding)
            return;

        cell.RemoveBuilding();
        OnCellChanged?.Invoke(cell);
    }
}
