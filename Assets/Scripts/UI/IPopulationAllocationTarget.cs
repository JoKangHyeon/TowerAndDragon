public interface IPopulationAllocationTarget
{
    int AssignedPopulation {get;}   
    int Capacity {get;}
    int AvailableCapacity {get;}
    bool IsInitialized {get;}

    bool TryAssign (int amount);
    bool TryUnassign (int amount);
}
