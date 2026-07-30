using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 아침 정산 중 유지비(소비) 단계를 담당한다. CycleManager.OnDayStartUpkeep은
/// OnDayStart(생산 정산) 다음에 발화하므로, 당일 생산분이 반영된 재고를 기준으로 소비한다.
/// 각 단계의 세부 계산과 상태 변경은 전용 시스템에 위임한다.
/// </summary>
public class DailySettlementManager : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private PopulationUpkeepSystem _populationUpkeepSystem;

    public UnityEvent<int, PopulationUpkeepResult> SettlementCompleted;

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStartUpkeep.AddListener(Settle);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStartUpkeep.RemoveListener(Settle);
        }
    }

    private void Settle(int currentDay)
    {
        if (_populationUpkeepSystem == null ||
            !_populationUpkeepSystem.TrySettle(out PopulationUpkeepResult result))
        {
            return;
        }

        SettlementCompleted?.Invoke(currentDay, result);
    }
}
