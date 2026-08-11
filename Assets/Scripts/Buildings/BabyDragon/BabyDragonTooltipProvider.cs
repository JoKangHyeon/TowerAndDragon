using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배치된 새끼용의 툴팁 내용을 만든다. 값을 모으는 일만 하고 문구 조립은 BabyDragonTooltipBuilder가 맡는다.
/// 새끼용이 아닌 건물에는 응답하지 않으므로 드라이버가 다음 제공자로 넘어간다.
/// </summary>
public class BabyDragonTooltipProvider : BuildingTooltipProvider
{
    [Tooltip("실제 적용 배율과 버프 대상을 조회한다. 비우면 버프 관련 내용이 빠진 채로 표시된다.")]
    [SerializeField] private BabyDragonBuffSystem _buffSystem;

    [SerializeField] private ResourceCatalog _resourceCatalog;

    // 커서를 올린 동안 주기적으로 다시 만들므로 호출마다 새로 할당하지 않는다.
    private readonly List<BabyDragonBuffTarget> _buffTargetBuffer = new();

    public override bool TryBuild(Building building, out TooltipContent content)
    {
        content = default;

        if (!(building is BabyDragonTower babyDragon) || babyDragon.DragonData == null)
        {
            return false;
        }

        _buffTargetBuffer.Clear();
        float effectiveMultiplier = babyDragon.DragonData.BuffYieldMultiplier;

        // 둘 다 없어도 문구 자체는 뜨지만 값이 조용히 빈약해진다 - 어느 칸이 비었는지 알린다.
        if (WiringGuard.Optional(_buffSystem, nameof(_buffSystem), this))
        {
            effectiveMultiplier = _buffSystem.GetEffectiveYieldMultiplier(babyDragon.DragonData);
            _buffSystem.CollectBuffTargets(babyDragon, _buffTargetBuffer);
        }

        WiringGuard.Optional(_resourceCatalog, nameof(_resourceCatalog), this);

        content = BabyDragonTooltipBuilder.Build(
            babyDragon,
            effectiveMultiplier,
            _buffTargetBuffer,
            _resourceCatalog);

        return content.HasContent;
    }
}
