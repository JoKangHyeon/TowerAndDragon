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
    [Tooltip("연구 티어 잠금을 주기와 연동하기 위한 참조. 비워 두면 T1만 열린다.")]
    private WaveCycleProgression _waveCycleProgression;

    [SerializeField]
    private DragonTreeManager _dragonTreeManager;

    [SerializeField]
    private GameSpeedManager _gameSpeedManager;

    [SerializeField]
    [Tooltip("이어하기 지원용. 비워 두면 항상 새 게임으로 시작한다(세이브 없는 테스트 씬).")]
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
    public UnityEvent GameOverOccurred;
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

        // Dragon.Construct가 어디서도 호출되지 않아 OnDayStart→ResetChangedThisDay 구독이 성립하지 않았다.
        // 그 결과 속성 변경 제한이 "하루 1회"가 아니라 "런 1회"로 동작했다.
        _currentRun?.CurrentDragon?.Construct(_cycleManager);

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
        foreach (CycleLight light in _defaultLights)
        {
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
