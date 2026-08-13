using NUnit.Framework;

public class ResourceForecastRulesTests
{
    // 알파 기준값(EB_EconomyBalance_Alpha). 이 값이 1일 때 부족 식량과 아사 인원이 1:1로 대응한다.
    private const int FOOD_PER_POPULATION = 1;

    [TestCase(20, 0, 20)]
    [TestCase(20, 35, -15)]
    [TestCase(20, 20, 0)]
    [TestCase(0, 0, 0)]
    public void NetChange_SubtractsConsumptionFromProduction(
        int production,
        int consumption,
        int expected
    )
    {
        Assert.That(ResourceForecastRules.NetChange(production, consumption), Is.EqualTo(expected));
    }

    [TestCase(100, 20, 120)]
    [TestCase(100, -20, 80)]
    [TestCase(10, -15, -5)]
    [TestCase(15, -15, 0)]
    public void ProjectedAmount_AddsNetChangeToCurrentAmount(
        int currentAmount,
        int netChange,
        int expected
    )
    {
        Assert.That(
            ResourceForecastRules.ProjectedAmount(currentAmount, netChange),
            Is.EqualTo(expected));
    }

    [TestCase(100, 20, false, TestName = "WillRunOut_Growing_IsFalse")]
    [TestCase(100, -20, false, TestName = "WillRunOut_ShrinkingButStillPositive_IsFalse")]
    [TestCase(10, -15, true, TestName = "WillRunOut_ShrinkingBelowZero_IsTrue")]
    [TestCase(15, -15, true, TestName = "WillRunOut_ShrinkingExactlyToZero_IsTrue")]
    public void WillRunOut_FlagsOnlyShrinkingResources(
        int currentAmount,
        int netChange,
        bool expected
    )
    {
        Assert.That(ResourceForecastRules.WillRunOut(currentAmount, netChange), Is.EqualTo(expected));
    }

    // 소모원이 없는 특화·슬라임 자원은 보유량 0 · 순증감 0으로 오래 머무른다.
    // "0 이하"만 보면 여기서 항상 경고가 켜지므로, 줄어드는 중일 때만 켜지는지 못 박아 둔다.
    [Test]
    public void WillRunOut_ResourceWithNoFlowAtZero_IsFalse()
    {
        Assert.That(ResourceForecastRules.WillRunOut(0, 0), Is.False);
    }

    [Test]
    public void WillRunOut_ResourceAlreadyEmptyButNotShrinking_IsFalse()
    {
        Assert.That(ResourceForecastRules.WillRunOut(0, 5), Is.False);
    }

    [TestCase(10, -15, 5)]
    [TestCase(0, -30, 30)]
    [TestCase(15, -15, 0)]
    [TestCase(100, -20, 0)]
    [TestCase(100, 20, 0)]
    public void Shortage_ReportsHowMuchIsMissing(
        int currentAmount,
        int netChange,
        int expected
    )
    {
        Assert.That(ResourceForecastRules.Shortage(currentAmount, netChange), Is.EqualTo(expected));
    }

    // 식량 부족량은 인구 아사 수와 같아야 한다(PopulationUpkeepRules와 정의가 어긋나면 툴팁이 거짓말을 한다).
    [TestCase(20, 30, 5)]
    [TestCase(0, 30, 10)]
    [TestCase(50, 30, 10)]
    public void Shortage_MatchesPopulationUpkeepPreviewFoodShortage(
        int currentFood,
        int population,
        int foodProduction
    )
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            population,
            currentFood,
            foodProduction,
            FOOD_PER_POPULATION);

        int netChange = ResourceForecastRules.NetChange(
            foodProduction,
            PopulationUpkeepRules.GetRequiredFood(population, FOOD_PER_POPULATION));

        Assert.That(
            ResourceForecastRules.Shortage(currentFood, netChange),
            Is.EqualTo(preview.PopulationLost));
    }

    [Test]
    public void NetChange_Overflow_ClampsInsteadOfWrapping()
    {
        Assert.That(
            ResourceForecastRules.NetChange(int.MaxValue, int.MinValue),
            Is.EqualTo(int.MaxValue));
        Assert.That(
            ResourceForecastRules.NetChange(int.MinValue, int.MaxValue),
            Is.EqualTo(int.MinValue));
    }

    [Test]
    public void ProjectedAmount_Overflow_ClampsInsteadOfWrapping()
    {
        Assert.That(
            ResourceForecastRules.ProjectedAmount(int.MaxValue, int.MaxValue),
            Is.EqualTo(int.MaxValue));
        Assert.That(
            ResourceForecastRules.ProjectedAmount(int.MinValue, int.MinValue),
            Is.EqualTo(int.MinValue));
    }
}
