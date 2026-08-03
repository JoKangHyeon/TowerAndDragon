using System;

/// <summary>
/// 인구 유지비의 실제 정산과 UI 미리보기가 공유하는 순수 계산 규칙이다.
/// </summary>
public static class PopulationUpkeepRules
{
    public static PopulationUpkeepPreview Calculate(
        int population,
        int currentFood,
        int projectedFoodProduction
    )
    {
        int requiredFood = Math.Max(0, population);
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
