using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class GridMap : MonoBehaviour
{
    [SerializeField]
    private Tilemap _tilemap;
    private Dictionary<Vector3Int, GridCell> _cells = new();

    
    public Action OnCellChanged; // 그리드 셀의 상태 변경 이벤트 - 건물 배치 / 건물 파괴 / 적 진입

    private void GenerateGridFromTilemap()
    {
        foreach (var pos in _tilemap.cellBounds.allPositionsWithin)
        {
            if (!_tilemap.HasTile(pos)) 
                continue;

            TileBase tile = _tilemap.GetTile(pos);
            //TerrainType terrain = ResolveTerrainType(tile);
            //_cells[pos] = new GridCell(pos, terrain);
        }
    }
    public State GetCellState(Vector3Int coord)
    {
        if (_cells.TryGetValue(coord, out var cell))
            return cell.CurrentState;
        
        return State.Unknown;
    }
    public void ConvertGridToWorld(Vector3Int cellCoord) => _tilemap.GetCellCenterWorld(cellCoord);
    public void ConvertWorldToGrid(Vector3 worldCoord) => _tilemap.WorldToCell(worldCoord);

    public void CanConstructBuilding(GridCell cell)
    {

    }

    public void ExamExistType(GridCell cell)
    {

    }

    public void ConstructBuilding(GridCell cell)
    {
        
    }

    public void RemoveBuilding(GridCell cell)
    {

    }
}
