using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("CurrentRun")]
    private RunData _currentRun;

    [SerializeField]
    [FormerlySerializedAs("CycleManager")]
    private CycleManager _cycleManager;

    [SerializeField]
    [FormerlySerializedAs("DefaultLights")]
    private List<CycleLight> _defaultLights;

    public RunData CurrentRun => _currentRun;
    public CycleManager CycleManager => _cycleManager;
    public List<CycleLight> DefaultLights => _defaultLights;

    private bool _isGameOver;

    /// <summary>성이 파괴되어 게임오버가 되면 발생. 게임오버 UI 등이 구독한다.</summary>
    public event Action GameOverOccurred;

    private void Awake()
    {
        _cycleManager.Construct(this);
    }

    private void Start()
    {
        foreach (CycleLight light in _defaultLights)
        {
            light.Construct(_cycleManager);
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
