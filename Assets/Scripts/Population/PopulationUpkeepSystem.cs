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

    [Tooltip("새 게임 +(뮤테이터)의 식량 유지비 배율 출처. 미연결이면 배율 1(표준 모드)로 본다. " +
        "ResourceForecast·UI_IngameWindow와 같은 서비스를 연결해야 예상치·표시값과 실제 차감액이 어긋나지 않는다.")]
    [WiringOptional]
    [SerializeField] private RunModifierService _runModifiers;

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

        // 배율을 규칙 클래스가 스스로 읽지 않는 이유는 1명당 소모량과 같다 - 순수 규칙은
        // 씬 참조를 가질 수 없으므로 호출부가 읽어서 넘긴다.
        float foodPerPopulation = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            _economyBalance.FoodUpkeepPerPopulation,
            RunModifiers.SnapshotOf(_runModifiers).GetMultiplier(RunModifierChannel.FoodUpkeep));

        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            _populationManager.MaxPopulation,
            _resourceManager.GetAmount(ResourceType.Food),
            0,
            foodPerPopulation
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
