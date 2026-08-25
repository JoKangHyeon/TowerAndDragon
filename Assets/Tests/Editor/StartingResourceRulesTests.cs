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

    private const int FOOD_FLOOR = 150;

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
            StartingResourceRules.ApplyFoodFloor(food, FOOD_FLOOR, NEUTRAL),
            Is.EqualTo(food));
    }

    [TestCase(88, 150)]
    [TestCase(0, 150)]
    [TestCase(150, 150)]
    [TestCase(263, 263)]
    public void ApplyFoodFloor_RaisesOnlyBelowTheFloor(int food, int expected)
    {
        Assert.That(
            StartingResourceRules.ApplyFoodFloor(food, FOOD_FLOOR, TIER3),
            Is.EqualTo(expected));
    }

    // 최악 조합(빈손 3단계): 350 x 0.25 = 88 -> 하한 150으로 올라와야 한다.
    // 최대 인구 50 x 대식가 3단계(1명당 3) = 1일차 유지비 150이라, 하한이 없으면 1일차부터
    // 굶어 죽기 시작해 회복 불능이 된다.
    [Test]
    public void WorstCombination_StartsAtOrAboveOneDayOfUpkeep()
    {
        int scaledFood = StartingResourceRules.Scale(INITIAL_AMOUNT, TIER3);
        int flooredFood = StartingResourceRules.ApplyFoodFloor(scaledFood, FOOD_FLOOR, TIER3);

        float effectivePerPopulation =
            PopulationUpkeepRules.GetEffectiveFoodPerPopulation(1, 3f);
        int day1Upkeep = PopulationUpkeepRules.GetRequiredFood(50, effectivePerPopulation);

        Assert.That(scaledFood, Is.LessThan(day1Upkeep));
        Assert.That(flooredFood, Is.GreaterThanOrEqualTo(day1Upkeep));
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
}
