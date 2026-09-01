using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 낮/밤 주기의 단일 소유자.
///
/// 낮 시작은 네 단계로 나뉜다. 앞 두 단계는 "정산"(실행할 때마다 자원이 변한다)이고,
/// 뒤 두 단계는 "정산 이후"(몇 번 실행해도 상태가 같다)다:
///   OnDayStart(생산) → OnDayStartUpkeep(소비) → OnDayReady(파생 상태·연출) → OnDaySettled(관측)
///
/// 이 경계가 이어하기 계약이다. 새 낮은 <see cref="StartDay"/>가 네 단계를 모두 태우고,
/// 이어하기는 <see cref="SeedRestoredDay"/> + <see cref="ResumeDay"/>로 뒤 두 단계만 재생한다 -
/// 세이브 스냅샷이 이미 정산 이후 상태이므로 정산을 재생하면 낮에 저장→로드를 반복할 때마다
/// 순생산분이 증식한다(SaveService 계약 2).
/// </summary>
public class CycleManager : MonoBehaviour
{
    public enum CycleState
    {
        Day,
        Night,
    }

    // 게임은 1일차부터 시작한다 - StartDay가 0에서 1로 올린다.
    //
    // public인 이유: "이 날이 새 런의 첫날인가"를 밖에서도 판정한다. 이어하기는
    // SeedRestoredDay + ResumeDay로 들어와 OnDayStart를 발화하지 않으므로, 이 일차의
    // OnDayStart는 곧 "새 런이 방금 시작됐다"와 같다(DragonEggInventorySystem이 쓴다).
    public const int FIRST_DAY_NUMBER = 1;

    // [임시 계측] 밤→낮 전환 프리즈 조사용 프로파일러 마커 - 이름에 TND. 접두사를 통일해
    // 프로파일러 Hierarchy 검색창에 "TND."만 치면 전환 단계 전체가 한 화면에 나열되게 한다.
    private const string END_NIGHT_ON_NIGHT_END_MARKER_NAME = "TND.EndNight.OnNightEnd";
    private const string END_NIGHT_START_DAY_MARKER_NAME = "TND.EndNight.StartDay";
    private const string START_DAY_ON_DAY_START_MARKER_NAME = "TND.StartDay.OnDayStart";
    private const string START_DAY_UPKEEP_MARKER_NAME = "TND.StartDay.Upkeep";
    private const string START_DAY_ON_DAY_READY_MARKER_NAME = "TND.StartDay.OnDayReady";
    private const string START_DAY_ON_CYCLE_CHANGED_MARKER_NAME = "TND.StartDay.OnCycleChanged";
    private const string START_DAY_ON_DAY_SETTLED_MARKER_NAME = "TND.StartDay.OnDaySettled";

    private static readonly ProfilerMarker END_NIGHT_ON_NIGHT_END_MARKER = new(END_NIGHT_ON_NIGHT_END_MARKER_NAME);
    private static readonly ProfilerMarker END_NIGHT_START_DAY_MARKER = new(END_NIGHT_START_DAY_MARKER_NAME);
    private static readonly ProfilerMarker START_DAY_ON_DAY_START_MARKER = new(START_DAY_ON_DAY_START_MARKER_NAME);
    private static readonly ProfilerMarker START_DAY_UPKEEP_MARKER = new(START_DAY_UPKEEP_MARKER_NAME);
    private static readonly ProfilerMarker START_DAY_ON_DAY_READY_MARKER = new(START_DAY_ON_DAY_READY_MARKER_NAME);
    private static readonly ProfilerMarker START_DAY_ON_CYCLE_CHANGED_MARKER = new(START_DAY_ON_CYCLE_CHANGED_MARKER_NAME);
    private static readonly ProfilerMarker START_DAY_ON_DAY_SETTLED_MARKER = new(START_DAY_ON_DAY_SETTLED_MARKER_NAME);

    [Tooltip("켜면 밤→낮 전환(EndNight 시작 ~ StartDay 종료)에 걸린 실제 시간을 로그로 남긴다 - " +
        "프로파일러를 띄우지 않고도 수정 전/후 숫자를 바로 비교하기 위한 임시 계측이다.")]
    [SerializeField]
    private bool _logsCycleTransitionTiming;

    private GameManager _gameManager;

    public UnityEvent<int> OnDayStart;

    // OnDayStart(생산 정산·상태 갱신) 이후, 반드시 그 뒤에 소비(인구 식량 유지비, 새끼용 슬라임 먹이 등)가
    // 일어나도록 분리한 단계. 기존 씬에 직렬화된 인스턴스에는 이 필드의 YAML 항목이 없으므로,
    // 구독 시점(OnEnable/Awake)에 null이 되지 않도록 직접 초기화한다.
    public UnityEvent<int> OnDayStartUpkeep = new();

    // 낮 시작 정산(OnDayStart 생산 + OnDayStartUpkeep 소비)이 모두 끝나 하루가 진행 가능해진 뒤 발화.
    // 다시 실행해도 게임 상태가 변하지 않는 파생 상태·연출 전용이며, 이어하기 복원도 이 단계만 재생한다.
    // 씬 YAML에 이 필드 항목이 없으므로 인라인 초기화가 필수다(OnDayStartUpkeep과 같은 이유).
    public UnityEvent<int> OnDayReady = new();

    // 낮 시작 처리 전부가 끝난 직후 1회. 상태를 관측만 하는 구독자(자동저장) 전용이며,
    // 여기서 게임 상태를 변경하면 안 된다.
    // 자동저장이 OnDayStart/OnDayStartUpkeep/OnDayReady에 직접 붙지 않는 이유: 같은 UnityEvent 안에서
    // 구독자 순서는 등록순(각 오브젝트의 OnEnable 순서)이라 보장되지 않아, 씬 오브젝트 순서를
    // 바꾸면 저장되는 내용이 조용히 달라진다. "정산이 끝난 경계"라는 시점을 여기서 명시적으로 만든다.
    // ResumeDay에서는 발화하지 않는다 - 이어하기 직후 같은 상태를 다시 저장할 이유가 없다.
    // 1일차에도 발화하지만 자동저장은 그 발화를 무시한다 - 게임오버 후 Restart가 씬을 다시 로드해
    // 1일차를 시작하므로, 저장하면 직전 런의 이어하기가 덮어써진다(SaveService.HandleDaySettled).
    // 씬 YAML에 이 필드 항목이 없으므로 인라인 초기화가 필수다(OnDayStartUpkeep과 같은 이유).
    public UnityEvent<int> OnDaySettled = new();

    public UnityEvent<int> OnDayEnd;
    public UnityEvent<int> OnNightStart;
    public UnityEvent<int> OnNightEnd;
    public UnityEvent<CycleState> OnCycleChanged;

    public CycleState CurrentCycle { get; private set; }

    public int CurrentDayNumber =>
        _gameManager != null ? _gameManager.CurrentRun.CurrentCycle : 0;

    // 기존 건설 코드와의 호환용 이름이다. 실제 값은 큰 단위 주기가 아니라 누적 일차다.
    public int CurrentCycleNumber => CurrentDayNumber;

    public void Construct(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void StartDay()
    {
        _gameManager.CurrentRun.CurrentCycle += 1;
        CurrentCycle = CycleState.Day;

        int day = _gameManager.CurrentRun.CurrentCycle;

        using (START_DAY_ON_DAY_START_MARKER.Auto())
        {
            SafeInvoke(OnDayStart, day);          // 생산 정산
        }

        using (START_DAY_UPKEEP_MARKER.Auto())
        {
            SafeInvoke(OnDayStartUpkeep, day);    // 소비 정산
        }

        EnterDay(day);                        // 파생 상태·연출 + OnCycleChanged

        using (START_DAY_ON_DAY_SETTLED_MARKER.Auto())
        {
            SafeInvoke(OnDaySettled, day);        // 관측 전용(자동저장)
        }
    }

    /// <summary>
    /// 이어하기 1단계. 이벤트 없이 일차·페이즈만 저장값으로 맞춘다.
    /// 복원 핸들러들이 일관된 일차를 보게 하려면 다른 복원보다 앞서야 한다.
    /// 저장 스냅샷은 이미 정산이 끝난 상태이므로 <see cref="StartDay"/>가 아니라
    /// <see cref="ResumeDay"/>로 이어진다 - 일차를 N-1로 시드하지 않는다.
    /// </summary>
    public void SeedRestoredDay(int savedDayNumber)
    {
        Debug.Assert(
            savedDayNumber >= FIRST_DAY_NUMBER,
            $"[CycleManager] 복원할 일차가 유효하지 않습니다: {savedDayNumber}");

        _gameManager.CurrentRun.CurrentCycle = savedDayNumber;
        CurrentCycle = CycleState.Day;
    }

    /// <summary>
    /// 이어하기 2단계. 정산을 건너뛰고 저장된 낮의 파생 상태·연출만 재생한다.
    /// 저장 시점이 이미 정산 이후이므로 생산·유지비·알 성장·성 회복·이동권을 다시 적용하면
    /// 낮에 저장→로드를 반복할 때마다 순생산분이 증식한다.
    /// </summary>
    public void ResumeDay()
    {
        EnterDay(_gameManager.CurrentRun.CurrentCycle);
    }

    // 정산이 끝난 낮에 "들어가는" 단계. 몇 번 실행해도 게임 상태가 같아야 하며,
    // StartDay와 ResumeDay가 공유한다.
    private void EnterDay(int day)
    {
        using (START_DAY_ON_DAY_READY_MARKER.Auto())
        {
            SafeInvoke(OnDayReady, day);
        }

        using (START_DAY_ON_CYCLE_CHANGED_MARKER.Auto())
        {
            SafeInvoke(OnCycleChanged, CycleState.Day);
        }
    }

    // 튜토리얼이 등록한다 - 아무도 등록하지 않은 씬에서는 비어 있어 언제나 밤으로 넘어간다(기존 동작 유지).
    //
    // 슬롯 하나가 아니라 목록인 이유: 막는 주체가 여럿이고(강제 안내 러너·새끼용 가이드·일일 목표)
    // 저마다 사는 기간이 다르다. 슬롯 하나를 서로 덮어쓰면 나중에 온 쪽이 앞의 잠금을 지우고,
    // 그쪽이 물러날 때 null로 되돌려 앞의 잠금까지 함께 풀려버린다.
    private readonly List<IDayEndBlockQuery> _dayEndBlockers = new();

    // 밤 진입이 관문에 막혔다. 막는 쪽이 직접 문구를 띄우지 않고 여기서 알리는 이유는,
    // 관문을 거는 컴포넌트가 여럿인데 그 전부에 알림 배선을 복제하게 되기 때문이다.
    // 씬 YAML에 이 필드 항목이 없으므로 인라인 초기화가 필수다(OnDayReady와 같은 이유).
    public UnityEvent DayEndBlocked = new();

    /// <summary>하나라도 막고 있으면 밤으로 넘어가지 않는다. 같은 대상을 두 번 넣어도 한 번만 등록된다.</summary>
    public void AddDayEndBlocker(IDayEndBlockQuery blocker)
    {
        if (blocker != null && !_dayEndBlockers.Contains(blocker))
        {
            _dayEndBlockers.Add(blocker);
        }
    }

    /// <summary>등록을 뗀다. 자기가 넣은 것만 빼므로 남의 잠금은 건드리지 않는다.</summary>
    public void RemoveDayEndBlocker(IDayEndBlockQuery blocker)
    {
        _dayEndBlockers.Remove(blocker);
    }

    /// <summary>
    /// 밤으로 넘어갈 수 있도록 관문을 연다. 접을 수 있는 관문은 여기서 접고
    /// (<see cref="IDayEndBlockRelaxQuery"/>), 그러고도 남은 관문이 있으면 거짓이다.
    ///
    /// 확인창처럼 <see cref="EndDay()"/> 앞에 끼어드는 UI가 "지금 누르면 밤이 오는가"를
    /// 미리 알아야 할 때 쓴다. 단순한 관문 조회를 두지 않는 이유는 그것이 함정이기 때문이다 -
    /// 접을 수 있는 관문 때문에 막힌 것까지 "막혔다"로 읽고 확인창을 건너뛰는데, 정작
    /// 그 뒤에 부른 EndDay는 관문을 접고 성공한다. 확인 없이 밤이 시작되는 경로가 그렇게 생긴다.
    ///
    /// 같은 프레임 안에서 이것이 참이면 곧이어 부르는 EndDay도 반드시 성공한다(관문은 스스로 닫히지 않는다).
    /// </summary>
    public bool TryOpenDayEndGate()
    {
        if (CanEndDay())
        {
            return true;
        }

        return TryRelaxDayEndBlockers() && CanEndDay();
    }

    private bool CanEndDay()
    {
        foreach (IDayEndBlockQuery blocker in _dayEndBlockers)
        {
            if (blocker != null && !blocker.CanEndDay())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 지금 밤 진입 확인창을 띄우면 플레이어가 그것을 조작할 수 있는지.
    /// 거짓이면 확인창을 건너뛰고 곧장 밤으로 보내야 한다 - 자세한 사유는
    /// <see cref="IDayEndConfirmBlockQuery"/>에 적어 두었다.
    /// </summary>
    public bool IsDayEndConfirmBlocked => !CanShowDayEndConfirm();

    // 접는 동안 _dayEndBlockers가 바뀐다 - 끝난 챕터가 빠지고 다음 챕터가 그 자리에서 들어온다.
    // 그대로 foreach를 돌면 컬렉션이 수정돼 터지므로 접을 대상을 먼저 모아 두고 돈다(버퍼는 재사용한다).
    private readonly List<IDayEndBlockRelaxQuery> _relaxBuffer = new();

    /// <summary>
    /// 접을 수 있는 관문을 접는다. <b>접을 수 없는 관문이 하나라도 막고 있으면 아무것도 건드리지 않고
    /// 물러난다</b> - 접는 것은 조회가 아니라 상태 변경이므로(안내가 읽을 틈을 잃고 다음 챕터가
    /// 앞당겨 열린다) 어차피 거절될 조작이 진행 상태를 바꾸면 안 된다.
    /// 목표가 남아 밤이 막힌 상태에서 버튼을 눌렀더니 안내만 건너뛰어진 적이 있다.
    ///
    /// <b>다만 "접으면 밤이 온다"까지는 보장하지 않는다</b> - 접는 도중 태어나는 관문은 미리 볼 수 없다.
    /// 인계를 접으면 그 자리에서 TutorialEnded가 다음 챕터를 열고, 그 챕터가 관문을 다시 건다.
    /// 그때는 읽을 틈만 접히고 밤은 오지 않는다.
    /// 그래도 되는 이유는 두 가지다. 다음 챕터가 할 일을 주는 중이므로 막히는 것이 맞고
    /// (<see cref="IDayEndBlockRelaxQuery.TryRelaxDayEndBlock"/>), 인계 구간에는 이미 화면이 걷혀 있어
    /// 플레이어가 눈으로 잃는 안내도 없다. 같은 날 안에서 챕터가 갈릴 때만 해당한다 -
    /// 그 날의 마지막 챕터면 다음 챕터가 제 일차를 기다리며 열리지 않으므로 밤이 온다.
    ///
    /// 막고 있지 않은 관문에도 접기를 청한다. 접을 것이 없으면 거짓을 돌려주므로 무해하고,
    /// 막고 있는지를 따로 묻는 것보다 판정이 한 곳에 모인다.
    /// </summary>
    private bool TryRelaxDayEndBlockers()
    {
        _relaxBuffer.Clear();

        foreach (IDayEndBlockQuery blocker in _dayEndBlockers)
        {
            if (blocker == null)
            {
                continue;
            }

            if (blocker is IDayEndBlockRelaxQuery relaxable)
            {
                _relaxBuffer.Add(relaxable);
                continue;
            }

            if (!blocker.CanEndDay())
            {
                _relaxBuffer.Clear();
                return false;
            }
        }

        bool hasRelaxed = false;

        foreach (IDayEndBlockRelaxQuery relaxable in _relaxBuffer)
        {
            if (relaxable.TryRelaxDayEndBlock())
            {
                hasRelaxed = true;
            }
        }

        _relaxBuffer.Clear();
        return hasRelaxed;
    }

    private bool CanShowDayEndConfirm()
    {
        foreach (IDayEndBlockQuery blocker in _dayEndBlockers)
        {
            if (blocker is IDayEndConfirmBlockQuery confirmQuery && !confirmQuery.CanShowDayEndConfirm())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [디버그 전용] 관문을 무시하고 밤으로 넘긴다. 목표 관문은 낮 내내 등록돼 있어
    /// 평범한 EndDay로는 목표를 다 채우기 전까지 넘어갈 수 없다 - 건너뛰기 도구는 그것을 지나야 한다.
    /// </summary>
    public void ForceEndDay()
    {
        EndDay(ignoresBlockers: true);
    }

    public void EndDay()
    {
        EndDay(ignoresBlockers: false);
    }

    private void EndDay(bool ignoresBlockers)
    {
        // 밤 시작은 되돌릴 수 없으므로 버튼이 아니라 이 관문에서 막는다 - 다른 진입 경로가 생겨도 함께 막힌다.
        if (!ignoresBlockers && !TryOpenDayEndGate())
        {
            // 왜 안 눌리는지 알려주지 않으면 버튼이 고장 난 것으로 보인다.
            DayEndBlocked.Invoke();
            return;
        }

        SafeInvoke(OnDayEnd, _gameManager.CurrentRun.CurrentCycle);
        StartNight();
    }

    public void StartNight()
    {
        CurrentCycle = CycleState.Night;
        SafeInvoke(OnNightStart, _gameManager.CurrentRun.CurrentCycle);
        SafeInvoke(OnCycleChanged, CycleState.Night);
    }

    public void EndNight()
    {
        // [임시 계측] 프로파일러 없이도 수정 전/후 전환 소요 시간을 바로 비교하기 위한 스톱워치.
        System.Diagnostics.Stopwatch transitionStopwatch =
            _logsCycleTransitionTiming ? System.Diagnostics.Stopwatch.StartNew() : null;

        using (END_NIGHT_ON_NIGHT_END_MARKER.Auto())
        {
            SafeInvoke(OnNightEnd, _gameManager.CurrentRun.CurrentCycle);
        }

        if (_gameManager.IsGameEnded)
        {
            return;
        }

        using (END_NIGHT_START_DAY_MARKER.Auto())
        {
            StartDay();
        }

        if (transitionStopwatch != null)
        {
            Debug.Log($"[CycleManager] 밤→낮 전환 소요 시간: {transitionStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        }
    }

    public void DebugCycle()
    {
        if(CurrentCycle == CycleState.Day)
        {
            EndDay();
        }
        else
        {
            EndNight();
        }
    }

    // 구독자(정산 UI 등) 중 하나가 예외를 던져도 낮/밤 전환 자체(다음 단계 호출, CurrentCycle 갱신,
    // 조명 등 나머지 시스템)는 멈추지 않도록 각 이벤트 발행을 격리한다.
    private static void SafeInvoke<T>(UnityEvent<T> unityEvent, T arg)
    {
        try
        {
            unityEvent?.Invoke(arg);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
