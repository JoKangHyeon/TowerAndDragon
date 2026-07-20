using System;
using UnityEngine;

/// <summary>
/// 아침 정산의 실행 시점과 단계별 호출 순서를 관리한다.
/// 각 단계의 세부 계산과 상태 변경은 전용 시스템에 위임한다.
/// </summary>
public class DailySettlementManager : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private PopulationUpkeepSystem _populationUpkeepSystem;

    public event Action<int, PopulationUpkeepResult> SettlementCompleted;

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(Settle);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(Settle);
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
