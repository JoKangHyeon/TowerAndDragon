using System.Collections.Generic;
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
public sealed class TutorialObjectiveController : MonoBehaviour
{
    private const string OBJECTIVE_COMPLETED_LOC_KEY = "tutorial_objective_completed";
    private const string OBJECTIVE_REMAINING_LOC_KEY = "tutorial_objective_remaining";
    private const int FIRST_DAY_NUMBER = 1;

    [Tooltip("추적할 목표들. 순서는 목록에 보이는 순서다.")]
    [SerializeField] private List<TutorialObjectiveSO> _objectives = new();

    [Tooltip("완료 기록을 남길 런. 없으면 완료가 기록되지 않는다.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("권장 일차를 판정하고 밤 진입 상기를 띄우는 데 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("완료·상기 알림을 띄운다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Header("완료 조건을 듣는 대상")]
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private UI_DragonWindow _dragonWindow;

    /// <summary>
    /// 목록에 보일 것이 바뀌었다(목표가 열렸거나 완료됐거나 날짜가 바뀌었다).
    /// 표시하는 쪽은 이것만 구독하고 어떤 목표가 왜 바뀌었는지는 다시 물어본다 -
    /// 인자로 넘기면 표시가 늘 때마다 시그니처를 고쳐야 한다.
    /// </summary>
    public UnityEvent ObjectivesChanged = new();

    // 매번 새로 만들면 구독자가 프레임마다 리스트를 할당하게 된다.
    private readonly List<TutorialObjectiveSO> _visibleObjectives = new();

    private RunData CurrentRun => _gameManager == null ? null : _gameManager.CurrentRun;

    private int CurrentDayNumber => _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

    /// <summary>지금 목록에 보여야 할 목표들. 완료한 것도 남는다 - 지운 자리는 성취로 보이지 않는다.</summary>
    public IReadOnlyList<TutorialObjectiveSO> VisibleObjectives => _visibleObjectives;

    public bool IsCompleted(TutorialObjectiveSO objective)
    {
        RunData run = CurrentRun;
        return objective != null && run != null && run.HasCompletedObjective(objective.ObjectiveId);
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
        }

        if (_dragonWindow != null)
        {
            _dragonWindow.OnTabDisplayed.AddListener(HandleDragonTabDisplayed);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
            _cycleManager.OnDayEnd.AddListener(HandleDayEnd);
            _cycleManager.OnNightEnd.AddListener(HandleNightEnd);
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
        }

        if (_dragonWindow != null)
        {
            _dragonWindow.OnTabDisplayed.RemoveListener(HandleDragonTabDisplayed);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
            _cycleManager.OnDayEnd.RemoveListener(HandleDayEnd);
            _cycleManager.OnNightEnd.RemoveListener(HandleNightEnd);
        }
    }

    private void Start()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[TutorialObjectiveController] GameManager 참조가 없어 목표 완료가 기록되지 않습니다.", this);
        }

        RebuildVisibleObjectives();
    }

    private void HandleDayStart(int _) => RebuildVisibleObjectives();

    // 밤이 시작되기 전 마지막 알림. 막지는 않고 남은 것만 알린다.
    private void HandleDayEnd(int _)
    {
        if (_toast == null)
        {
            return;
        }

        int remaining = 0;
        foreach (TutorialObjectiveSO objective in _visibleObjectives)
        {
            if (!IsCompleted(objective))
            {
                remaining++;
            }
        }

        if (remaining > 0)
        {
            _toast.Show(OBJECTIVE_REMAINING_LOC_KEY, remaining);
        }
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
    }

    private void HandleNightEnd(int _) =>
        TryCompleteMatching(TutorialConditionType.NightSurvived, null);

    private void HandleExclusiveModeOpened(MonoBehaviour mode)
    {
        TryCompleteMatching(TutorialConditionType.ExclusiveModeOpened,
            objective => TutorialTargetMatcher.MatchesMode(mode, objective.CompletionTrigger.TargetMode));
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
        RebuildVisibleObjectives();

        _toast?.Show(OBJECTIVE_COMPLETED_LOC_KEY, StringTable.GetString(objective.TitleLocKey));
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
