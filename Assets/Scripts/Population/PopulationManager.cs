using System;
using System.Collections.Generic;
using UnityEngine;

public class PopulationManager : MonoBehaviour
{
    [SerializeField] 
    [Min(0)]    
    private int _maxPopulation;
    private readonly List<PopulationAllocation> _allocations = new();
    public int MaxPopulation => _maxPopulation;
    public int AssignedPopulation
    {
        get
        {
            int total = 0;

            foreach (PopulationAllocation allocation in _allocations)
            {
                total += allocation.AssignedPopulation;
            }
            return total;
        }
    }

    public int AvailablePopulation =>
        MaxPopulation - AssignedPopulation;

    public PopulationState CurrentState =>
        new PopulationState(
            MaxPopulation,
            AssignedPopulation,
            AvailablePopulation
        );

    public event Action<PopulationState> PopulationChanged;
}
