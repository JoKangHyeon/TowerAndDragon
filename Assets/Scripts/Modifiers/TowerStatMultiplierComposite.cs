using System.Collections.Generic;
using UnityEngine;

// 연구·용 스킬트리 등 여러 시스템의 타워 배율 기여를 하나의 소비 지점(TowerAttack)으로 합성한다.
// 각 소스는 이미 "1 + Σ보너스" 형태의 완성된 배율을 반환하므로, 소스 사이는 곱으로 합성한다
// (BabyDragonBuffSystem·연구 관례와 동일한 곱연산 합성).
public sealed class TowerStatMultiplierComposite : MonoBehaviour, ITowerStatMultiplierQuery
{
    private readonly List<ITowerStatMultiplierQuery> _sources = new();

    public void Register(ITowerStatMultiplierQuery source)
    {
        if (source != null && !_sources.Contains(source))
        {
            _sources.Add(source);
        }
    }

    public void Unregister(ITowerStatMultiplierQuery source)
    {
        _sources.Remove(source);
    }

    public float GetDamageMultiplier(TowerData towerData)
    {
        float multiplier = 1f;

        foreach (ITowerStatMultiplierQuery source in _sources)
        {
            multiplier *= source.GetDamageMultiplier(towerData);
        }

        return multiplier;
    }

    public float GetRangeMultiplier(TowerData towerData)
    {
        float multiplier = 1f;

        foreach (ITowerStatMultiplierQuery source in _sources)
        {
            multiplier *= source.GetRangeMultiplier(towerData);
        }

        return multiplier;
    }

    public float GetAttackSpeedMultiplier(TowerData towerData)
    {
        float multiplier = 1f;

        foreach (ITowerStatMultiplierQuery source in _sources)
        {
            multiplier *= source.GetAttackSpeedMultiplier(towerData);
        }

        return multiplier;
    }
}
