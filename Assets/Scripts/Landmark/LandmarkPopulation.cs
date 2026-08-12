using UnityEngine;

// 랜드마크에 투입된 인구. ResearchLabPopulation과 같은 구조지만, 랜드마크는 배치 건물이 아니라
// LandmarkManager가 테이블을 보고 만든 오브젝트이므로 GridMap.OnBuildingAdded가 아니라
// 매니저가 직접 Initialize를 호출한다(ConquestPopulationCoordinator가 건물 없이 할당을
// 만드는 것과 같은 방식).
[RequireComponent(typeof(Landmark))]
public sealed class LandmarkPopulation : MonoBehaviour, IPopulationAllocationTarget
{
    private PopulationManager _populationManager;
    private CycleManager _cycleManager;
    private LandmarkManager _landmarkManager;
    private Landmark _landmark;
    private PopulationAllocation _allocation;
    private bool _isInitialized;

    public int AssignedPopulation => _allocation?.AssignedPopulation ?? 0;
    public int Capacity => _allocation?.Capacity ?? 0;
    public int AvailableCapacity => _allocation?.AvailableCapacity ?? 0;
    public bool IsInitialized => _isInitialized;

    public bool Initialize(
        PopulationManager populationManager,
        CycleManager cycleManager,
        LandmarkManager landmarkManager)
    {
        if (_isInitialized || populationManager == null || cycleManager == null)
        {
            return false;
        }

        Landmark landmark = GetComponent<Landmark>();
        if (landmark == null || landmark.Data == null || !landmark.Data.IsOperable)
        {
            return false;
        }

        if (!populationManager.TryCreateAllocation(
            PopulationAssignmentType.Landmark,
            landmark.Data.PopulationCapacity,
            out _allocation))
        {
            return false;
        }

        _landmark = landmark;
        _populationManager = populationManager;
        _cycleManager = cycleManager;
        _landmarkManager = landmarkManager;
        _isInitialized = true;
        return true;
    }

    // 배치가 실제로 바뀐 경우에만 알린다 - 실패한 요청까지 알리면 마커가 매 클릭마다 다시 그려진다.
    public bool TryAssign(int amount)
    {
        if (!CanChangePopulation() || !_populationManager.TryAssign(_allocation, amount))
        {
            return false;
        }

        _landmarkManager?.NotifyOperationChanged();
        return true;
    }

    public bool TryUnassign(int amount)
    {
        if (!CanChangePopulation() || !_populationManager.TryUnassign(_allocation, amount))
        {
            return false;
        }

        _landmarkManager?.NotifyOperationChanged();
        return true;
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
        _landmarkManager = null;
        _landmark = null;
        _isInitialized = false;
        return true;
    }

    // 연구소와 달리 점령 조건이 하나 더 붙는다 - 아직 점령하지 않은 랜드마크는 가동할 수 없다.
    private bool CanChangePopulation()
    {
        return _isInitialized &&
            _landmark != null &&
            _landmark.IsConquered &&
            _cycleManager != null &&
            _cycleManager.CurrentCycle == CycleManager.CycleState.Day;
    }
}
