using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 생산시설 한 개의 인구 할당을 관리한다.
/// 인구 변경은 PopulationManager를 통해서만 수행한다.
/// </summary>
[RequireComponent(typeof(Factory))]
public class FactoryPopulation : MonoBehaviour, IPopulationAllocationTarget
{
    private Factory _factory;
    private PopulationManager _populationManager;
    private PopulationAllocation _allocation;
    private bool _isInitialized;

    public int AssignedPopulation =>
        _allocation?.AssignedPopulation ?? 0;

    public int Capacity =>
        _allocation?.Capacity ?? 0;

    public int AvailableCapacity =>
        _allocation?.AvailableCapacity ?? 0;

    public float StaffingRatio =>
        _allocation?.StaffingRatio ?? 0f;

    public bool HasAssignedPopulation => AssignedPopulation > 0f;

    public bool IsInitialized => _isInitialized;

    public UnityEvent OnPopulationChanged;
        
    private void Awake()
    {
        _factory = GetComponent<Factory>();
    }

    public bool Initialize(PopulationManager populationManager)
    {
        if (_isInitialized || populationManager == null)
        {
            return false;
        }

        if (_factory == null || _factory.Data == null)
        {
            return false;
        }

        bool created = populationManager.TryCreateAllocation(
            PopulationAssignmentType.Production,
            _factory.Data.PopulationCapacity,
            out _allocation);

        if (!created)
        {
            return false;
        }

        _populationManager = populationManager;
        _isInitialized = true;
        OnPopulationChanged?.Invoke();
        return true;
    }

    public bool TryAssign(int amount)
    {
        bool result = _isInitialized && _populationManager.TryAssign(_allocation, amount);
        OnPopulationChanged?.Invoke();
        return result;
    }

    public bool TryUnassign(int amount)
    {
        bool result = _isInitialized && _populationManager.TryUnassign(_allocation, amount);
        OnPopulationChanged?.Invoke();
        return result;
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
