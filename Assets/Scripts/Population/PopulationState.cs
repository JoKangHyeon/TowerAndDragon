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

    /// <summary>어디에도 배치되지 않아 놀고 있는 시민이 있는지. 놀아도 식량은 먹으므로 경고 대상이다.</summary>
    public bool HasIdlePopulation =>
        AvailablePopulation > 0;

    public PopulationState(
        int maxPop,
        int assignedPop
    )
    {
        MaxPopulation = maxPop;
        AssignedPopulation = assignedPop;
    }
}
