using NUnit.Framework;

/// <summary>
/// empty_hands(시작 자원 배율)와 시작 식량 하한 보정(조합 안전장치)을 고정한다.
/// </summary>
public class StartingResourceRulesTests
{
    // SampleScene의 초기 지급량(식량·통나무·돌 각각).
    private const int INITIAL_AMOUNT = 350;

    private const float NEUTRAL = 1f;
    private const float TIER1 = 0.75f;
    private const float TIER2 = 0.5f;
    private const float TIER3 = 0.25f;

    // SampleScene의 최대 인구 - PopulationManager.MaxPopulation.
    private const int MAX_POPULATION = 50;

    // 대식가(big_appetite) 3단계 배율 - RunMutatorAssetAuthoring.BIG_APPETITE_MULTIPLIER[2].
    private const float BIG_APPETITE_TIER3 = 1.5f;

    // EconomyBalanceData._startingFoodBufferDaysUnderMutators 기본값.
    private const int BUFFER_DAYS = 3;

    // 심연(65점, 빈손3+대식가3+흉년3 포함)의 하한값 = ceil(50명 x 1.5) x 3일 = 75 x 3 = 225.
    private const int EXPECTED_FLOOR_AT_ABYSS = 225;

    // 회귀: 뮤테이터가 없으면 지급량이 손대지 않은 값 그대로여야 한다.
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(INITIAL_AMOUNT)]
    [TestCase(int.MaxValue)]
    public void Scale_NeutralMultiplier_ReturnsAmountUnchanged(int amount)
    {
        Assert.That(StartingResourceRules.Scale(amount, NEUTRAL), Is.EqualTo(amount));
    }

    // 262.5는 Mathf.RoundToInt의 중간값 규칙(짝수로)에 따라 262가 된다 - TerrainUpkeepRules와
    // 같은 반올림 함수를 쓰기 위한 것이고, 이 자리에서 1 차이는 밸런스에 의미가 없다.
    [TestCase(TIER1, 262)]
    [TestCase(TIER2, 175)]
    [TestCase(TIER3, 88)]
    public void Scale_AppliesMultiplier(float multiplier, int expected)
    {
        Assert.That(StartingResourceRules.Scale(INITIAL_AMOUNT, multiplier), Is.EqualTo(expected));
    }

    // 반올림으로 0이 되면 소량 지급되는 특화 자원이 통째로 사라진다 - 최소 1은 남긴다.
    [TestCase(1, TIER3)]
    [TestCase(2, TIER3)]
    [TestCase(1, 0f)]
    public void Scale_NeverDropsAGrantedResourceToZero(int amount, float multiplier)
    {
        Assert.That(StartingResourceRules.Scale(amount, multiplier), Is.GreaterThanOrEqualTo(1));
    }

    // 지급 목록에 없던(0 이하) 항목은 배율과 무관하게 0으로 남아야 한다 - 최소 1 클램프가
    // "지급하지 않는다"를 "1개 지급한다"로 바꿔서는 안 된다.
    [TestCase(0)]
    [TestCase(-5)]
    public void Scale_DoesNotGrantResourcesThatWereNotGranted(int amount)
    {
        Assert.That(StartingResourceRules.Scale(amount, TIER3), Is.EqualTo(amount));
    }

    // 회귀: 뮤테이터가 없으면 하한이 표준 시작 식량을 건드리지 않는다.
    // 하한값이 시작 식량보다 커도 마찬가지여야 한다(그래서 배율로 판정한다).
    [TestCase(0)]
    [TestCase(INITIAL_AMOUNT)]
    public void ApplyFoodFloor_NeutralMultiplier_LeavesFoodUnchanged(int food)
    {
        Assert.That(
            StartingResourceRules.ApplyFoodFloor(food, EXPECTED_FLOOR_AT_ABYSS, NEUTRAL),
            Is.EqualTo(food));
    }

    [TestCase(88, 225)]
    [TestCase(0, 225)]
    [TestCase(225, 225)]
    [TestCase(263, 263)]
    public void ApplyFoodFloor_RaisesOnlyBelowTheFloor(int food, int expected)
    {
        Assert.That(
            StartingResourceRules.ApplyFoodFloor(food, EXPECTED_FLOOR_AT_ABYSS, TIER3),
            Is.EqualTo(expected));
    }

    // 하한 필드가 0이면 안전장치를 끈 것 - 음수 하한이 보유량을 깎아서는 안 된다.
    [TestCase(0)]
    [TestCase(-100)]
    public void ApplyFoodFloor_NonPositiveFloor_NeverReducesFood(int floor)
    {
        Assert.That(
            StartingResourceRules.ApplyFoodFloor(88, floor, TIER3),
            Is.EqualTo(88));
    }

    // ResolveFoodFloor: 배율 적용 전 시작량을 절대 넘지 않는다 - 하드 모드가 표준 모드보다
    // 후해지는 것을 막는 상한이다. 유지비 1000 x 30일 = 30000은 시작량 100보다 훨씬 크다.
    [Test]
    public void ResolveFoodFloor_NeverExceedsUnscaledStartingAmount()
    {
        Assert.That(StartingResourceRules.ResolveFoodFloor(100, 1000, 30), Is.EqualTo(100));
    }

    // 유지비가 0이면(설정으로 유지비를 걷지 않는 경우) 하한도 0이어야 한다 - 강제로 식량을
    // 채워 넣는 부작용을 만들지 않는다.
    [Test]
    public void ResolveFoodFloor_ZeroDailyUpkeep_ResultsInZeroFloor()
    {
        Assert.That(StartingResourceRules.ResolveFoodFloor(INITIAL_AMOUNT, 0, BUFFER_DAYS), Is.EqualTo(0));
    }

    // int 곱셈 오버플로가 음수 하한을 만들어서는 안 된다 - long으로 올려 계산하고 상한에서 클램프한다.
    [Test]
    public void ResolveFoodFloor_LargeInputs_NeverGoesNegative()
    {
        Assert.That(
            StartingResourceRules.ResolveFoodFloor(int.MaxValue, int.MaxValue, int.MaxValue),
            Is.GreaterThanOrEqualTo(0));
    }

    // 최악 조합(빈손 3단계 + 대식가 3단계 + 흉년 3단계): 시작 식량 350 x 0.25 = 88.
    // 최대 인구 50 x 대식가 3단계(1명당 1.5) = 1일차 유지비 75, 하한은 그 3일치(225)를 보장해야
    // 한다 - 하한이 없으면(또는 1일치뿐이면) 2일차부터 굶어 죽기 시작해 회복 불능이 된다.
    [Test]
    public void WorstCombination_StartsWithFullFoodBuffer()
    {
        int scaledFood = StartingResourceRules.Scale(INITIAL_AMOUNT, TIER3);

        float effectivePerPopulation =
            PopulationUpkeepRules.GetEffectiveFoodPerPopulation(1, BIG_APPETITE_TIER3);
        int day1Upkeep = PopulationUpkeepRules.GetRequiredFood(MAX_POPULATION, effectivePerPopulation);
        int floor = StartingResourceRules.ResolveFoodFloor(INITIAL_AMOUNT, day1Upkeep, BUFFER_DAYS);
        int flooredFood = StartingResourceRules.ApplyFoodFloor(scaledFood, floor, TIER3);

        Assert.That(floor, Is.EqualTo(EXPECTED_FLOOR_AT_ABYSS));
        Assert.That(scaledFood, Is.LessThan(day1Upkeep * BUFFER_DAYS));
        Assert.That(flooredFood, Is.GreaterThanOrEqualTo(day1Upkeep * BUFFER_DAYS));
    }
}
