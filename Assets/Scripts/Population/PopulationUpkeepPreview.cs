using System;

/// <summary>
/// 현재 상태로 다음 유지비 정산을 실행했을 때의 읽기 전용 예상 결과다.
/// 실제 자원이나 인구는 변경하지 않는다.
/// </summary>
public readonly struct PopulationUpkeepPreview
{
    // 아사 인원 환산의 분모 하한. 1명당 소모량이 0이면 0으로 나누게 된다.
    private const float MIN_FOOD_PER_POPULATION = 1f;

    // 올림 직전에 빼는 부동소수 오차 흡수분. 이유는 PopulationUpkeepRules.CEILING_EPSILON과 같다.
    private const double CEILING_EPSILON = 1e-6;

    public int RequiredFood { get; }
    public int FoodAtSettlement { get; }
    public int ConsumedFood { get; }

    // 1명당 소모량. 부족한 식량을 아사 인원으로 환산할 때 나눗셈의 분모가 된다.
    //
    // 정수가 아니라 float인 이유: big_appetite 1단계는 1명당 1.5를 먹는다. 이 분모를 2로
    // 반올림하면 같은 부족분에 굶어 죽는 인원이 실제보다 적게 나와, 유지비는 늘었는데
    // 기아 피해는 줄어드는 모순이 생긴다. (PopulationUpkeepRules.GetEffectiveFoodPerPopulation 참조)
    private readonly float _foodPerPopulation;

    public int FoodShortage => RequiredFood - ConsumedFood;

    /// <summary>
    /// 굶어 죽는 인원. 부족한 식량을 1명당 소모량으로 나눈 값을 올림한다 -
    /// 걷힌 식량으로 먹일 수 있는 인원은 floor(ConsumedFood / 1명당)이고, 나머지는 전부 굶기 때문이다.
    /// (1명당 소모량이 1이면 부족량과 그대로 같다.)
    /// </summary>
    public int PopulationLost
    {
        get
        {
            int shortage = FoodShortage;
            if (shortage <= 0)
            {
                return 0;
            }

            double perPopulation = Math.Max(MIN_FOOD_PER_POPULATION, _foodPerPopulation);

            // double로 나누는 이유는 int 올림 나눗셈이 오버플로로 음수가 되는 것을 막는 것과 같다
            // (분모가 1이고 부족량이 int.MaxValue면 중간 합이 int를 넘는다).
            // 몫 자체는 항상 shortage 이하이므로 int로 되돌리는 건 안전하다.
            return (int)Math.Ceiling(shortage / perPopulation - CEILING_EPSILON);
        }
    }

    public PopulationUpkeepPreview(
        int requiredFood,
        int foodAtSettlement,
        int consumedFood,
        float foodPerPopulation
    )
    {
        RequiredFood = requiredFood;
        FoodAtSettlement = foodAtSettlement;
        ConsumedFood = consumedFood;
        _foodPerPopulation = foodPerPopulation;
    }
}
