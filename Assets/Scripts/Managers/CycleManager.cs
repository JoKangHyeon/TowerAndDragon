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

    public void Construct(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void StartDay()
    {
        _gameManager.CurrentRun.currentCycle += 1;
        OnDayStart?.Invoke(_gameManager.CurrentRun.currentCycle);
        CurrentCycle = CycleState.Day;
        OnCycleChanged?.Invoke(CycleState.Day);
    }

    public void EndDay()
    {
        OnDayEnd?.Invoke(_gameManager.CurrentRun.currentCycle);
        StartNight();
    }

    public void StartNight()
    {
        OnNightStart?.Invoke(_gameManager.CurrentRun.currentCycle);
        CurrentCycle = CycleState.Night;
        OnCycleChanged?.Invoke(CycleState.Night);
    }

    public void EndNight()
    {
        StartDay();
    }
}
