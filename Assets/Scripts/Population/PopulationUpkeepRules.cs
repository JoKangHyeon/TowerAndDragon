using System;

/// <summary>
/// 인구 유지비의 실제 정산과 UI 미리보기가 공유하는 순수 계산 규칙이다.
/// </summary>
public static class PopulationUpkeepRules
{
    /// <summary>
    /// 하루에 필요한 식량. 인구 1명당 1이다.
    /// 자원 예측(ResourceForecast)이 소모량을 집계할 때도 같은 값을 써야 하므로 따로 노출한다.
    /// </summary>
    public static int GetRequiredFood(int population) => Math.Max(0, population);

    public static PopulationUpkeepPreview Calculate(
        int population,
        int currentFood,
        int projectedFoodProduction
    )
    {
        int requiredFood = GetRequiredFood(population);
        int normalizedCurrentFood = Math.Max(0, currentFood);
        int normalizedProduction = Math.Max(0, projectedFoodProduction);
        long projectedFood =
            (long)normalizedCurrentFood + normalizedProduction;
        int foodAtSettlement = projectedFood > int.MaxValue
            ? int.MaxValue
            : (int)projectedFood;
        int consumedFood = Math.Min(requiredFood, foodAtSettlement);

        return new PopulationUpkeepPreview(
            requiredFood,
            foodAtSettlement,
            consumedFood
        );
    }
}
