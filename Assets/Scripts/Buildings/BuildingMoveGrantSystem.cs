using UnityEngine;

/// <summary>
/// 건물 이동을 연구 기반 전역 일일 예산으로 제한한다. 매일 낮 시작 시 사용량이 리셋되고,
/// 완료된 연구(convenience_tower_move 등)가 늘려주는 허용 횟수 안에서만 이동을 허용한다.
/// </summary>
public sealed class BuildingMoveGrantSystem : MonoBehaviour, IBuildingMoveGrantQuery
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    private int _usedToday;

    // HasRemainingMoveGrant는 매번 ResearchManager.GetMoveAllowance()를 다시 조회하므로,
    // 건물이 들고 있는 이 쿼리 참조 자체는 연구 완료 시 다시 주입할 필요가 없다.
    public bool HasRemainingMoveGrant =>
        _researchManager != null && _usedToday < _researchManager.GetMoveAllowance();

    public void ConsumeMoveGrant()
    {
        _usedToday++;
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }

        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }

        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
    }

    private void HandleDayStart(int currentDay)
    {
        _usedToday = 0;
    }

    private void HandleBuildingAdded(Building building)
    {
        building.SetMoveGrantQuery(this);
    }

    private void HandleBuildingRemoving(Building building)
    {
        building.SetMoveGrantQuery(null);
    }
}
