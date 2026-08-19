using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 2일차 이후의 목표를 추적한다. TutorialRunner와 달리 순서를 강제하지는 않지만, 오늘 목록에 남은 것을
/// 다 끝내기 전에는 밤으로 넘어갈 수 없다 - 목표는 그날 배워야 할 것의 통과 관문이다.
/// (밤을 넘겨야 완료되는 목표만 예외다. 그것을 세면 밤 버튼이 자기 자신을 막는다.)
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
    // 밤 진입이 막혔을 때만 말을 건다. 막는 주체는 여럿이지만 문구는 여기서 낸다 -
    // 플레이어가 알아야 하는 것은 "누가 막았는가"가 아니라 무엇이 남았는가이고, 그 목록이 여기에 있다.

    // "안내가 끝나면 밤으로 넘어갈 수 있습니다." - 목표는 다 찼는데 안내가 도는 중일 때.
    private const string NIGHT_BLOCKED_LOC_KEY = "tutorial_night_blocked";
    // "아직 해보지 않은 것이 {0}가지 남았습니다." - 목표가 막았을 때.
    private const string OBJECTIVE_REMAINING_LOC_KEY = "tutorial_objective_remaining";
    // "이제 타워를 더 설치해서 보스를 막아보세요..." - 마지막 날 관문이 열린 순간 1회.
    private const string BOSS_PREPARATION_LOC_KEY = "tutorial_day3_ready_for_boss";

    private const int FIRST_DAY_NUMBER = 1;
    private const int DEFAULT_BOSS_PREPARATION_DAY_NUMBER = 3;
    // 기본 유지 시간으로는 두 문장을 읽기 전에 사라진다.
    private const float DEFAULT_BOSS_PREPARATION_SHOW_SECONDS = 6f;

    [Tooltip("추적할 목표들. 순서는 목록에 보이는 순서다.")]
    [SerializeField] private List<TutorialObjectiveSO> _objectives = new();

    [Tooltip("완료 기록을 남길 런. 없으면 완료가 기록되지 않는다.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("권장 일차를 판정하고 밤 진입 관문을 거는 데 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("밤 진입이 막힌 이유를 띄운다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Header("완료 조건을 듣는 대상")]
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private UI_DragonWindow _dragonWindow;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("알 확인 안내가 끝난 시점을 완료 조건으로 쓰는 목표에 필요하다. " +
             "이 컨트롤러와 다른 프리팹에 있으므로 씬에서 연결해야 한다.")]
    [SerializeField] private BabyDragonGuideController _babyDragonGuideController;

    [Tooltip("어미용 속성 변경을 완료 조건으로 쓰는 목표에 필요하다.")]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    [Header("마지막 날 관문이 열렸을 때")]
    [Tooltip("이 일차에 목표를 다 채우면 보스 준비 안내를 한 번 띄운다.")]
    [Min(FIRST_DAY_NUMBER)]
    [SerializeField] private int _bossPreparationDayNumber = DEFAULT_BOSS_PREPARATION_DAY_NUMBER;

    [Tooltip("그 안내를 띄워 둘 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _bossPreparationShowSeconds = DEFAULT_BOSS_PREPARATION_SHOW_SECONDS;

    /// <summary>
    /// 목록에 보일 것이 바뀌었다(목표가 열렸거나 완료됐거나 날짜가 바뀌었다).
    /// 표시하는 쪽은 이것만 구독하고 어떤 목표가 왜 바뀌었는지는 다시 물어본다 -
    /// 인자로 넘기면 표시가 늘 때마다 시그니처를 고쳐야 한다.
    /// </summary>
    public UnityEvent ObjectivesChanged = new();

    // 매번 새로 만들면 구독자가 프레임마다 리스트를 할당하게 된다.
    private readonly List<TutorialObjectiveSO> _visibleObjectives = new();
    // 속성 변경 알림이 실제 변경인지 새날·복원 갱신인지 가르는 기준값.
    private DragonType? _lastSeenAttribute;
    // 보스 준비 안내는 한 번만 낸다 - 목록이 다시 그려질 때마다 같은 말을 반복하게 된다.
    private bool _hasAnnouncedBossPreparation;

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
    /// 목록에 남은 목표를 다 끝내야 밤으로 넘어간다(밤을 넘겨야 완료되는 목표는 제외).
    ///
    /// <b>여기 걸리는 목표는 전부 "그날 낮 안에" 달성 가능해야 한다.</b> 한때 이 관문을 열어 둔 적이
    /// 있는데, 그때의 우려는 3일차가 어미용 속성 변경과 연구 완료 뒤에 잠긴다는 것이었다. 확인해 보니
    /// 둘 다 잠그지 않는다 - Dragon.TryChangeType에는 횟수 제한이 없고 3일차 강제 챕터가 속성 변경을
    /// 직접 시키며(TS_311), 연구는 ResearchManager.TryResearch가 구매 즉시 NodeCompleted를 발화하고
    /// 포인트도 2일차 아침에 채워진다. 목표를 새로 걸 때 이 조건이 깨지면 그날 밤이 영영 오지 않는다.
    ///
    /// 런이 없으면 막지 않는다 - IsCompleted가 전부 false를 돌려주므로, 배선이 빠진 씬에서
    /// 경고 한 줄로 끝나던 것이 눌리지 않는 밤 버튼이 된다.
    /// </summary>
    bool IDayEndBlockQuery.CanEndDay() => CurrentRun == null || RemainingObjectiveCount == 0;

    /// <summary>아직 끝내지 않은 오늘의 목표 수. 밤을 막을지와 막힌 이유를 정하는 데 쓴다.</summary>
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

        if (_babyDragonGuideController != null)
        {
            _babyDragonGuideController.EggCheckGuideFinished.AddListener(HandleEggCheckGuideFinished);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleAttributeChanged);
            _dragonTreeManager.NodeUnlocked.AddListener(HandleDragonNodeUnlocked);

            // 구독 직후 현재 값을 한 번 반영해 둔다 - 이 값이 없으면 구독 후 첫 발화가
            // 새날 갱신인지 실제 변경인지 가릴 수 없다.
            _lastSeenAttribute = _dragonTreeManager.ActiveAttribute;
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
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

        if (_babyDragonGuideController != null)
        {
            _babyDragonGuideController.EggCheckGuideFinished.RemoveListener(HandleEggCheckGuideFinished);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleAttributeChanged);
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleDragonNodeUnlocked);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
            _cycleManager.OnNightEnd.RemoveListener(HandleNightEnd);
            _cycleManager.DayEndBlocked.RemoveListener(HandleDayEndBlocked);
            _cycleManager.RemoveDayEndBlocker(this);
        }
    }

    // 막힌 이유를 말해 주지 않으면 버튼이 고장 난 것으로 보인다.
    //
    // 막는 주체가 목표만이 아니라서(1일차 안내 러너·새끼용 가이드도 같은 신호를 낸다) 어느 쪽이
    // 걸었는지를 남은 목표 수로 가른다. 남은 것이 있으면 그것이 이유고, 없으면 안내가 도는 중이다 -
    // 반대로 말하면 두 문구가 서로의 상황에서 거짓말이 되므로 하나로 합칠 수 없다.
    private void HandleDayEndBlocked()
    {
        if (_toast == null)
        {
            return;
        }

        int remaining = RemainingObjectiveCount;
        if (remaining > 0)
        {
            _toast.Show(OBJECTIVE_REMAINING_LOC_KEY, remaining);
            return;
        }

        _toast.Show(NIGHT_BLOCKED_LOC_KEY);
    }

    private void Start()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[TutorialObjectiveController] GameManager 참조가 없어 목표 완료가 기록되지 않습니다.", this);
        }

        WarnIfEggCheckObjectiveUnwired();
        RebuildVisibleObjectives();
    }

    // 배선을 빼먹으면 그 목표만 영영 체크되지 않고 아무 에러도 나지 않는다 - 씬 참조라 실제로 자주 빠진다.
    private void WarnIfEggCheckObjectiveUnwired()
    {
        if (_babyDragonGuideController != null)
        {
            return;
        }

        foreach (TutorialObjectiveSO objective in _objectives)
        {
            if (objective != null &&
                objective.CompletionTrigger.Condition == TutorialConditionType.BabyDragonEggChecked)
            {
                Debug.LogWarning(
                    "[TutorialObjectiveController] BabyDragonGuideController 참조가 없어 " +
                    $"'{objective.ObjectiveId}' 목표가 완료되지 않습니다.", this);
                return;
            }
        }
    }

    private void HandleDayStart(int _)
    {
        RebuildVisibleObjectives();
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

    private void HandleEggCheckGuideFinished() =>
        TryCompleteMatching(TutorialConditionType.BabyDragonEggChecked, null);

    // 어느 노드인지 가리지 않는다 - 목표는 "스킬 하나를 연다"이지 특정 노드가 아니다(연구와 같은 기준).
    //
    // 세이브 복원(ProgressionManagerBase.RestoreUnlockedNodes)도 이 신호를 재발화하므로, 이어하기로
    // 들어오면 이미 연 노드가 그 자리에서 목표를 채운다. 그것이 맞다 - 실제로 연 적이 있기 때문이다.
    private void HandleDragonNodeUnlocked(ProgressionNodeData _) =>
        TryCompleteMatching(TutorialConditionType.DragonSkillNodeUnlocked, null);

    // 어느 속성으로 바꿨는지는 묻지 않는다 - 목표는 "바꿔본다"이지 특정 속성이 아니다.
    private void HandleAttributeChanged(DragonType attribute)
    {
        // DragonTreeManager의 같은 신호는 새날 HUD 갱신과 저장 복원에도 쓰인다. 그쪽은 현재 속성을
        // 그대로 다시 실어 보내므로, 직전에 본 속성과 달라졌을 때만 실제 변경으로 인정한다.
        // (호출부는 Dragon.TryChangeType이 true를 돌려준 경우에만 변경 알림을 발화한다.)
        bool isActualChange = _lastSeenAttribute.HasValue && _lastSeenAttribute.Value != attribute;
        _lastSeenAttribute = attribute;

        if (!isActualChange)
        {
            return;
        }

        TryCompleteMatching(TutorialConditionType.MotherDragonAttributeChanged, null);
    }

    private void HandleExclusiveModeOpened(MonoBehaviour mode)
    {
        TryCompleteMatching(TutorialConditionType.ExclusiveModeOpened,
            objective => TutorialTargetMatcher.MatchesMode(mode, objective.CompletionTrigger.TargetMode));
    }

    // 닫힘은 연 적이 있어야만 발행되므로(UIManager가 IsOpen 전이를 본다), "열어서 확인하고 닫았다"가 된다.
    private void HandleExclusiveModeClosed(MonoBehaviour mode)
    {
        TryCompleteMatching(TutorialConditionType.ExclusiveModeClosed,
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
        //
        // 완료 알림은 따로 띄우지 않는다. 목록이 늘 화면에 있고 그 줄에 체크가 들어가므로
        // 토스트까지 내면 같은 사실을 두 번 말하면서 안내 말풍선과 자리를 다툰다.
        RebuildVisibleObjectives();
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
        TryAnnounceBossPreparation();
    }

    /// <summary>
    /// 마지막 날의 목표를 다 채운 순간, 이제 무엇을 하면 되는지 한 번 짚어 준다.
    ///
    /// 목록에서 체크가 전부 들어간 것만으로는 "밤 버튼이 풀렸다"가 전달되지 않는다 - 그 전까지
    /// 버튼을 눌러도 막히기만 했으므로, 플레이어는 아직 잠겨 있다고 여기고 다시 누르지 않는다.
    /// 챕터 컷이 아니라 알림인 이유는 이 시점이 자유 조작 구간이기 때문이다. 딤을 치면
    /// 정작 하라고 한 일(타워 더 짓기)을 못 한다.
    /// </summary>
    private void TryAnnounceBossPreparation()
    {
        if (_hasAnnouncedBossPreparation ||
            CurrentDayNumber != _bossPreparationDayNumber ||
            CurrentRun == null ||
            RemainingObjectiveCount > 0)
        {
            return;
        }

        // 목록이 아직 비어 있으면(에셋 배선 누락) 다 채운 것이 아니라 셀 것이 없는 것이다.
        if (_visibleObjectives.Count == 0)
        {
            return;
        }

        _hasAnnouncedBossPreparation = true;

        if (_toast != null)
        {
            _toast.ShowFor(_bossPreparationShowSeconds, BOSS_PREPARATION_LOC_KEY);
        }
    }
}
