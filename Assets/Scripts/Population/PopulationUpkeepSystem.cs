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

        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            _populationManager.MaxPopulation,
            _resourceManager.GetAmount(ResourceType.Food),
            0
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
