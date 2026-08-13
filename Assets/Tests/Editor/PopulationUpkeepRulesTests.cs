using NUnit.Framework;

public class PopulationUpkeepRulesTests
{
    // 알파 기준값(EB_EconomyBalance_Alpha). 이 값이 1일 때 부족 식량과 아사 인원이 1:1로 대응한다.
    private const int FOOD_PER_POPULATION = 1;

    [TestCase(30, 40, 0, 30, 40, 30, 0)]
    [TestCase(30, 20, 5, 30, 25, 25, 5)]
    [TestCase(30, 20, 20, 30, 40, 30, 0)]
    [TestCase(30, 0, 0, 30, 0, 0, 30)]
    [TestCase(0, 10, 5, 0, 15, 0, 0)]
    [TestCase(-1, 10, 5, 0, 15, 0, 0)]
    [TestCase(10, -5, -2, 10, 0, 0, 10)]
    public void Calculate_ResolvesSettlementPreview(
        int population,
        int currentFood,
        int projectedFoodProduction,
        int expectedRequiredFood,
        int expectedFoodAtSettlement,
        int expectedConsumedFood,
        int expectedPopulationLost
    )
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            population,
            currentFood,
            projectedFoodProduction,
            FOOD_PER_POPULATION
        );

        Assert.That(preview.RequiredFood, Is.EqualTo(expectedRequiredFood));
        Assert.That(preview.FoodAtSettlement, Is.EqualTo(expectedFoodAtSettlement));
        Assert.That(preview.ConsumedFood, Is.EqualTo(expectedConsumedFood));
        Assert.That(preview.PopulationLost, Is.EqualTo(expectedPopulationLost));
    }

    [Test]
    public void Calculate_FoodProjectionOverflows_ClampsToIntMaxValue()
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            FOOD_PER_POPULATION
        );

        Assert.That(preview.FoodAtSettlement, Is.EqualTo(int.MaxValue));
        Assert.That(preview.ConsumedFood, Is.EqualTo(int.MaxValue));
        Assert.That(preview.PopulationLost, Is.Zero);
    }

    [TestCase(5, 3, 15)]
    [TestCase(0, 5, 0)]
    [TestCase(5, 0, 0)]
    [TestCase(-3, 4, 0)]
    [TestCase(5, -4, 0)]
    public void GetRequiredFood_MultipliesAndFloorsNegativeInputsAtZero(
        int population,
        int foodPerPopulation,
        int expectedRequiredFood
    )
    {
        Assert.That(
            PopulationUpkeepRules.GetRequiredFood(population, foodPerPopulation),
            Is.EqualTo(expectedRequiredFood)
        );
    }

    // 인구 x 1명당 소모량이 int 범위를 넘어도 음수로 뒤집혀선 안 된다 - 음수 requiredFood는
    // Calculate의 Math.Min을 그대로 통과해 ResourceManager.ConsumeUpTo에 음수로 들어간다.
    // FOOD_PER_POPULATION이 1인 동안은 Calculate 쪽 테스트만으로는 이 경로가 드러나지 않으므로
    // 1명당 소모량을 직접 올려서 검증한다.
    [TestCase(int.MaxValue, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    [TestCase(100000, 100000)]
    public void GetRequiredFood_Overflows_ClampsToIntMaxValue(int population, int foodPerPopulation)
    {
        Assert.That(
            PopulationUpkeepRules.GetRequiredFood(population, foodPerPopulation),
            Is.EqualTo(int.MaxValue)
        );
    }

    // 부족 식량이 int 상한에 가까우면 올림 나눗셈의 중간 합이 int를 넘는다 - 아사 인원이 음수로
    // 뒤집히면 안 되고, 몫은 항상 부족량 이하여야 한다.
    [TestCase(2, 1073741824)]
    [TestCase(100000, 21475)]
    [TestCase(int.MaxValue, 1)]
    public void PopulationLost_ShortageNearIntMaxValue_StaysPositive(
        int foodPerPopulation,
        int expectedPopulationLost
    )
    {
        var preview = new PopulationUpkeepPreview(
            int.MaxValue,
            0,
            0,
            foodPerPopulation
        );

        Assert.That(preview.FoodShortage, Is.EqualTo(int.MaxValue));
        Assert.That(preview.PopulationLost, Is.EqualTo(expectedPopulationLost));
    }

    // 1명당 소모량을 올리면 부족 식량이 그대로 아사 인원이 되면 안 된다 - 걷힌 식량으로 먹일 수
    // 있는 인원은 floor(걷힌 식량 / 1명당)이고 나머지가 굶으므로, 부족량을 1명당으로 나눠 올림한 값이다.
    [TestCase(5, 7, 2, 2)]   // 필요 10, 걷힘 7 → 3 부족 → 3명은 먹이고 2명이 굶는다
    [TestCase(5, 10, 2, 0)]  // 전원 급식
    [TestCase(5, 0, 2, 5)]   // 전원 아사
    [TestCase(5, 9, 2, 1)]   // 1 부족 → 1명만 굶는다
    public void Calculate_ScalesPopulationLostByFoodPerPopulation(
        int population,
        int currentFood,
        int foodPerPopulation,
        int expectedPopulationLost
    )
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            population,
            currentFood,
            0,
            foodPerPopulation
        );

        Assert.That(preview.PopulationLost, Is.EqualTo(expectedPopulationLost));
    }
}
