using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    public enum GameResult
    {
        None,
        Victory,
        Defeat,
    }

    [SerializeField]
    [FormerlySerializedAs("CurrentRun")]
    private RunData _currentRun;

    [SerializeField]
    [FormerlySerializedAs("CycleManager")]
    private CycleManager _cycleManager;

    [SerializeField]
    [FormerlySerializedAs("DefaultLights")]
    private List<CycleLight> _defaultLights = new();

    [SerializeField]
    private ResourceManager _resourceManager;

    [SerializeField]
    private SkillManager _skillManager;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private ResearchManager _researchManager;

    [SerializeField]
    [Tooltip("연구 티어 잠금을 주기와 연동하기 위한 참조. 비워 두면 T1만 열린다.")]
    private WaveCycleProgression _waveCycleProgression;

    [SerializeField]
    private DragonTreeManager _dragonTreeManager;

    [SerializeField]
    private GameSpeedManager _gameSpeedManager;

    [SerializeField]
    [Tooltip("이어하기 지원용. 비워 두면 항상 새 게임으로 시작한다(세이브 없는 테스트 씬).")]
    [WiringOptional]
    private SaveService _saveService;

    [SerializeField] private UnityEvent _victoryOccurred = new();

    public RunData CurrentRun => _currentRun;
    public CycleManager CycleManager => _cycleManager;
    public List<CycleLight> DefaultLights => _defaultLights;
    public ResourceManager ResourceManager => _resourceManager;
    public SkillManager SkillManager => _skillManager;
    public ResearchManager ResearchManager => _researchManager;
    public DragonTreeManager DragonTreeManager => _dragonTreeManager;
    public GameSpeedManager GameSpeedManager => _gameSpeedManager;

    /// <summary>성이 파괴되어 게임오버가 되면 발생. 게임오버 UI 등이 구독한다.</summary>
    // 인라인 초기화가 없으면 씬 YAML에 항목이 없는 씬(테스트 씬 등)에서 null이 되어
    // 구독자의 OnEnable이 NRE로 중단된다. 직렬화된 씬 값이 있으면 그 값이 우선한다.
    public UnityEvent GameOverOccurred = new();
    public UnityEvent VictoryOccurred => _victoryOccurred;
    public GameResult CurrentGameResult {get; private set;} = GameResult.None;
    public bool IsGameEnded => CurrentGameResult != GameResult.None;

    private void OnEnable()
    {
        if (_waveCycleProgression != null)
        {
            _waveCycleProgression.AllCyclesCompleted.AddListener(Victory);
        }
    }

    private void Awake()
    {
        _cycleManager.Construct(this);

        if (_skillManager != null)
        {
            _skillManager.Construct(_cycleManager);
        }

        if (_researchManager != null)
        {
            _researchManager.Construct(
                _cycleManager,
                _resourceManager,
                _gridMap,
                _waveCycleProgression);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.Construct(this, _cycleManager, _resourceManager);
        }
    }

    private void Start()
    {
        // 빈 항목 하나로 Start 전체가 중단되면 아래 StartNewRun까지 건너뛰어 게임이 시작되지
        // 않는다. 해당 조명만 포기하고 나머지 초기화는 계속 진행한다.
        foreach (CycleLight light in _defaultLights)
        {
            if (light == null)
            {
                Debug.LogError(
                    "[GameManager] DefaultLights에 빈 항목이 있습니다 - 해당 조명은 낮/밤 전환을 따르지 않습니다.",
                    this);
                continue;
            }

            light.Construct(_cycleManager);
        }

        // 이어하기 요청이 있으면 초기 자원 지급도 StartDay도 하지 않는다 - 둘 다 복원 흐름이
        // 대신 처리한다(SaveService가 저장된 상태를 적용한 뒤 StartDay를 부른다).
        // _saveService가 없는 씬에서는 기존 동작 그대로다.
        if (_saveService != null && _saveService.HasPendingLoad)
        {
            _saveService.BeginLoadFlow(this);
            return;
        }

        StartNewRun();
    }

    /// <summary>
    /// 새 게임 시작. 이어하기 로드가 실패했을 때 SaveService가 폴백으로 호출하기도 한다 -
    /// 그래서 public이며, 이 경로가 없으면 로드 실패 시 조명·UI·일차가 전부 초기 상태로 멈춘다.
    /// </summary>
    public void StartNewRun()
    {
        // OnEnable 단계에서 이미 구독을 마친 ResourceChanged 리스너(UI 등)가 최초 자원 지급까지
        // 받을 수 있도록, Awake가 아닌 Start에서 Construct한다.
        if (_resourceManager != null)
        {
            _resourceManager.Construct(this);
        }

        // 실행 시 Day 1 시작
        _cycleManager.StartDay();
    }

    private void OnDisable()
    {
        if (_waveCycleProgression != null)
        {
            _waveCycleProgression.AllCyclesCompleted.RemoveListener(Victory);
        }
    }

    // 성이 파괴되면 성이 호출한다(중복 호출 무시). 실제 창 표시는 이벤트 구독자가 담당.
    public void GameOver()
    {
        if (!TrySetGameResult(GameResult.Defeat))
        {
            return;
        }

        GameOverOccurred?.Invoke();
    }

    public void Victory()
    {
        if (!TrySetGameResult(GameResult.Victory))
        {
            return;
        }

        _victoryOccurred?.Invoke();
    }

    private bool TrySetGameResult(GameResult result)
    {
        if (IsGameEnded || result == GameResult.None)
        {
            return false;
        }

        CurrentGameResult = result;
        return true;
    }
}
