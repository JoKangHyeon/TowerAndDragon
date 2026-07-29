using System.Collections.Generic;
using UnityEngine;

// 시야 반경 보너스는 배율이 아니라 가산치이므로 소스 간 합은 Σ.
public sealed class VisionRadiusComposite : MonoBehaviour, IVisionRadiusBonusQuery
{
    private readonly List<IVisionRadiusBonusQuery> _sources = new();

    public void Register(IVisionRadiusBonusQuery source)
    {
        if (source != null && !_sources.Contains(source))
        {
            _sources.Add(source);
        }
    }

    public void Unregister(IVisionRadiusBonusQuery source)
    {
        _sources.Remove(source);
    }

    public int GetVisionRadiusBonus()
    {
        int bonus = 0;

        foreach (IVisionRadiusBonusQuery source in _sources)
        {
            bonus += source.GetVisionRadiusBonus();
        }

        return bonus;
    }
}
