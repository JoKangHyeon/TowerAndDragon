using System;
using UnityEngine;
using UnityEngine.Events;

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

    // 일차가 증가하고 낮으로 전환된 직후, 어떤 낮 시작 "처리"보다도 먼저 발화한다.
    // 상태를 관측만 하는 구독자(자동저장) 전용 - 여기서 게임 상태를 변경하면 안 된다.
    // 자동저장이 OnDayStart/OnDayStartUpkeep에 직접 붙지 않는 이유: 같은 UnityEvent 안에서
    // 구독자 순서는 등록순(각 오브젝트의 OnEnable 순서)이라 보장되지 않아, 씬 오브젝트 순서를
    // 바꾸면 저장되는 내용이 조용히 달라진다. "정산 전 경계"라는 시점을 여기서 명시적으로 만든다.
    // OnDayStartUpkeep(:20)과 같은 이유로 인라인 초기화가 필수다(씬 YAML에 이 필드 항목이 없다).
    public UnityEvent<int> OnDayAdvanced = new();

    public UnityEvent<int> OnDayStart;

    // OnDayStart(생산 정산·상태 갱신) 이후, 반드시 그 뒤에 소비(인구 식량 유지비, 새끼용 슬라임 먹이 등)가
    // 일어나도록 분리한 단계. 기존 씬에 직렬화된 인스턴스에는 이 필드의 YAML 항목이 없으므로,
    // 구독 시점(OnEnable/Awake)에 null이 되지 않도록 직접 초기화한다.
    public UnityEvent<int> OnDayStartUpkeep = new();
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
        SafeInvoke(OnDayAdvanced, _gameManager.CurrentRun.CurrentCycle);
        SafeInvoke(OnDayStart, _gameManager.CurrentRun.CurrentCycle);
        SafeInvoke(OnDayStartUpkeep, _gameManager.CurrentRun.CurrentCycle);
        SafeInvoke(OnCycleChanged, CycleState.Day);
    }

    /// <summary>
    /// 이어하기 전용. 저장된 일차 N을 "아직 N일차 처리를 하지 않은" 상태로 시드한다.
    /// 호출자는 나머지 복원을 마친 뒤 <see cref="StartDay"/>를 호출해야 하며, 그 StartDay가 N일차를 만든다.
    ///
    /// 세이브 스냅샷은 OnDayAdvanced 시점(= OnDayStart 발화 이전)에 캡처되므로, 이렇게 해야
    /// 생산 지급·식량 유지비·알 성장이 정확히 한 번만 실행된다. 여기서 일차를 N으로 바로 넣고
    /// StartDay를 부르면 N+1일차가 되고, StartDay를 부르지 않으면 조명·UI·웨이브 스냅샷·포탈 개방이
    /// 전부 초기 상태로 남는다.
    /// </summary>
    public void RestoreDay(int savedDayNumber)
    {
        Debug.Assert(
            savedDayNumber >= FIRST_DAY_NUMBER,
            $"[CycleManager] 복원할 일차가 유효하지 않습니다: {savedDayNumber}");

        _gameManager.CurrentRun.CurrentCycle = savedDayNumber - 1;
    }

    public void EndDay()
    {
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
