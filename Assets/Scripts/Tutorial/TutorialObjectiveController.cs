using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 2일차 이후의 자유 목표를 추적한다. TutorialRunner와 달리 순서를 강제하지 않고, 밤 진입도 막지 않는다 -
/// 목표는 통과 관문이 아니라 권장 학습 항목이다.
///
/// **항상 켜 두어야 한다.** 강제 시퀀스가 도는 1일차부터 게임 이벤트를 듣고 있어야, 안내보다 먼저
/// 해 버린 행동도 완료로 잡힌다. 표시(UI_TutorialObjectiveWindow)만 나중에 켜면 된다 -
/// 그래서 이 컴포넌트는 표시를 직접 들고 있지 않고 ObjectivesChanged만 알린다.
///
/// 완료 판정은 <b>이벤트로만</b> 한다. 건물 계열은 시작 시점 스냅샷을 뜨지 않는데, 성이 게임 시작 시
/// GridMap에 등록되며 같은 이벤트 경로를 지나므로 스냅샷을 넣으면 목표가 시작 즉시 완료돼 버린다.
/// 대신 이 컴포넌트가 1일차부터 살아 있는 것으로 누락을 막는다.
/// </summary>
public sealed class TutorialObjectiveController : MonoBehaviour, IDayEndBlockQuery
{
    // 완료 알림은 쓰지 않는다 - 목록의 체크 표시가 같은 말을 이미 하고 있다.
    // 남은 것은 밤으로 넘어가기 직전의 상기뿐이다(목록을 늘 띄워 두어도, 결정 직전에는 한 번 짚어 준다).
    // 밤 진입이 막혔을 때 띄운다. 막는 주체는 안내 러너나 새끼용 가이드지만 문구는 여기서 낸다 -
    // 플레이어가 알아야 하는 것은 "누가 막았는가"가 아니라 "오늘 할 일이 남았다"이고, 그 목록이 여기에 있다.
    private const string NIGHT_BLOCKED_LOC_KEY = "tutorial_night_blocked";
    private const string WORKER_MODE_NUDGE_LOC_KEY = "tutorial_worker_mode_nudge";

    // "아직 해보지 않은 것이 {0}가지 남았습니다." - 밤으로 넘어가기 직전 1회 상기.
    private const string OBJECTIVE_REMAINING_LOC_KEY = "tutorial_objective_remaining";

    private const int FIRST_DAY_NUMBER = 1;
    private const int WORKER_MODE_NUDGE_DAY = 2;
    private const float DEFAULT_WORKER_MODE_NUDGE_DELAY_SECONDS = 1f;

    [Tooltip("추적할 목표들. 순서는 목록에 보이는 순서다.")]
    [SerializeField] private List<TutorialObjectiveSO> _objectives = new();

    [Tooltip("완료 기록을 남길 런. 없으면 완료가 기록되지 않는다.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("권장 일차를 판정하고 밤 진입 상기를 띄우는 데 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("완료·상기 알림을 띄운다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("마지막 건물 팁이 화면 표시권을 반납한 뒤 워커모드 토스트를 띄우는 데 쓴다.")]
    [SerializeField] private TutorialTipChainController _tipChainController;

    [Tooltip("건물 팁과 열려 있던 창이 모두 끝난 뒤 워커모드 토스트를 띄우기까지 기다릴 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _workerModeNudgeDelaySeconds = DEFAULT_WORKER_MODE_NUDGE_DELAY_SECONDS;

    [Header("완료 조건을 듣는 대상")]
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private UI_DragonWindow _dragonWindow;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("어미용 속성 변경을 완료 조건으로 쓰는 목표에 필요하다.")]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    /// <summary>
    /// 목록에 보일 것이 바뀌었다(목표가 열렸거나 완료됐거나 날짜가 바뀌었다).
    /// 표시하는 쪽은 이것만 구독하고 어떤 목표가 왜 바뀌었는지는 다시 물어본다 -
    /// 인자로 넘기면 표시가 늘 때마다 시그니처를 고쳐야 한다.
    /// </summary>
    public UnityEvent ObjectivesChanged = new();

    // 매번 새로 만들면 구독자가 프레임마다 리스트를 할당하게 된다.
    private readonly List<TutorialObjectiveSO> _visibleObjectives = new();
    private bool _workerModeNudgeShown;
    private bool _workerModeNudgeCheckScheduled;

    private RunData CurrentRun => _gameManager == null ? null : _gameManager.CurrentRun;

    private int CurrentDayNumber => _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

    /// <summary>지금 목록에 보여야 할 목표들. 완료한 것도 남는다 - 지운 자리는 성취로 보이지 않는다.</summary>
    public IReadOnlyList<TutorialObjectiveSO> VisibleObjectives => _visibleObjectives;

    public bool IsCompleted(TutorialObjectiveSO objective)
    {
        RunData run = CurrentRun;
        return objective != null && run != null && run.HasCompletedObjective(objective.ObjectiveId);
    }

    /// <summary>
    /// <b>목표는 밤 진입을 막지 않는다.</b> 항상 true를 돌려준다.
    ///
    /// 예전에는 오늘 목표를 다 채워야 밤으로 넘어갈 수 있었는데, 그 구조에는 풀 수 없는 잠금이 있었다 -
    /// 3일차 보스 밤이 "어미용 속성 변경"(하루 1회 제한)과 "연구 완료"(지급 타이밍 의존) 뒤에 잠기고,
    /// 2일차는 건설 목표 셋이 동시에 관문이 되어 자원이 모자라면 회복할 방법이 사라진다.
    /// 목표는 통과 관문이 아니라 권장 학습 항목이므로, 미완료는 다음 날로 이월시키고 밤은 그대로 보낸다.
    ///
    /// 인터페이스 구현 자체는 남겨 둔다 - 등록/해제 배선과 DayEndBlocked 상기 문구가 이 자리에 묶여 있고,
    /// 나중에 "이 목표만은 막는다"가 필요해지면 여기 한 곳만 고치면 된다.
    /// </summary>
    bool IDayEndBlockQuery.CanEndDay() => true;

    /// <summary>아직 끝내지 않은 오늘의 목표 수. 밤 버튼을 누를 때 한 번 상기하는 데 쓴다.</summary>
    private int RemainingObjectiveCount
    {
        get
        {
            int remaining = 0;
            foreach (TutorialObjectiveSO objective in _visibleObjectives)
            {
                // 밤을 넘겨야 완료되는 목표는 세지 않는다 - 지금 누르는 이 버튼이 그 목표의 완료 방법이라,
                // 세어 버리면 절대 지울 수 없는 잔소리가 된다.
                if (objective.CompletionTrigger.Condition == TutorialConditionType.NightSurvived)
                {
                    continue;
                }

                if (!IsCompleted(objective))
                {
                    remaining++;
                }
            }

            return remaining;
        }
    }

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙) - Start끼리는 순서가 보장되지 않아
    // "구독 Start vs 발화 Start"가 뒤집힐 수 있다.
    private void Awake()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleResearchNodeCompleted);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionSent.AddListener(HandleExpeditionSent);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.AddListener(HandleExclusiveModeOpened);
            _uiManager.ExclusiveModeClosed.AddListener(HandleExclusiveModeClosed);
        }

        if (_dragonWindow != null)
        {
            _dragonWindow.OnTabDisplayed.AddListener(HandleDragonTabDisplayed);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleAttributeChanged);
        }

        if (_tipChainController != null)
        {
            _tipChainController.ChainEnded.AddListener(HandleTipChainEnded);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
            _cycleManager.OnDayEnd.AddListener(HandleDayEnd);
            _cycleManager.OnNightEnd.AddListener(HandleNightEnd);
            _cycleManager.DayEndBlocked.AddListener(HandleDayEndBlocked);
            _cycleManager.AddDayEndBlocker(this);
        }
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchNodeCompleted);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.RemoveListener(HandleExclusiveModeOpened);
            _uiManager.ExclusiveModeClosed.RemoveListener(HandleExclusiveModeClosed);
        }

        if (_dragonWindow != null)
        {
            _dragonWindow.OnTabDisplayed.RemoveListener(HandleDragonTabDisplayed);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleAttributeChanged);
        }

        if (_tipChainController != null)
        {
            _tipChainController.ChainEnded.RemoveListener(HandleTipChainEnded);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
            _cycleManager.OnDayEnd.RemoveListener(HandleDayEnd);
            _cycleManager.OnNightEnd.RemoveListener(HandleNightEnd);
            _cycleManager.DayEndBlocked.RemoveListener(HandleDayEndBlocked);
            _cycleManager.RemoveDayEndBlocker(this);
        }
    }

    // 막힌 이유를 말해 주지 않으면 버튼이 고장 난 것으로 보인다.
    // 이제 목표는 밤을 막지 않으므로, 이 신호는 1일차 안내 러너처럼 다른 관문이 걸었을 때만 온다.
    private void HandleDayEndBlocked()
    {
        if (_toast != null)
        {
            _toast.Show(NIGHT_BLOCKED_LOC_KEY);
        }
    }

    /// <summary>
    /// 밤으로 넘어가는 순간 남은 목표 수를 한 번 짚어 준다. 막지는 않는다 -
    /// 목록은 늘 화면에 있지만, 되돌릴 수 없는 결정 직전에는 한 번 더 말해 주는 편이 낫다.
    /// 남은 것이 없으면 아무 말도 하지 않는다.
    /// </summary>
    private void HandleDayEnd(int _)
    {
        int remaining = RemainingObjectiveCount;
        if (remaining > 0 && _toast != null)
        {
            _toast.Show(OBJECTIVE_REMAINING_LOC_KEY, remaining);
        }
    }

    private void Start()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[TutorialObjectiveController] GameManager 참조가 없어 목표 완료가 기록되지 않습니다.", this);
        }

        RebuildVisibleObjectives();
        ScheduleWorkerModeNudgeCheck();
    }

    private void ScheduleWorkerModeNudgeCheck()
    {
        if (_workerModeNudgeShown || _workerModeNudgeCheckScheduled)
        {
            return;
        }

        _workerModeNudgeCheckScheduled = true;
        CheckWorkerModeNudgeAfterTipStartAsync().Forget();
    }

    private async UniTaskVoid CheckWorkerModeNudgeAfterTipStartAsync()
    {
        // 건설 목표와 건물 팁이 같은 OnBuildingAdded에서 결정된다. 이벤트 구독 순서와 무관하게
        // 팁이 시작된 뒤 검사하도록 한 프레임을 양보한다.
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.Yield(token);

        if (_workerModeNudgeDelaySeconds > 0f)
        {
            await UniTask.WaitForSeconds(
                _workerModeNudgeDelaySeconds,
                ignoreTimeScale: true,
                cancellationToken: token);
        }

        _workerModeNudgeCheckScheduled = false;
        TryShowWorkerModeNudge();
    }

    private void HandleTipChainEnded() => ScheduleWorkerModeNudgeCheck();

    /// <summary>
    /// 2일차 건설 목표를 모두 마친 시점에 워커모드의 존재만 알린다.
    /// 학습 체인은 플레이어가 워커모드를 직접 열었을 때 별도로 시작한다.
    /// </summary>
    private void TryShowWorkerModeNudge()
    {
        if (_workerModeNudgeShown || _toast == null || CurrentDayNumber != WORKER_MODE_NUDGE_DAY ||
            (_tipChainController != null && _tipChainController.IsRunning) ||
            (_uiManager != null && _uiManager.CurrentOpenExclusiveMode != null))
        {
            return;
        }

        bool hasBuildingObjective = false;
        foreach (TutorialObjectiveSO objective in _objectives)
        {
            if (objective == null || objective.RecommendedDay != WORKER_MODE_NUDGE_DAY ||
                objective.CompletionTrigger.Condition != TutorialConditionType.BuildingConstructed)
            {
                continue;
            }

            hasBuildingObjective = true;
            if (!IsCompleted(objective))
            {
                return;
            }
        }

        if (hasBuildingObjective)
        {
            _workerModeNudgeShown = true;
            _toast.Show(WORKER_MODE_NUDGE_LOC_KEY);
        }
    }

    private void HandleDayStart(int _)
    {
        RebuildVisibleObjectives();
        ScheduleWorkerModeNudgeCheck();
    }

    private void HandleBuildingAdded(Building building)
    {
        TryCompleteMatching(TutorialConditionType.BuildingConstructed,
            objective => objective.CompletionTrigger.MatchesBuilding(building));
    }

    // 어느 노드인지 가리지 않는다 - 목표는 "연구 하나를 끝낸다"이지 특정 연구가 아니다.
    private void HandleResearchNodeCompleted(ResearchNodeData _) =>
        TryCompleteMatching(TutorialConditionType.ResearchNodeCompleted, null);

    private void HandleExpeditionSent(Vector2Int _, ResourceCost __) =>
        TryCompleteMatching(TutorialConditionType.ExpeditionSent, null);

    // 총량으로 본다 - 선형 단계처럼 "이 단계 동안 늘어난 만큼"으로 재면 시작 시점이 없는 목표에서는
    // 기준이 서지 않는다. 한 명이라도 어딘가에 들어가 있으면 배운 것으로 친다.
    private void HandlePopulationChanged(PopulationState _)
    {
        if (_populationManager != null && _populationManager.AssignedPopulation > 0)
        {
            TryCompleteMatching(TutorialConditionType.AnyPopulationAssigned, null);
        }

        TryCompleteMatching(TutorialConditionType.PopulationAssignedToBuilding,
            objective => HasStaffedBuilding(objective.CompletionTrigger));
    }

    /// <summary>
    /// 그 종류의 건물 중 인구가 한 명이라도 들어간 것이 있는지. 합계가 아니라 건물을 직접 봐야 한다 -
    /// PopulationChanged는 전체 배치 인구만 넘기므로, 그것만 보면 농장에 넣어도 "타워에 배치"가 통과한다.
    /// </summary>
    private bool HasStaffedBuilding(TutorialTriggerSpec trigger)
    {
        if (_gridMap == null)
        {
            return false;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building == null || !trigger.MatchesBuilding(building))
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

    private void HandleNightEnd(int _) =>
        TryCompleteMatching(TutorialConditionType.NightSurvived, null);

    private void HandleEggGranted(DragonType _) =>
        TryCompleteMatching(TutorialConditionType.DragonEggGranted, null);

    // 어느 속성으로 바꿨는지는 묻지 않는다 - 목표는 "바꿔본다"이지 특정 속성이 아니다.
    private void HandleAttributeChanged(DragonType _)
    {
        // DragonTreeManager의 같은 신호는 새날 HUD 갱신과 저장 복원에도 쓰인다. 실제 변경에 성공한 경우에는
        // Dragon.TryChangeType이 그날의 변경권을 먼저 소비하므로, 그 상태일 때만 목표 행동으로 인정한다.
        Dragon dragon = CurrentRun?.CurrentDragon;
        if (dragon == null || !dragon.IsChangedThisDay)
        {
            return;
        }

        TryCompleteMatching(TutorialConditionType.MotherDragonAttributeChanged, null);
    }

    private void HandleExclusiveModeOpened(MonoBehaviour mode)
    {
        if (mode is WorkerModeController)
        {
            // 이미 스스로 일꾼 모드를 찾은 플레이어에게 나중에 존재를 다시 알리지 않는다.
            _workerModeNudgeShown = true;
        }

        TryCompleteMatching(TutorialConditionType.ExclusiveModeOpened,
            objective => TutorialTargetMatcher.MatchesMode(mode, objective.CompletionTrigger.TargetMode));
    }

    // 닫힘은 연 적이 있어야만 발행되므로(UIManager가 IsOpen 전이를 본다), "열어서 확인하고 닫았다"가 된다.
    private void HandleExclusiveModeClosed(MonoBehaviour mode)
    {
        TryCompleteMatching(TutorialConditionType.ExclusiveModeClosed,
            objective => TutorialTargetMatcher.MatchesMode(mode, objective.CompletionTrigger.TargetMode));

        // 슬라임 농장 팁은 용 창을 연 채 끝날 수 있다. 그 창을 닫은 뒤 읽을 틈을 두고 토스트를 다시 검사한다.
        ScheduleWorkerModeNudgeCheck();
    }

    // 용 창은 새끼용·어미용이 한 창의 두 탭이라 창이 열렸는지로는 구분되지 않는다 - 탭까지 봐야 한다.
    //
    // 이 이벤트는 창이 닫힌 채로 목록을 다시 그릴 때도 발화한다. 어미용이 기본 탭이라 게임 시작 시점의
    // 초기화 한 번으로 isBabyTab=false가 흘러나오고, 그것만 보면 플레이어가 창을 열기도 전에
    // 목표가 완료돼 버린다(실제로 그랬다). 창이 정말 열려 그 탭이 보이는 중인지까지 확인한다.
    private void HandleDragonTabDisplayed(bool isBabyTab)
    {
        if (isBabyTab || _dragonWindow == null || !_dragonWindow.IsMotherTabShown)
        {
            return;
        }

        TryCompleteMatching(TutorialConditionType.DragonWindowMotherTabSelected, null);
    }

    /// <summary>
    /// 해당 조건을 가진 미완료 목표 중 추가 판정까지 통과한 것을 완료 처리한다.
    /// </summary>
    /// <param name="extraFilter">대상 종류까지 봐야 하는 조건에서만 넘긴다. null이면 조건 일치만으로 완료다.</param>
    private void TryCompleteMatching(
        TutorialConditionType condition, System.Predicate<TutorialObjectiveSO> extraFilter)
    {
        foreach (TutorialObjectiveSO objective in _objectives)
        {
            if (objective == null || objective.CompletionTrigger.Condition != condition)
            {
                continue;
            }

            if (extraFilter != null && !extraFilter(objective))
            {
                continue;
            }

            CompleteObjective(objective);
        }
    }

    private void CompleteObjective(TutorialObjectiveSO objective)
    {
        RunData run = CurrentRun;

        // TryCompleteObjective가 중복을 걸러주므로 완료 알림은 처음 한 번만 뜬다.
        if (run == null || !run.TryCompleteObjective(objective.ObjectiveId))
        {
            return;
        }

        // 아직 목록에 뜨지 않은 날의 목표를 먼저 해냈을 수도 있다 - 그때는 목록에 나타나며 체크된 채로 보인다.
        //
        // 완료 알림은 따로 띄우지 않는다. 목록이 늘 화면에 있고 그 줄에 체크가 들어가므로
        // 토스트까지 내면 같은 사실을 두 번 말하면서 안내 말풍선과 자리를 다툰다.
        RebuildVisibleObjectives();
        ScheduleWorkerModeNudgeCheck();
    }

    // 권장 일차가 된 목표를 목록에 넣는다. 지난 날의 목표는 완료 여부와 관계없이 계속 남는다 -
    // 미완료는 이월되어야 하고, 완료된 것은 체크 표시로 남아야 한다.
    private void RebuildVisibleObjectives()
    {
        int today = CurrentDayNumber;

        _visibleObjectives.Clear();
        foreach (TutorialObjectiveSO objective in _objectives)
        {
            if (objective != null && objective.RecommendedDay <= today)
            {
                _visibleObjectives.Add(objective);
            }
        }

        ObjectivesChanged.Invoke();
    }
}
