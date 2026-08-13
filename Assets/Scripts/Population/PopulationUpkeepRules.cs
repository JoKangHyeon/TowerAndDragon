using System;

/// <summary>
/// 인구 유지비의 실제 정산과 UI 미리보기가 공유하는 순수 계산 규칙이다.
/// </summary>
public static class PopulationUpkeepRules
{
    /// <summary>
    /// 하루에 필요한 식량. 인구 1명당 foodPerPopulation이다.
    /// 자원 예측(ResourceForecast)이 소모량을 집계할 때도 같은 값을 써야 하므로 따로 노출한다.
    ///
    /// 1명당 소모량을 인자로 받는 이유: 이 값은 밸런싱 대상이라 EconomyBalanceData 에셋에 있는데,
    /// 이 클래스는 씬 참조를 가질 수 없는 순수 규칙이므로 호출부가 읽어서 넘긴다.
    ///
    /// 곱셈은 long으로 올려서 계산한다. int로 곱하면 오버플로로 음수가 나오고, 그 음수가
    /// Calculate의 Math.Min을 그대로 통과해 ResourceManager.ConsumeUpTo에 음수로 들어간다.
    /// (Calculate의 projectedFood 합산도 같은 이유로 long이다.)
    /// </summary>
    public static int GetRequiredFood(int population, int foodPerPopulation)
    {
        long required = (long)Math.Max(0, population) * Math.Max(0, foodPerPopulation);

        return required > int.MaxValue ? int.MaxValue : (int)required;
    }

    public static PopulationUpkeepPreview Calculate(
        int population,
        int currentFood,
        int projectedFoodProduction,
        int foodPerPopulation
    )
    {
        int requiredFood = GetRequiredFood(population, foodPerPopulation);
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
            consumedFood,
            foodPerPopulation
        );
    }
}
