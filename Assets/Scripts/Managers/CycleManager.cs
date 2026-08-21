using System;
using System.Collections.Generic;
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
    private const int FIRST_DAY_NUMBER = 1;

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

        SafeInvoke(OnDayStart, day);          // 생산 정산
        SafeInvoke(OnDayStartUpkeep, day);    // 소비 정산
        EnterDay(day);                        // 파생 상태·연출 + OnCycleChanged
        SafeInvoke(OnDaySettled, day);        // 관측 전용(자동저장)
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
        SafeInvoke(OnDayReady, day);
        SafeInvoke(OnCycleChanged, CycleState.Day);
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
    /// 지금 밤으로 넘어가는 것이 관문에 막혀 있는지. 확인창처럼 EndDay 앞에 끼어드는 UI가
    /// "제 문구보다 막힌 이유를 먼저 보여줘야 하는 상황"을 구분하는 데 쓴다 -
    /// 막혀 있는데 확인창부터 띄우면, 확인을 눌러도 아무 일이 없는 것처럼 보인다.
    /// </summary>
    public bool IsDayEndBlocked => !CanEndDay();

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
        if (!ignoresBlockers && !CanEndDay())
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
        SafeInvoke(OnNightEnd, _gameManager.CurrentRun.CurrentCycle);

        if (_gameManager.IsGameEnded)
        {
            return;
        }
        
        StartDay();
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
