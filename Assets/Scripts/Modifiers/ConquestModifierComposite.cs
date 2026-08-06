using System.Collections.Generic;
using UnityEngine;

// 점령 비용 할인·소요일 감소는 배율이 아니라 감산치이므로 소스 간 합은 Σ.
public sealed class ConquestModifierComposite : MonoBehaviour, IConquestModifierQuery
{
    private readonly RefCountedSourceSet<IConquestModifierQuery> _sources = new();

    public void Register(IConquestModifierQuery source)
    {
        _sources.Register(source);
    }

    public void Unregister(IConquestModifierQuery source)
    {
        _sources.Unregister(source);
    }

    public float GetConquestCostReductionRatio()
    {
        float ratio = 0f;
        IReadOnlyList<IConquestModifierQuery> sources = _sources.Sources;

        for (int i = 0; i < sources.Count; i++)
        {
            ratio += sources[i].GetConquestCostReductionRatio();
        }

        return ratio;
    }

    public int GetConquestDaysReduction()
    {
        int days = 0;
        IReadOnlyList<IConquestModifierQuery> sources = _sources.Sources;

        for (int i = 0; i < sources.Count; i++)
        {
            days += sources[i].GetConquestDaysReduction();
        }

        return days;
    }
}
