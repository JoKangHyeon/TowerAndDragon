using UnityEngine;

public class GridCell 
{
    public Vector3Int Coord { get; }
    public TerrainType TerrainType { get; }
    public ChunkState CurrentState { get; private set; }
    public bool CanConstruct { get; }
    public bool CanFarmField { get; }

    private Building _occupantBuilding;
    public bool HasBuilding => _occupantBuilding != null;
    public Building OccupantBuilding => _occupantBuilding;

    public ExistTypeOnCell ExistTypeOnCell =>
        _occupantBuilding is Tower ? ExistTypeOnCell.Tower
        : _occupantBuilding is FarmField ? ExistTypeOnCell.FarmField
        : _occupantBuilding is Castle ? ExistTypeOnCell.Castle
        : HasBuilding ? ExistTypeOnCell.Building
        : ExistTypeOnCell.None;

    public GridCell(Vector3Int coord, TerrainType terrainType, bool canConstruct)
    {
        Coord = coord;
        TerrainType = terrainType;
        CurrentState = ChunkState.Hidden;
        CanConstruct = canConstruct;
        CanFarmField = terrainType == TerrainType.Grass;
    }

    public void SetState(ChunkState newState) => CurrentState = newState;

    public bool PlaceBuilding(Building building)
    {
        if (HasBuilding)
            return false;

        _occupantBuilding = building;
        return true;
    }

    public void RemoveBuilding() 
    {
        Debug.Log($"[GridCell] RemoveBuilding - {Coord} 타일 삭제");
        _occupantBuilding = null;
    }
}
