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
            AssignedPopulation
        );

    public event Action<PopulationState> PopulationChanged;

    public bool TryCreateAllocation(
        PopulationAssignmentType assignmentType,
        int capacity,
        out PopulationAllocation allocation
    )
    {
        allocation = null;

        if (assignmentType == PopulationAssignmentType.None)
        {
            return false;
        }

        if (capacity <= 0)
        {
            return false;
        }

        allocation = new PopulationAllocation(
            assignmentType,
            capacity
        );

        _allocations.Add(allocation);
        return true;
    }

    public bool TryAssign(
        PopulationAllocation allocation,
        int amount
    )
    {
        if (!IsRegistered(allocation))
        {
            return false;
        }

        if (amount <= 0)
        {
            return false;
        }

        if (amount > allocation.AvailableCapacity)
        {
            return false;
        }

        if (amount > AvailablePopulation)
        {
            return false;
        }

        allocation.Assign(amount);
        NotifyPopulationChanged();
        return true;
    }

    public bool TryUnassign(
        PopulationAllocation allocation,
        int amount  
    )
    {
        if (!IsRegistered(allocation))
        {
            return false;
        }

        if (amount <= 0 || amount > allocation.AssignedPopulation)
        {
            return false;
        }

        allocation.Unassign(amount);
        NotifyPopulationChanged();
        return true;
    }

    private void NotifyPopulationChanged()
    {
        PopulationChanged?.Invoke(CurrentState);
    }

    public bool TryIncreaseMaxPopulation (int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        _maxPopulation += amount;
        NotifyPopulationChanged();
        return true;
    }

    public bool TryReleaseAll(
        PopulationAllocation allocation
    )
    {
        if (!IsRegistered(allocation))
        {
            return false;
        }

        if (allocation.AssignedPopulation == 0)
        {
            return true;
        }

        allocation.Unassign(
            allocation.AssignedPopulation
        );

        NotifyPopulationChanged();
        return true;
    }

    public bool TryRemoveAllocation(
        PopulationAllocation allocation
    )
    {
        if (!IsRegistered(allocation))
        {
            return false;
        }

        TryReleaseAll(allocation);
        _allocations.Remove(allocation);
        return true;
    }

    private bool IsRegistered(
        PopulationAllocation allocation
    )
    {
        return allocation != null &&
            _allocations.Contains(allocation);
    }
}
