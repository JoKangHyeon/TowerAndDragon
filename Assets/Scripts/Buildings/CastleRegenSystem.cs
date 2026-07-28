using UnityEngine;

/// <summary>
/// convenience_castle_regen_1 연구 완료 시 매일 낮 시작마다 성 체력을 자동 회복시킨다.
/// PopulationUpkeepSystem·BabyDragonBuffSystem과 같은 독립 시스템 컴포넌트 스타일을 따른다 -
/// Castle에 CycleManager 참조를 새로 넣지 않는다.
/// </summary>
public sealed class CastleRegenSystem : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private Castle _castle;
    [SerializeField] private ResearchManager _researchManager;

    private void OnEnable()
    {
        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayStart.AddListener(HandleDayStart);
    }

    private void OnDisable()
    {
        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
    }

    private void HandleDayStart(int currentDay)
    {
        if (_castle == null || _researchManager == null)
        {
            return;
        }

        float regenAmount = _researchManager.GetCastleDailyRegenAmount();

        if (regenAmount > 0f)
        {
            _castle.Repair(regenAmount);
        }
    }
}
