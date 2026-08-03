/// <summary>
/// 현재 상태로 다음 유지비 정산을 실행했을 때의 읽기 전용 예상 결과다.
/// 실제 자원이나 인구는 변경하지 않는다.
/// </summary>
public readonly struct PopulationUpkeepPreview
{
    public int RequiredFood { get; }
    public int FoodAtSettlement { get; }
    public int ConsumedFood { get; }
    public int FoodShortage => RequiredFood - ConsumedFood;
    public int PopulationLost => FoodShortage;

    public PopulationUpkeepPreview(
        int requiredFood,
        int foodAtSettlement,
        int consumedFood
    )
    {
        RequiredFood = requiredFood;
        FoodAtSettlement = foodAtSettlement;
        ConsumedFood = consumedFood;
    }
}
