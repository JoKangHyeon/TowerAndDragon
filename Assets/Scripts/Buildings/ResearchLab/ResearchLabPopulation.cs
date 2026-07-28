using UnityEngine;

[RequireComponent(typeof(ResearchLab))]
public sealed class ResearchLabPopulation : MonoBehaviour, IPopulationAllocationTarget
{
    private PopulationManager _populationManager;
    private CycleManager _cycleManager;
    private PopulationAllocation _allocation;
    private bool _isInitialized;

    public int AssignedPopulation => _allocation?.AssignedPopulation ?? 0;
    public int Capacity => _allocation?.Capacity ?? 0;
    public int AvailableCapacity => _allocation?.AvailableCapacity ?? 0;
    public bool IsInitialized => _isInitialized;

    public bool Initialize(
        PopulationManager populationManager,
        CycleManager cycleManager)
    {
        if (_isInitialized || populationManager == null || cycleManager == null)
        {
            return false;
        }

        ResearchLab lab = GetComponent<ResearchLab>();
        if (lab == null || lab.Data == null)
        {
            return false;
        }

        if (!populationManager.TryCreateAllocation(
            PopulationAssignmentType.Research,
            lab.Data.PopulationCapacity,
            out _allocation))
        {
            return false;
        }

        _populationManager = populationManager;
        _cycleManager = cycleManager;
        _isInitialized = true;
        return true;
    }

    public bool TryAssign(int amount)
    {
        return CanChangePopulation() &&
            _populationManager.TryAssign(_allocation, amount);
    }

    public bool TryUnassign(int amount)
    {
        return CanChangePopulation() &&
            _populationManager.TryUnassign(_allocation, amount);
    }

    public bool Release()
    {
        if (!_isInitialized || !_populationManager.TryRemoveAllocation(_allocation))
        {
            return false;
        }

        _allocation = null;
        _populationManager = null;
        _cycleManager = null;
        _isInitialized = false;
        return true;
    }

    private bool CanChangePopulation()
    {
        return _isInitialized &&
            _cycleManager != null &&
            _cycleManager.CurrentCycle == CycleManager.CycleState.Day;
    }
}
