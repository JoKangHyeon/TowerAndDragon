using UnityEngine;

public abstract class ResearchEffectSO : ScriptableObject
{
    public virtual float GetYieldMultiplierBonus(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        return 0f;
    }
}
