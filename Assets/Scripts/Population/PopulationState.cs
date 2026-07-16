public readonly struct PopulationState
{
    public int MaxPopulation {get;}
    public int AssignedPopulation {get;}    
    public int AvailablePopulation {get;}

    public PopulationState(
        int maxPop,
        int assignedPop,
        int availablePop
    )
    {
        MaxPopulation = maxPop;
        AssignedPopulation = assignedPop;           
        AvailablePopulation = availablePop;
    }
}