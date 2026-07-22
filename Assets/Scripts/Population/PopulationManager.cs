using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 보유한 전체 인구와 모든 사용처의 할당 상태를 관리한다.
/// 외부 시스템은 Try API를 통해서만 인구를 배치·회수하고 변경 결과를 이벤트로 받는다.
/// </summary>
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

    public UnityEvent<PopulationState> PopulationChanged;

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

    /// <summary>
    /// 기아 사망을 가용, 타워, 생산, 점령 인구 순서로 적용한다.
    /// 각 분류 안에서는 등록된 할당 순서대로 한 명씩 순환하여 감소시킨다.
    /// </summary>
    public bool TryApplyStarvation(
        int requestedDeaths,
        out StarvationResult result
    )
    {
        result = default;

        if (requestedDeaths <= 0 || MaxPopulation == 0)
        {
            return false;
        }

        int remainingDeaths = Math.Min(requestedDeaths, MaxPopulation);
        int availablePopulationLost = Math.Min(
            remainingDeaths,
            AvailablePopulation
        );
        remainingDeaths -= availablePopulationLost;

        int towerPopulationLost = ReduceAssignedPopulation(
            PopulationAssignmentType.Tower,
            remainingDeaths
        );
        remainingDeaths -= towerPopulationLost;

        int productionPopulationLost = ReduceAssignedPopulation(
            PopulationAssignmentType.Production,
            remainingDeaths
        );
        remainingDeaths -= productionPopulationLost;

        int conquestPopulationLost = ReduceAssignedPopulation(
            PopulationAssignmentType.Conquest,
            remainingDeaths
        );

        result = new StarvationResult(
            requestedDeaths,
            availablePopulationLost,
            towerPopulationLost,
            productionPopulationLost,
            conquestPopulationLost
        );

        _maxPopulation -= result.PopulationLost;
        NotifyPopulationChanged();
        return true;
    }

    private int ReduceAssignedPopulation(
        PopulationAssignmentType assignmentType,
        int requestedDeaths
    )
    {
        int remainingDeaths = requestedDeaths;
        int populationLost = 0;

        while (remainingDeaths > 0)
        {
            bool reducedInCurrentRound = false;

            foreach (PopulationAllocation allocation in _allocations)
            {
                if (remainingDeaths == 0)
                {
                    break;
                }

                if (allocation.AssignmentType != assignmentType ||
                    allocation.AssignedPopulation == 0)
                {
                    continue;
                }

                allocation.Unassign(1);
                remainingDeaths -= 1;
                populationLost += 1;
                reducedInCurrentRound = true;
            }

            if (!reducedInCurrentRound)
            {
                break;
            }
        }

        return populationLost;
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
