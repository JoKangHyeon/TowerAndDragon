using UnityEngine;

public interface IConstructionOverrideQuery
{
    bool IsConstructionAllowed (Vector3Int coord, TerrainType terrain);
}