/// <summary>
/// 하루 인구 유지비 정산 결과를 전달하는 읽기 전용 스냅숏이다.
/// </summary>
public readonly struct PopulationUpkeepResult
{
    public int RequiredFood { get; }
    public int ConsumedFood { get; }
    public int FoodShortage { get; }
    public StarvationResult Starvation { get; }
    public int PopulationLost => Starvation.PopulationLost;

    public PopulationUpkeepResult(
        int requiredFood,
        int consumedFood,
        StarvationResult starvation
    )
    {
        RequiredFood = requiredFood;
        ConsumedFood = consumedFood;
        FoodShortage = requiredFood - consumedFood;
        Starvation = starvation;
    }
}
