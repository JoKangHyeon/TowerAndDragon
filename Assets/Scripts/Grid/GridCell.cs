using UnityEngine;

public enum State
{
        Active, // 점령 = 활성화
        Inactive, // 미점령 & 가시 범위 안에 존재
        Unknown // 미점령 & 가시 범위 밖에 존재
}

public class GridCell 
{
    public Vector3Int Coord { get; }
    public TerrainType TerrainType { get; }
    public State CurrentState { get; private set; }
    public bool CanConstruct { get; }
    public bool CanFarmField { get; }

    private Building _occupantBuilding;
    public bool HasBuilding => _occupantBuilding != null;
    public Building OccupantBuilding => _occupantBuilding;

    public ExistTypeOnCell ExistTypeOnCell =>
        _occupantBuilding is Tower ? ExistTypeOnCell.Tower
        : _occupantBuilding is FarmField ? ExistTypeOnCell.FarmField
        : HasBuilding ? ExistTypeOnCell.Building
        : ExistTypeOnCell.None;

    public GridCell(Vector3Int coord, TerrainType terrainType, bool canConstruct)
    {
        Coord = coord;
        TerrainType = terrainType;
        CurrentState = State.Unknown;
        CanConstruct = canConstruct;
        CanFarmField = terrainType == TerrainType.Grass;
    }

    public void SetState(State newState) => CurrentState = newState;

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
