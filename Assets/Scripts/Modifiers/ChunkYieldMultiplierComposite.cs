using System.Collections.Generic;
using UnityEngine;

// TowerStatMultiplierComposite와 동일한 목적 - 청크 생산 배율에 기여하는 소스(연구·용 스킬트리)를
// GridMap.YieldMultiplierQuery 슬롯 하나로 합성한다. 소스 간 곱연산.
public sealed class ChunkYieldMultiplierComposite : MonoBehaviour, IChunkYieldMultiplierQuery
{
    private readonly List<IChunkYieldMultiplierQuery> _sources = new();

    public void Register(IChunkYieldMultiplierQuery source)
    {
        if (source != null && !_sources.Contains(source))
        {
            _sources.Add(source);
        }
    }

    public void Unregister(IChunkYieldMultiplierQuery source)
    {
        _sources.Remove(source);
    }

    public float GetYieldMultiplier(Vector2Int chunkCoord, ResourceType resourceType)
    {
        float multiplier = 1f;

        foreach (IChunkYieldMultiplierQuery source in _sources)
        {
            multiplier *= source.GetYieldMultiplier(chunkCoord, resourceType);
        }

        return multiplier;
    }
}
