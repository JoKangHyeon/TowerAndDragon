using UnityEngine;

public class PopulationUpkeepDebugTester : MonoBehaviour
{
    private const int TEST_FOOD_AMOUNT = 50;

    [SerializeField]
    private PopulationUpkeepSystem _populationUpkeepSystem;

    [SerializeField]
    private ResourceManager _resourceManager;

    [SerializeField]
    private PopulationManager _populationManager;

    [ContextMenu("테스트/유지비 정산 실행")]
    private void Settle()
    {
        bool isSettled = _populationUpkeepSystem.TrySettle(
            out PopulationUpkeepResult result
        );

        Debug.Log(
            $"정산 성공: {isSettled}, " +
            $"필요 식량: {result.RequiredFood}, " +
            $"소비 식량: {result.ConsumedFood}, " +
            $"부족 식량: {result.FoodShortage}, " +
            $"사망 인구: {result.PopulationLost}"
        );

        LogCurrentState();
    }

    [ContextMenu("테스트/식량 지급")]
    private void AddFood()
    {
        _resourceManager.Add(
            ResourceType.Food,
            TEST_FOOD_AMOUNT
        );

        LogCurrentState();
    }

    [ContextMenu("테스트/식량 전부 제거")]
    private void RemoveAllFood()
    {
        int currentFood = _resourceManager.GetAmount(
            ResourceType.Food
        );

        if (currentFood > 0)
        {
            _resourceManager.TrySpend(
                ResourceType.Food,
                currentFood
            );
        }

        LogCurrentState();
    }

    [ContextMenu("테스트/현재 상태 출력")]
    private void LogCurrentState()
    {
        Debug.Log(
            $"식량: {_resourceManager.GetAmount(ResourceType.Food)}, " +
            $"전체 인구: {_populationManager.MaxPopulation}, " +
            $"할당 인구: {_populationManager.AssignedPopulation}, " +
            $"가용 인구: {_populationManager.AvailablePopulation}"
        );
    }
}