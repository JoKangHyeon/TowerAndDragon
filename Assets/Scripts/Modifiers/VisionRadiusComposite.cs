using System.Collections.Generic;
using UnityEngine;

// 시야 반경 보너스는 배율이 아니라 가산치이므로 소스 간 합은 Σ.
// 같은 소스를 CastleVisionCoordinator와 DragonModifierCoordinator가 각각 등록하므로
// 등록 수명은 RefCountedSourceSet이 센다(한쪽 해제가 기여를 통째로 지우지 않도록).
public sealed class VisionRadiusComposite : MonoBehaviour, IVisionRadiusBonusQuery
{
    private readonly RefCountedSourceSet<IVisionRadiusBonusQuery> _sources = new();

    public void Register(IVisionRadiusBonusQuery source)
    {
        _sources.Register(source);
    }

    public void Unregister(IVisionRadiusBonusQuery source)
    {
        _sources.Unregister(source);
    }

    public int GetVisionRadiusBonus()
    {
        int bonus = 0;
        IReadOnlyList<IVisionRadiusBonusQuery> sources = _sources.Sources;

        for (int i = 0; i < sources.Count; i++)
        {
            bonus += sources[i].GetVisionRadiusBonus();
        }

        return bonus;
    }
}
