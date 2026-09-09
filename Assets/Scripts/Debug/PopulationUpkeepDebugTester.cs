using UnityEngine;

/// <summary>
/// [에디터 테스트 전용] 인구 유지비 정산·식량 지급/제거를 ContextMenu로 확인하는 테스트 컴포넌트다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public class PopulationUpkeepDebugTester : MonoBehaviour
{
#if UNITY_EDITOR
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
#endif
}
