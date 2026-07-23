using UnityEngine;

public interface IChunkYieldMultiplierQuery
{
    float GetYieldMultiplier(Vector2Int chunkCoord, ResourceType resourceType);
}
