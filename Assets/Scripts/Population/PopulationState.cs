public readonly struct PopulationState
{
    public int MaxPopulation {get;}
    public int AssignedPopulation {get;}    
    public int AvailablePopulation =>
        MaxPopulation - AssignedPopulation;

    public PopulationState(
        int maxPop,
        int assignedPop,
        int availablePop
    )
    {
        MaxPopulation = maxPop;
        AssignedPopulation = assignedPop;
    }
}