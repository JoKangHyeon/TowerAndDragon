using UnityEngine;

/// <summary>
/// 전체 인구를 기준으로 하루 식량 유지비를 소비하고 부족분만큼 기아를 적용한다.
/// 실행 시점과 다른 정산 단계의 순서는 DailySettlementManager가 담당한다.
/// </summary>
public class PopulationUpkeepSystem : MonoBehaviour
{
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private PopulationManager _populationManager;

    // 인구 1명당 식량 소모량의 출처. 미연결이면 유지비를 걷지 않는다(정산 자체를 건너뛴다).
    [SerializeField] private EconomyBalanceData _economyBalance;

    public bool TrySettle(out PopulationUpkeepResult result)
    {
        result = default;

        if (_resourceManager == null || _populationManager == null)
        {
            return false;
        }

        if (!WiringGuard.Require(_economyBalance, nameof(_economyBalance), this))
        {
            return false;
        }

        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            _populationManager.MaxPopulation,
            _resourceManager.GetAmount(ResourceType.Food),
            0,
            _economyBalance.FoodUpkeepPerPopulation
        );
        int consumedFood = _resourceManager.ConsumeUpTo(
            ResourceType.Food,
            preview.ConsumedFood
        );
        StarvationResult starvation = default;

        if (preview.PopulationLost > 0)
        {
            _populationManager.TryApplyStarvation(
                preview.PopulationLost,
                out starvation
            );
        }

        result = new PopulationUpkeepResult(
            preview.RequiredFood,
            consumedFood,
            starvation
        );
        return true;
    }
}
