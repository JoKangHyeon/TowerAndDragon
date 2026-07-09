using System;
using UnityEngine;


public class Building {}
public class Enemy {}
public enum ExistType 
{
     Building,
     Enemy,
     FarmField,
     Tower,
     None
 }

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
    private Enemy _enemyOnCell;
    public bool HasBuilding => _occupantBuilding != null;
    public bool HasEnemy => _enemyOnCell != null;
    public GridCell(Vector3Int coord, TerrainType terrainType)
    {
        Coord = coord;
        TerrainType = terrainType;
        CurrentState = State.Unknown;
        CanConstruct = terrainType != TerrainType.Volcano;
        CanFarmField = terrainType == TerrainType.Grass;
    }

    public void SetState(State newState) => CurrentState = newState;
}
