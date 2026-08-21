using System.Collections.Generic;
using UnityEngine;

// ChunkYieldMultiplierComposite와 동일한 목적 - 지역 페널티를 완화하는 여러 소스(새끼용, 추후
// 연구 등)의 기여를 TerrainPenaltySystem의 슬롯 하나로 합성한다. 소스 간 곱연산이므로
// 어느 한 소스가 0(무효화)을 내면 다른 소스와 무관하게 페널티가 사라진다.
public sealed class TerrainPenaltyScaleComposite : MonoBehaviour, ITerrainPenaltyScaleQuery
{
    private readonly RefCountedSourceSet<ITerrainPenaltyScaleQuery> _sources = new();

    public void Register(ITerrainPenaltyScaleQuery source)
    {
        _sources.Register(source);
    }

    public void Unregister(ITerrainPenaltyScaleQuery source)
    {
        _sources.Unregister(source);
    }

    public float GetPenaltyScale(Vector3 worldPosition, TerrainType terrain, TerrainPenaltyKind kind)
    {
        float scale = 1f;
        IReadOnlyList<ITerrainPenaltyScaleQuery> sources = _sources.Sources;

        for (int i = 0; i < sources.Count; i++)
        {
            scale *= sources[i].GetPenaltyScale(worldPosition, terrain, kind);
        }

        return scale;
    }
}
