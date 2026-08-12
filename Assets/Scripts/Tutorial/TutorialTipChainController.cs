using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 목표를 향한 행동이 실제로 일어났을 때 그 주제의 안내를 열어 준다.
///
/// 1일차 강제 시퀀스와 다른 점은 "언제 여는가"뿐이다. 열고 나면 같은 <see cref="TutorialRunner"/>가
/// 같은 방식으로 단계를 진행한다 - 조건 판정·앵커·동적 대상이 전부 그대로다.
///
/// 대신 <b>관문을 걸지 않는다</b>(각 체인 러너의 _holdsGates를 꺼 둔다). 플레이어가 스스로 시작한
/// 안내이므로 도중에 그만두는 것도 자유여야 한다. 그만두면 러너는 그 단계에 머물고, 다시 그 화면으로
/// 돌아오면 이어서 진행된다.
///
/// 체인은 한 번에 하나만 연다. 농장과 슬라임 농장을 연달아 지으면 뒤엣것은 대기열에 남겨
/// 앞엣것이 끝난 뒤에 연다. 건설처럼 한 번뿐인 신호를 버리면 그 팁은 다시 열 방법이 없기 때문이다.
/// </summary>
public sealed class TutorialTipChainController : MonoBehaviour
{
    private const int FIRST_DAY_NUMBER = 1;

    [Serializable]
    private sealed class Chain
    {
        [Tooltip("이 체인의 러너. 꺼진 채로 자기 차례를 기다리는 별도 오브젝트여야 한다.")]
        public TutorialRunner Runner;

        [Tooltip("무엇이 일어나면 이 체인을 여는가.")]
        public TutorialTriggerSpec Trigger = new();

        [Tooltip("이 일차부터 열린다. 앞당겨 해내면 그때 바로 열어도 되므로 대개 1로 둔다.")]
        [Min(FIRST_DAY_NUMBER)]
        public int RecommendedDay = FIRST_DAY_NUMBER;
    }

    [SerializeField] private List<Chain> _chains = new();

    [SerializeField] private CycleManager _cycleManager;

    [Header("트리거를 듣는 대상")]
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private UIManager _uiManager;

    [Tooltip("이미 서 있는 건물을 클릭하는 것을 트리거로 쓸 때 필요하다(성처럼 지을 일이 없는 건물).")]
    [SerializeField] private BuildingPlacementController _placementController;

    // 지금 돌고 있는 체인. 하나가 끝나야 다음이 열린다.
    private TutorialRunner _running;

    // 다른 체인이 도는 동안 조건을 만족한 체인. 건설 이벤트는 한 번뿐이므로 그 자리에서 버리지 않는다.
    private readonly Queue<Chain> _pendingChains = new();
    private readonly HashSet<TutorialRunner> _pendingRunners = new();

    // 이미 한 번 연 체인. 다시 열지 않는다 - 러너는 진행도를 RunData에 남기므로 재활성하면
    // 마지막 단계 뒤에서 재개해 아무것도 안내하지 않은 채 끝난다.
    private readonly HashSet<TutorialRunner> _opened = new();

    /// <summary>실행 중이거나 대기 중인 모든 팁 체인이 끝나 화면 표시권을 반납했다.</summary>
    public UnityEvent ChainEnded = new();

    public bool IsRunning => _running != null;

    private int CurrentDayNumber =>
        _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.AddListener(HandleExclusiveModeOpened);
        }

        if (_placementController != null)
        {
            // SelectedBuildingChanged는 선택 해제 경로에서 쓰는 상태 전이 신호다. 실제 클릭은
            // BuildingSelected가 매번 발행하므로, 성처럼 이미 서 있는 건물의 팁 시작은 이쪽을 들어야 한다.
            _placementController.BuildingSelected.AddListener(HandleBuildingSelected);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }

        // 체인 러너는 제 차례가 오기 전까지 꺼져 있어야 한다 - 켜져 있으면 시작하자마자 안내가 뜬다.
        foreach (Chain chain in _chains)
        {
            if (chain.Runner != null && chain.Runner.gameObject.activeSelf)
            {
                Debug.LogWarning(
                    $"[TutorialTipChainController] {chain.Runner.name}이 켜진 채로 저장돼 있어 끕니다 - " +
                    "체인 러너는 꺼진 상태로 두어야 합니다.", chain.Runner);
                chain.Runner.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.RemoveListener(HandleExclusiveModeOpened);
        }

        if (_placementController != null)
        {
            _placementController.BuildingSelected.RemoveListener(HandleBuildingSelected);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }

        foreach (Chain chain in _chains)
        {
            if (chain.Runner != null)
            {
                chain.Runner.TutorialEnded.RemoveListener(HandleChainEnded);
            }
        }
    }

    private void HandleBuildingAdded(Building building) =>
        TryOpen(TutorialConditionType.BuildingConstructed, chain => chain.Trigger.MatchesBuilding(building));

    private void HandleExclusiveModeOpened(MonoBehaviour mode) =>
        TryOpen(TutorialConditionType.ExclusiveModeOpened,
            chain => TutorialTargetMatcher.MatchesMode(mode, chain.Trigger.TargetMode));

    // 정산은 OnDayStart에서 이미 끝나 있으므로(CycleManager의 낮 시작 4단계 중 첫 단계),
    // 이 시점에 안내를 열면 플레이어는 방금 바뀐 자원 숫자를 보면서 설명을 읽는다.
    private void HandleDayStart(int _) =>
        TryOpen(TutorialConditionType.DayStartedWithStaffedProduction, _ => HasStaffedProduction());

    /// <summary>
    /// 인구가 들어간 생산시설이 하나라도 있는지. 있으면 오늘 아침에 무언가 정산됐다는 뜻이다.
    /// 자원 총량 증감으로 보지 않는 이유: 유지비가 생산보다 크면 총량이 줄어드는데 그래도 정산은 일어났다.
    /// </summary>
    private bool HasStaffedProduction()
    {
        if (_gridMap == null)
        {
            return false;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (!(building is Factory))
            {
                continue;
            }

            var target = building.GetComponent<IPopulationAllocationTarget>();
            if (target != null && target.IsInitialized && target.AssignedPopulation > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 성처럼 플레이어가 짓지 않는 건물은 "지어졌을 때"로 열 수 없다 - 클릭해서 골랐을 때를 본다.
    // 선택이 풀리면 null이 오므로 그때는 아무 체인도 열지 않는다.
    private void HandleBuildingSelected(Building building)
    {
        if (building == null)
        {
            return;
        }

        TryOpen(TutorialConditionType.BuildingSelectedOnGrid, chain => chain.Trigger.MatchesBuilding(building));
    }

    private void TryOpen(TutorialConditionType condition, Predicate<Chain> matches)
    {
        foreach (Chain chain in _chains)
        {
            if (chain.Runner == null ||
                _opened.Contains(chain.Runner) ||
                _pendingRunners.Contains(chain.Runner))
            {
                continue;
            }

            if (chain.Trigger.Condition != condition || chain.RecommendedDay > CurrentDayNumber)
            {
                continue;
            }

            if (!matches(chain))
            {
                continue;
            }

            if (_running == null)
            {
                Open(chain);
            }
            else
            {
                _pendingChains.Enqueue(chain);
                _pendingRunners.Add(chain.Runner);
            }

            return;
        }
    }

    private void Open(Chain chain)
    {
        _opened.Add(chain.Runner);
        _running = chain.Runner;

        // 구독을 먼저 건다 - 빈 시퀀스는 켜지는 프레임에 끝나버려 종료를 놓친다.
        chain.Runner.TutorialEnded.AddListener(HandleChainEnded);
        chain.Runner.gameObject.SetActive(true);

        Debug.Log($"[TutorialTipChainController] 팁 체인 시작: {chain.Runner.name}", chain.Runner);
    }

    private void HandleChainEnded()
    {
        if (_running == null)
        {
            return;
        }

        _running.TutorialEnded.RemoveListener(HandleChainEnded);
        _running.gameObject.SetActive(false);
        Debug.Log($"[TutorialTipChainController] 팁 체인 종료: {_running.name}", _running);
        _running = null;

        while (_pendingChains.Count > 0)
        {
            Chain next = _pendingChains.Dequeue();
            TutorialRunner nextRunner = next.Runner;
            _pendingRunners.Remove(nextRunner);

            if (nextRunner == null || _opened.Contains(nextRunner))
            {
                continue;
            }

            if (!IsTriggerContextValid(next))
            {
                Debug.Log(
                    $"[TutorialTipChainController] {nextRunner.name}은 기다리는 동안 트리거 상황이 끝나 열지 않습니다 - " +
                    "다음에 같은 조건이 다시 성립하면 그때 시작합니다.", nextRunner);
                continue;
            }

            Open(next);
            return;
        }

        ChainEnded.Invoke();
    }

    /// <summary>
    /// 대기하던 체인을 지금 열어도 되는지. 기다리는 동안 상황이 끝났으면 안내가 엉뚱한 화면에서 뜬다 -
    /// 창 안을 설명하는 안내가 창 밖에서 뜨거나, 이미 선택이 풀린 건물을 가리킨다.
    /// 특히 뒤엣것은 행동형 단계라 Render가 입력 차단을 자동으로 켜므로, 플레이어가 그 사이 열어 둔
    /// 다른 창이 딤에 덮여 닫지도 못하는 상태가 된다(메인성 팁이 실제로 그렇게 갇혔다).
    ///
    /// <b>다시 만들 수 있는 상황만 검사한다.</b> 창 열기·건물 선택은 플레이어가 한 번 더 하면 되지만,
    /// 건설은 그렇지 않다 - 농장을 이미 지은 사람에게 "또 지어야 팁을 준다"고 할 수는 없으므로
    /// BuildingConstructed는 상황이 지나갔어도 그대로 연다.
    ///
    /// 열지 않고 흘려보내되 _opened에는 넣지 않으므로, 다음에 같은 조건이 성립하면 그때 제대로 시작된다.
    /// 안내가 사라지는 것이 아니라 맞는 타이밍으로 미뤄지는 것이다.
    /// </summary>
    private bool IsTriggerContextValid(Chain chain)
    {
        switch (chain.Trigger.Condition)
        {
            case TutorialConditionType.ExclusiveModeOpened:
                return _uiManager == null ||
                       TutorialTargetMatcher.MatchesMode(_uiManager.CurrentOpenExclusiveMode, chain.Trigger.TargetMode);

            case TutorialConditionType.BuildingSelectedOnGrid:
                return _placementController == null ||
                       chain.Trigger.MatchesBuilding(_placementController.SelectedBuilding);

            default:
                return true;
        }
    }
}
