/// <summary>
/// 특정 시점의 최대·할당·가용 인구를 전달하는 읽기 전용 스냅숏이다.
/// 가용 인구는 최대 인구에서 할당 인구를 뺀 파생값으로 계산한다.
/// </summary>
public readonly struct PopulationState
{
    public int MaxPopulation {get;}
    public int AssignedPopulation {get;}    
    public int AvailablePopulation =>
        MaxPopulation - AssignedPopulation;

    public PopulationState(
        int maxPop,
        int assignedPop
    )
    {
        MaxPopulation = maxPop;
        AssignedPopulation = assignedPop;
    }
}
