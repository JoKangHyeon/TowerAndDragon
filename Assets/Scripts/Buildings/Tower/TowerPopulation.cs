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

    public int AssignedPopulation => ResolveAssignedPopulation(Capacity);

    public int AvailableCapacity
    {
        get
        {
            int capacity = Capacity;
            return Mathf.Max(0, capacity - ResolveAssignedPopulation(capacity));
        }
    }

    public float StaffingRatio
    {
        get
        {
            int capacity = Capacity;
            return capacity == 0
                ? 0f
                : Mathf.Clamp01((float)ResolveAssignedPopulation(capacity) / capacity);
        }
    }

    public bool HasAssignedPopulation => AssignedPopulation > 0;

    // 정원을 인자로 받아 호출부가 한 번만 읽게 한다. Capacity는 연구 정원 보정
    // (ResearchManager.ResolveCapacity)을 읽을 때마다 다시 계산하는 파생값이라,
    // 프로퍼티끼리 서로를 부르면 한 판정에 같은 계산이 두세 번 돈다 -
    // StaffingRatio·CanOperate는 TowerAttack.Update가 프레임마다 지나는 경로다.
    private int ResolveAssignedPopulation(int capacity) =>
        Mathf.Min(capacity, RealAssignedPopulation + SoulPopulation);

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
