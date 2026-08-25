using NUnit.Framework;

/// <summary>
/// big_appetite(식량 유지비 배율)의 세 소비 지점이 같은 값을 내는지 고정한다.
///
/// 세 지점은 실제 정산(PopulationUpkeepSystem.TrySettle) · 다음날 예측(ResourceForecast) ·
/// HUD 표시(UI_IngameWindow)다. 셋 다 MonoBehaviour라 EditMode에서 직접 만들 수 없으므로,
/// 검증을 두 겹으로 나눈다.
///  - "세 곳이 같은 순수 함수를 거치는가"는 코드 리뷰가 본다.
///  - "그 함수들이 서로 일치하는가"는 이 테스트가 고정한다.
/// 예측은 GetRequiredFood를, 정산·표시는 Calculate를 쓰므로 둘이 어긋나면 "표시 4 / 실제 8"이 된다.
/// </summary>
public class RunFoodUpkeepConsistencyTests
{
    // 알파 기준값(EB_EconomyBalance_Alpha).
    private const int BASE_FOOD_PER_POPULATION = 1;

    // SampleScene의 최대 인구.
    private const int POPULATION = 50;

    private const float NEUTRAL = 1f;
    private const float TIER1 = 1.5f;
    private const float TIER2 = 2f;
    private const float TIER3 = 3f;

    // 예측(GetRequiredFood)과 정산·표시(Calculate)가 같은 필요 식량을 내야 한다.
    [TestCase(NEUTRAL)]
    [TestCase(TIER1)]
    [TestCase(TIER2)]
    [TestCase(TIER3)]
    public void ForecastAndSettlement_UseSameRequiredFood(float runMultiplier)
    {
        float effective = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, runMultiplier);

        int forecastRequired = PopulationUpkeepRules.GetRequiredFood(POPULATION, effective);
        PopulationUpkeepPreview settlement = PopulationUpkeepRules.Calculate(
            POPULATION, int.MaxValue / 2, 0, effective);

        Assert.That(settlement.RequiredFood, Is.EqualTo(forecastRequired));
        Assert.That(settlement.ConsumedFood, Is.EqualTo(forecastRequired));
    }

    // 회귀: 뮤테이터가 없으면 배율을 도입하기 전의 정수 경로와 숫자 하나까지 같아야 한다.
    // 이게 이 기능 전체에서 가장 중요한 검사다.
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(37, 3)]
    [TestCase(POPULATION, BASE_FOOD_PER_POPULATION)]
    public void NeutralMultiplier_MatchesLegacyIntegerPath(int population, int basePerPopulation)
    {
        float effective = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            basePerPopulation, NEUTRAL);

        Assert.That(effective, Is.EqualTo((float)basePerPopulation));
        Assert.That(
            PopulationUpkeepRules.GetRequiredFood(population, effective),
            Is.EqualTo(PopulationUpkeepRules.GetRequiredFood(population, basePerPopulation)));
    }

    // 1명당 값을 정수로 반올림하면 x1.5와 x2가 둘 다 2가 되어 1단계와 2단계가 같아진다.
    // 총액에서 한 번만 올림하면 세 단계가 전부 구분된다 - 그게 이 설계의 이유다.
    [TestCase(NEUTRAL, 50)]
    [TestCase(TIER1, 75)]
    [TestCase(TIER2, 100)]
    [TestCase(TIER3, 150)]
    public void EveryTier_ProducesDistinctUpkeep(float runMultiplier, int expectedRequiredFood)
    {
        float effective = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, runMultiplier);

        Assert.That(
            PopulationUpkeepRules.GetRequiredFood(POPULATION, effective),
            Is.EqualTo(expectedRequiredFood));
    }

    // 1단계가 반올림에 먹히지 않는지 - 인구가 1명이어도 유지비는 반드시 늘어야 한다.
    // (내림을 골랐다면 1명 x 1.5 = 1로 깎여 3점짜리 뮤테이터가 0점이 된다.)
    [Test]
    public void Tier1_IncreasesUpkeepEvenForSinglePopulation()
    {
        float neutral = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, NEUTRAL);
        float tier1 = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, TIER1);

        Assert.That(
            PopulationUpkeepRules.GetRequiredFood(1, tier1),
            Is.GreaterThan(PopulationUpkeepRules.GetRequiredFood(1, neutral)));
    }

    // 유지비가 늘면 같은 부족분에 굶어 죽는 인원도 늘어야 한다 - 분모를 정수로 반올림하면
    // 유지비는 늘었는데 기아 피해는 줄어드는 모순이 생긴다.
    [Test]
    public void HigherUpkeep_DoesNotReduceStarvation()
    {
        float neutral = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, NEUTRAL);
        float tier1 = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, TIER1);

        PopulationUpkeepPreview neutralPreview =
            PopulationUpkeepRules.Calculate(POPULATION, 0, 0, neutral);
        PopulationUpkeepPreview tier1Preview =
            PopulationUpkeepRules.Calculate(POPULATION, 0, 0, tier1);

        Assert.That(tier1Preview.RequiredFood, Is.GreaterThan(neutralPreview.RequiredFood));
        Assert.That(
            tier1Preview.PopulationLost,
            Is.GreaterThanOrEqualTo(neutralPreview.PopulationLost));
    }

    // 배율이 소수여도 필요 식량이 인구에 대해 단조 증가해야 한다(올림 오차로 뒤집히지 않는다).
    [Test]
    public void RequiredFood_IsMonotonicInPopulation()
    {
        float effective = PopulationUpkeepRules.GetEffectiveFoodPerPopulation(
            BASE_FOOD_PER_POPULATION, TIER1);
        int previous = 0;

        for (int population = 0; population <= POPULATION; population++)
        {
            int required = PopulationUpkeepRules.GetRequiredFood(population, effective);

            Assert.That(required, Is.GreaterThanOrEqualTo(previous));
            previous = required;
        }
    }
}
