using System;

/// <summary>
/// 현재 상태로 다음 유지비 정산을 실행했을 때의 읽기 전용 예상 결과다.
/// 실제 자원이나 인구는 변경하지 않는다.
/// </summary>
public readonly struct PopulationUpkeepPreview
{
    public int RequiredFood { get; }
    public int FoodAtSettlement { get; }
    public int ConsumedFood { get; }

    // 1명당 소모량. 부족한 식량을 아사 인원으로 환산할 때 나눗셈의 분모가 된다.
    private readonly int _foodPerPopulation;

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

            int perPopulation = Math.Max(1, _foodPerPopulation);

            // 올림 나눗셈의 중간 합(shortage + perPopulation - 1)이 int를 넘을 수 있어 long으로 올린다.
            // int로 계산하면 음수로 뒤집혀 아사 인원이 음수가 된다.
            // 몫 자체는 항상 shortage 이하이므로 int로 되돌리는 건 안전하다.
            return (int)(((long)shortage + perPopulation - 1) / perPopulation);
        }
    }

    public PopulationUpkeepPreview(
        int requiredFood,
        int foodAtSettlement,
        int consumedFood,
        int foodPerPopulation
    )
    {
        RequiredFood = requiredFood;
        FoodAtSettlement = foodAtSettlement;
        ConsumedFood = consumedFood;
        _foodPerPopulation = foodPerPopulation;
    }
}
