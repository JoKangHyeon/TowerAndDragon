using UnityEngine;

/// <summary>
/// 타워 한 개의 인구 할당을 관리한다.
/// 인구 변경은 PopulationManager를 통해서만 수행한다.
/// </summary>
[RequireComponent(typeof(Tower))]
public class TowerPopulation : MonoBehaviour, IPopulationAllocationTarget, ITowerStaffing
{
    private Tower _tower;
    private PopulationManager _populationManager;
    private PopulationAllocation _allocation;
    private bool _isInitialized;



    public int Capacity =>
        _allocation?.Capacity ?? 0;

    public int SoulPopulation =>
        _tower != null && _tower.AuraSystem != null
        ? _tower.AuraSystem.ResolveModifiers(_tower).SoulPopulation
        : 0;

    public int RealAssignedPopulation =>
        _allocation?.AssignedPopulation ?? 0;

    public int AssignedPopulation =>
        Mathf.Min(Capacity, RealAssignedPopulation + SoulPopulation);

    public int AvailableCapacity =>
        Mathf.Max(0, Capacity - AssignedPopulation);

    public float StaffingRatio =>
        Capacity == 0 ? 0f : Mathf.Clamp01((float)AssignedPopulation / Capacity);

    public bool HasAssignedPopulation => AssignedPopulation > 0f;

    public bool IsInitialized => _isInitialized;

    public bool CanOperate => IsInitialized && HasAssignedPopulation;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
    }

    public bool Initialize(PopulationManager populationManager)
    {
        if (_isInitialized || populationManager == null)
        {
            return false;
        }

        if (_tower == null || _tower.Data == null)
        {
            return false;
        }

        bool created = populationManager.TryCreateAllocation(
            PopulationAssignmentType.Tower,
            _tower.Data.PopulationCapacity,
            out _allocation);

        if (!created)
        {
            return false;
        }

        _populationManager = populationManager;
        _isInitialized = true;
        return true;
    }

    public bool TryAssign(int amount)
    {
        return _isInitialized &&
            !_tower.IsSuspended &&
            _populationManager.TryAssign(_allocation, amount);
    }

    public bool TryUnassign(int amount)
    {
        return _isInitialized &&
            _populationManager.TryUnassign(_allocation, amount);
    }

    public bool Release()
    {
        if (!_isInitialized)
        {
            return false;
        }

        if (!_populationManager.TryRemoveAllocation(_allocation))
        {
            return false;
        }

        _allocation = null;
        _populationManager = null;
        _isInitialized = false;
        return true;
    }
}
