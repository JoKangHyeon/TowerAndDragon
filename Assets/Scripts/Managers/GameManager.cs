using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public RunData CurrentRun;

    public CycleManager CycleManager;


    public List<CycleLight> DefaultLights;

    private bool _isGameOver;

    /// <summary>성이 파괴되어 게임오버가 되면 발생. 게임오버 UI 등이 구독한다.</summary>
    public event Action GameOverOccurred;

    private void Awake()
    {
        CycleManager.Construct(this);
    }

    private void Start()
    {
        foreach (CycleLight light in DefaultLights)
        {
            light.Construct(CycleManager);
        }
    }

    // 성이 파괴되면 성이 호출한다(중복 호출 무시). 실제 창 표시는 이벤트 구독자가 담당.
    public void GameOver()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        GameOverOccurred?.Invoke();
    }
}
