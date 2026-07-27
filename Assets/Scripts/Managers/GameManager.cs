using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

    [SerializeField]
    private ResourceManager _resourceManager;

    [SerializeField]
    private SkillManager _skillManager;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private ResearchManager _researchManager;

    [SerializeField]
    private DragonTreeManager _dragonTreeManager;

    public RunData CurrentRun => _currentRun;
    public CycleManager CycleManager => _cycleManager;
    public List<CycleLight> DefaultLights => _defaultLights;
    public ResourceManager ResourceManager => _resourceManager;
    public SkillManager SkillManager => _skillManager;
    public ResearchManager ResearchManager => _researchManager;
    public DragonTreeManager DragonTreeManager => _dragonTreeManager;

    /// <summary>성이 파괴되어 게임오버가 되면 발생. 게임오버 UI 등이 구독한다.</summary>
    public UnityEvent GameOverOccurred;

    private bool _isGameOver;


    private void Awake()
    {
        _cycleManager.Construct(this);

        if (_skillManager != null)
        {
            _skillManager.Construct(_cycleManager);
        }

        if (_researchManager != null)
        {
            _researchManager.Construct(_cycleManager, _resourceManager, _gridMap);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.Construct(this, _cycleManager, _resourceManager);
        }
    }

    private void Start()
    {
        foreach (CycleLight light in _defaultLights)
        {
            light.Construct(_cycleManager);
        }

        // OnEnable 단계에서 이미 구독을 마친 ResourceChanged 리스너(UI 등)가 최초 자원 지급까지
        // 받을 수 있도록, Awake가 아닌 Start에서 Construct한다.
        if (_resourceManager != null)
        {
            _resourceManager.Construct(this);
        }

        // 실행 시 Day 1 시작
        _cycleManager.StartDay();
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
