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

    private GameManager _gameManager;

    public UnityEvent<int> OnDayStart;
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
        SafeInvoke(OnDayStart, _gameManager.CurrentRun.CurrentCycle);
        CurrentCycle = CycleState.Day;
        SafeInvoke(OnCycleChanged, CycleState.Day);
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
