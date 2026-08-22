using System.Collections.Generic;
using UnityEngine;

// 연구·용 스킬트리의 타워 최대체력 기여를 하나의 소비 지점(TowerMaxHealthApplier)으로 합성한다.
// 각 소스가 이미 "1 + Σ보너스"를 반환하므로 소스 사이는 곱으로 합성한다
// (TowerStatMultiplierComposite와 같은 관례).
public sealed class TowerMaxHealthMultiplierComposite : MonoBehaviour, ITowerMaxHealthMultiplierQuery
{
    private readonly RefCountedSourceSet<ITowerMaxHealthMultiplierQuery> _sources = new();

    public void Register(ITowerMaxHealthMultiplierQuery source)
    {
        _sources.Register(source);
    }

    public void Unregister(ITowerMaxHealthMultiplierQuery source)
    {
        _sources.Unregister(source);
    }

    public float GetMaxHealthMultiplier(TowerData towerData)
    {
        float multiplier = 1f;
        IReadOnlyList<ITowerMaxHealthMultiplierQuery> sources = _sources.Sources;

        for (int i = 0; i < sources.Count; i++)
        {
            multiplier *= sources[i].GetMaxHealthMultiplier(towerData);
        }

        return multiplier;
    }
}
