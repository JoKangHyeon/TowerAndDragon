using System.Collections.Generic;
using UnityEngine;

public class GridCell
{
    public Vector3Int Coord { get; }
    public TerrainType TerrainType { get; }
    public ChunkState CurrentState { get; private set; }
    public bool CanConstruct { get; }

    // 물은 맵 경계 장식이라 어떤 해금(연구·새끼용)으로도 건설을 허용하지 않는다.
    public bool IsWater => TerrainType == TerrainType.Water;

    // 이 셀에서 지을 수 있는 자원 생산시설의 종류(복수 플래그) - 터레인 기본값 + 수기 지정 영역이 누적된다.
    public ResourceType AvailableResourceNodes { get; private set; }

    // 디버그 오버레이/로그 전용 원시 지형 생산력 - 자원 종류와 무관하며 실제 정산에는 쓰이지 않는다.
    public int BaseYield { get; private set; }

    // 실제 정산에 쓰이는 값 - 이 셀이 보유한 자원노드(AvailableResourceNodes)에 해당하는 자원만 등록된다.
    private readonly Dictionary<ResourceType, int> _yields = new();

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

    public void SetBaseYield(int amount) => BaseYield = amount;

    public void SetYield(ResourceType resourceType, int yield) => _yields[resourceType] = yield;

    // 등록되지 않은 자원(= 이 셀에 없는 자원노드)은 0을 반환한다.
    public int GetYield(ResourceType resourceType) => _yields.TryGetValue(resourceType, out int yield) ? yield : 0;

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
