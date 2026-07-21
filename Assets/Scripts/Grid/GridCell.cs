using UnityEngine;

public class GridCell 
{
    public Vector3Int Coord { get; }
    public TerrainType TerrainType { get; }
    public ChunkState CurrentState { get; private set; }
    public bool CanConstruct { get; }

    // 이 셀에서 지을 수 있는 자원 생산시설의 종류(복수 플래그) - 터레인 기본값 + 수기 지정 영역이 누적된다.
    public ResourceType AvailableResourceNodes { get; private set; }

    private Building _occupantBuilding;
    public bool HasBuilding => _occupantBuilding != null;
    public Building OccupantBuilding => _occupantBuilding;

    public ExistTypeOnCell ExistTypeOnCell =>
        _occupantBuilding is Tower ? ExistTypeOnCell.Tower
        : _occupantBuilding is Castle ? ExistTypeOnCell.Castle
        : HasBuilding ? ExistTypeOnCell.Building
        : ExistTypeOnCell.None;

    public GridCell(Vector3Int coord, TerrainType terrainType, bool canConstruct)
    {
        Coord = coord;
        TerrainType = terrainType;
        CurrentState = ChunkState.Hidden;
        CanConstruct = canConstruct;
    }

    public void SetState(ChunkState newState) => CurrentState = newState;

    // 그리드 생성 시점(터레인 기본값)과 수기 지정 영역(Add 모드) 적용 시점에 각각 호출되어 누적(OR)된다.
    public void AddResourceNodes(ResourceType flags) => AvailableResourceNodes |= flags;

    // 수기 지정 영역(Override 모드) 전용 - 터레인 기본값을 포함해 기존 값을 전부 무시하고 지정한 값으로 교체한다.
    public void SetResourceNodes(ResourceType flags) => AvailableResourceNodes = flags;

    public bool HasResourceNode(ResourceType flag) => (AvailableResourceNodes & flag) != 0;

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
