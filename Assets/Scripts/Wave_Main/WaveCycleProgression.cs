using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 누적 일차를 현재 주기와 주기 내 웨이브로 해석하고 진행 이벤트를 발행한다.
/// </summary>
public class WaveCycleProgression : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private WaveCycleScheduleSO _schedule;
    [SerializeField] private UnityEvent<int> _cycleStarted = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> _cycleCompleted = new UnityEvent<int>();
    [SerializeField] private UnityEvent _allCyclesCompleted = new UnityEvent();
    [SerializeField] private UnityEvent<int> _dayWaveResolved = new UnityEvent<int>();

    public bool HasCurrentSnapshot { get; private set; }
    public WaveCycleSnapshot CurrentSnapshot { get; private set; }

    /// <summary>
    /// 현재 주기 번호. 스냅샷이 아직 없으면(첫 낮 시작 전·일정 데이터 누락) 첫 주기로 간주한다.
    /// 주기에 따라 잠금이 풀리는 시스템(연구 티어 등)이 스냅샷 구조체를 몰라도 되도록 노출한다.
    /// </summary>
    public int CurrentCycleNumber =>
        HasCurrentSnapshot
            ? CurrentSnapshot.CycleNumber
            : WaveCycleRules.FIRST_CYCLE_NUMBER;
    public UnityEvent<int> CycleStarted => _cycleStarted;
    public UnityEvent<int> CycleCompleted => _cycleCompleted;
    public UnityEvent AllCyclesCompleted => _allCyclesCompleted;

    /// <summary>
    /// 오늘 일차의 스냅샷(웨이브 편성·주기)이 확정된 뒤 발행된다.
    /// CycleStarted보다 뒤에 발행되므로 구독자는 Portal.IsActive가 갱신된 상태를 본다.
    /// </summary>
    public UnityEvent<int> DayWaveResolved => _dayWaveResolved;

    private void OnEnable()
    {
        if (!WiringGuard.Require(_cycleManager, nameof(_cycleManager), this))
        {
            return;
        }

        _cycleManager.OnDayReady.AddListener(HandleDayStart);
        _cycleManager.OnNightEnd.AddListener(HandleNightEnd);

        if (_cycleManager.CurrentDayNumber >= WaveCycleRules.FIRST_CYCLE_NUMBER)
        {
            HandleDayStart(_cycleManager.CurrentDayNumber);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayReady.RemoveListener(HandleDayStart);
        _cycleManager.OnNightEnd.RemoveListener(HandleNightEnd);
    }

    public bool TryGetCurrentWaveDefinition(
        out WaveDefinitionSO waveDefinition)
    {
        if (!HasCurrentSnapshot)
        {
            waveDefinition = null;
            return false;
        }

        waveDefinition = CurrentSnapshot.WaveDefinition;
        return waveDefinition != null;
    }

    private void HandleDayStart(int currentDay)
    {
        if (!WiringGuard.Require(_schedule, nameof(_schedule), this))
        {
            HasCurrentSnapshot = false;
            return;
        }

        if (!_schedule.TryResolve(currentDay, out WaveCycleSnapshot nextSnapshot))
        {
            Debug.LogError(
                $"[WaveCycleProgression] {currentDay}일차에 해당하는 주기·웨이브 데이터가 없습니다.",
                this);
            HasCurrentSnapshot = false;
            return;
        }

        bool isNewCycle =
            !HasCurrentSnapshot ||
            CurrentSnapshot.CycleNumber != nextSnapshot.CycleNumber;

        CurrentSnapshot = nextSnapshot;
        HasCurrentSnapshot = true;

        if (isNewCycle)
        {
            _cycleStarted?.Invoke(nextSnapshot.CycleNumber);
        }

        _dayWaveResolved?.Invoke(currentDay);
    }

    private void HandleNightEnd(int currentDay)
    {
        if (_schedule == null ||
            !_schedule.TryResolve(currentDay, out WaveCycleSnapshot completedSnapshot) ||
            !completedSnapshot.IsBossWave)
        {
            return;
        }

        _cycleCompleted?.Invoke(completedSnapshot.CycleNumber);

        if (completedSnapshot.IsFinalCycle)
        {
            _allCyclesCompleted?.Invoke();
        }
    }
}
