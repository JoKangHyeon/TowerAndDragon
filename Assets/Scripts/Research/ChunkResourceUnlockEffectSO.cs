using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Chunk Resource Unlock",
    fileName = "ChunkResourceUnlockEffect")]
public sealed class ChunkResourceUnlockEffectSO : ResearchEffectSO
{
    [SerializeField] private TerrainType[] _targetTerrains;
    [SerializeField] private ResourceType _targetResources;

    public override bool UnlocksResourceNode(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        if (resourceType == ResourceType.None || (_targetResources & resourceType) != resourceType)
        {
            return false;
        }

        if (_targetTerrains == null)
        {
            return false;
        }

        foreach (TerrainType targetTerrain in _targetTerrains)
        {
            if (targetTerrain == terrainType)
            {
                return true;
            }
        }

        return false;
    }
}
