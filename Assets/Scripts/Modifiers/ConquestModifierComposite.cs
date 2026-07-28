using System.Collections.Generic;
using UnityEngine;

// 점령 비용 할인·소요일 감소는 배율이 아니라 감산치이므로 소스 간 합은 Σ.
public sealed class ConquestModifierComposite : MonoBehaviour, IConquestModifierQuery
{
    private readonly List<IConquestModifierQuery> _sources = new();

    public void Register(IConquestModifierQuery source)
    {
        if (source != null && !_sources.Contains(source))
        {
            _sources.Add(source);
        }
    }

    public void Unregister(IConquestModifierQuery source)
    {
        _sources.Remove(source);
    }

    public float GetConquestCostReductionRatio()
    {
        float ratio = 0f;

        foreach (IConquestModifierQuery source in _sources)
        {
            ratio += source.GetConquestCostReductionRatio();
        }

        return ratio;
    }

    public int GetConquestDaysReduction()
    {
        int days = 0;

        foreach (IConquestModifierQuery source in _sources)
        {
            days += source.GetConquestDaysReduction();
        }

        return days;
    }
}
