using UnityEngine;

/// <summary>
/// 전체 인구를 기준으로 하루 식량 유지비를 소비하고 부족분만큼 기아를 적용한다.
/// 실행 시점과 다른 정산 단계의 순서는 DailySettlementManager가 담당한다.
/// </summary>
public class PopulationUpkeepSystem : MonoBehaviour
{
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private PopulationManager _populationManager;

    public bool TrySettle(out PopulationUpkeepResult result)
    {
        result = default;

        if (_resourceManager == null || _populationManager == null)
        {
            return false;
        }

        int requiredFood = _populationManager.MaxPopulation;
        int consumedFood = _resourceManager.ConsumeUpTo(
            ResourceType.Food,
            requiredFood
        );
        int foodShortage = requiredFood - consumedFood;
        StarvationResult starvation = default;

        if (foodShortage > 0)
        {
            _populationManager.TryApplyStarvation(
                foodShortage,
                out starvation
            );
        }

        result = new PopulationUpkeepResult(
            requiredFood,
            consumedFood,
            starvation
        );
        return true;
    }
}
