using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 가이드 퀘스트의 완료 판정과 목록 상태를 소유한다.
/// 구조는 <see cref="HelpDiscoveryController"/>·<see cref="TutorialObjectiveController"/>와 같지만
/// 코드를 공유하지 않는다 - 저 둘은 각각 "플레이어 평생 이력"과 "튜토리얼 씬 안의 학습 목표"이고,
/// 이쪽은 <b>본게임 한 판의 극초반 빌드업 체크리스트</b>라 수명도 목적도 다르다.
/// 실제 대상 판정은 셋 다 <see cref="TutorialTargetMatcher"/>에 위임하므로 겹치는 것은 배선뿐이다.
///
/// <b>가이드 수준과 무관하게 항상 켜 둔다.</b> "안내 없음"으로 꺼 두었다가 나중에 켰을 때
/// 이미 한 일이 체크돼 있어야 하기 때문이다. 수준이 가르는 것은 표시뿐이다.
///
/// 완료 판정은 <b>이벤트로만</b> 한다. 진입 시점 스냅샷을 뜨지 않는 이유는 성이 게임 시작 시
/// GridMap에 등록되며 건설과 같은 이벤트 경로를 지나기 때문이다 - 스냅샷을 넣으면 시작하자마자
/// 건설 퀘스트가 완료된다.
/// </summary>
public sealed class GuideQuestController : MonoBehaviour
{
    private const int FIRST_DAY_NUMBER = 1;

    [Tooltip("추적할 퀘스트 전체 목록.")]
    [SerializeField] private GuideQuestCatalogSO _catalog;

    [Tooltip("완료 기록을 남길 런. 없으면 완료가 기록되지 않는다.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("권장 일차와 지각 판정에 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("보상을 지급한다. 보상이 붙은 퀘스트가 없으면 비워도 된다.")]
    [WiringOptional]
    [SerializeField] private ResourceManager _resourceManager;

    [Header("완료 조건을 듣는 대상")]
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [WiringOptional]
    [SerializeField] private LandmarkManager _landmarkManager;

    [WiringOptional]
    [SerializeField] private WaveCycleProgression _cycleProgression;

    [Tooltip("용 스킬을 실제로 썼는지 듣는다.")]
    [WiringOptional]
    [SerializeField] private SkillTargetingController _skillTargetingController;

    [Tooltip("자원 예상 증감을 묻는다. 자원 UI와 같은 출처를 써야 표기와 판정이 어긋나지 않는다.")]
    [WiringOptional]
    [SerializeField] private ResourceForecast _resourceForecast;

    /// <summary>
    /// 목록에 보일 것이 바뀌었다(퀘스트가 열렸거나 완료됐거나 날짜가 바뀌었다).
    /// 표시하는 쪽은 이것만 구독하고 무엇이 왜 바뀌었는지는 다시 물어본다 -
    /// 인자로 넘기면 표시가 늘 때마다 시그니처를 고쳐야 한다.
    /// </summary>
    public UnityEvent QuestsChanged = new();

    /// <summary>퀘스트 하나가 방금 완료됐다. 알림을 띄우는 쪽이 구독한다.</summary>
    public UnityEvent<GuideQuestSO> QuestCompleted = new();

    // 매번 새로 만들면 구독자가 갱신마다 리스트를 할당하게 된다.
    private readonly List<GuideQuestSO> _visibleQuests = new();

    // 이미 경고를 낸 퀘스트. 판정은 이벤트마다 돌아가므로 가드가 없으면 같은 경고가 로그를 덮는다.
    private readonly HashSet<GuideQuestSO> _warnedQuests = new();

    // 속성 변경 알림이 실제 변경인지 새날·복원 갱신인지 가르는 기준값
    // (TutorialObjectiveController.HandleAttributeChanged와 같은 처리).
    private DragonType? _lastSeenAttribute;

    /// <summary>오늘 목록에 보일 퀘스트들. 지난 퀘스트는 완료 여부와 무관하게 남는다.</summary>
    public IReadOnlyList<GuideQuestSO> VisibleQuests => _visibleQuests;

    public int CurrentDayNumber =>
        _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

    /// <summary>
    /// 조언자에게 답했는가. 목록은 이 답을 받은 뒤에 열린다 - 가이드 수준을 고르기도 전에
    /// 목록부터 떠 있으면 "안내 없음"을 고르려던 사람에게 이미 안내를 들이민 셈이 된다.
    /// </summary>
    public bool IsIntroAnswered => Run != null && Run.IsGuideIntroAnswered;

    private RunData Run => _gameManager == null ? null : _gameManager.CurrentRun;

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoved);
            _gridMap.OnBuildingMoved.AddListener(HandleBuildingMoved);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleResearchNodeCompleted);
        }

        // 이 이벤트들은 인라인 초기화가 없어 인스펙터에서 접혀 있으면 null일 수 있다.
        if (_conquestManager != null && _conquestManager.OnExpeditionSent != null)
        {
            _conquestManager.OnExpeditionSent.AddListener(HandleExpeditionSent);
        }

        if (_conquestManager != null && _conquestManager.OnConquestCompleted != null)
        {
            _conquestManager.OnConquestCompleted.AddListener(HandleConquestCompleted);
        }

        if (_landmarkManager != null)
        {
            _landmarkManager.OnLandmarkClaimed.AddListener(HandleLandmarkClaimed);
        }

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatched);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleAttributeChanged);
            _dragonTreeManager.NodeUnlocked.AddListener(HandleDragonSkillUnlocked);

            // 구독 직후 현재 값을 한 번 반영해 둔다 - 이 값이 없으면 구독 후 첫 발화가
            // 새날 갱신인지 실제 변경인지 가릴 수 없다.
            _lastSeenAttribute = _dragonTreeManager.ActiveAttribute;
        }

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.AddListener(HandleWaveCycleCompleted);
        }

        if (_skillTargetingController != null)
        {
            _skillTargetingController.SkillUsed.AddListener(HandleSkillUsed);
        }

        if (_resourceForecast != null && _resourceForecast.ForecastChanged != null)
        {
            _resourceForecast.ForecastChanged.AddListener(HandleForecastChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleDayReady);
            _cycleManager.OnNightEnd.AddListener(HandleNightEnd);
        }
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoved);
            _gridMap.OnBuildingMoved.RemoveListener(HandleBuildingMoved);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchNodeCompleted);
        }

        if (_conquestManager != null && _conquestManager.OnExpeditionSent != null)
        {
            _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
        }

        if (_conquestManager != null && _conquestManager.OnConquestCompleted != null)
        {
            _conquestManager.OnConquestCompleted.RemoveListener(HandleConquestCompleted);
        }

        if (_landmarkManager != null)
        {
            _landmarkManager.OnLandmarkClaimed.RemoveListener(HandleLandmarkClaimed);
        }

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatched);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleAttributeChanged);
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleDragonSkillUnlocked);
        }

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.RemoveListener(HandleWaveCycleCompleted);
        }

        if (_skillTargetingController != null)
        {
            _skillTargetingController.SkillUsed.RemoveListener(HandleSkillUsed);
        }

        if (_resourceForecast != null && _resourceForecast.ForecastChanged != null)
        {
            _resourceForecast.ForecastChanged.RemoveListener(HandleForecastChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleDayReady);
            _cycleManager.OnNightEnd.RemoveListener(HandleNightEnd);
        }
    }

    // 첫 발화는 Start 이후로 미룬다(CLAUDE.md 이벤트 초기화 규칙).
    private void Start()
    {
        RebuildVisibleQuests();
    }

    public bool IsCompleted(GuideQuestSO quest)
    {
        return quest != null && Run != null && Run.HasCompletedGuideQuest(quest.QuestId);
    }

    /// <summary>
    /// 그날 안에 끝냈어야 하는데 아직 못 한 항목인가. 목록이 이 값으로 경고색과 정렬을 정한다.
    /// 강제하지 않는 대신 놓친 것이 계속 보이게 하는 유일한 장치다 - 이것이 없으면
    /// 1일차 체크리스트를 완전하게 만든 의미가 사라진다.
    /// 상태에서 매번 계산하므로 저장하지 않는다.
    /// </summary>
    public bool IsOverdue(GuideQuestSO quest)
    {
        return quest != null &&
               quest.IsCoreStep &&
               quest.RecommendedDay < CurrentDayNumber &&
               !IsCompleted(quest);
    }

    /// <summary>
    /// 퀘스트를 완료로 기록하고 보상을 준다. 이미 완료한 퀘스트면 false -
    /// <b>보상 중복 지급을 막는 관문이 여기 하나뿐이므로</b> 완료 경로는 전부 이 메서드를 지나야 한다.
    /// 조언자 퀘스트처럼 조건 없이 끝나는 것은 바깥(GuideQuestIntroPresenter)에서 직접 부른다.
    /// </summary>
    public bool TryCompleteQuest(GuideQuestSO quest)
    {
        if (quest == null || Run == null || !Run.TryCompleteGuideQuest(quest.QuestId))
        {
            return false;
        }

        GrantRewards(quest.Rewards);
        RebuildVisibleQuests();
        QuestCompleted.Invoke(quest);
        return true;
    }

    /// <summary>
    /// 자원 보상을 지급한다. 퀘스트 완료 보상과 선택지 보상이 같은 경로를 쓰도록 열어 둔다.
    /// 중복 방지는 부르는 쪽 책임이다 - 여기서는 주기만 한다.
    /// </summary>
    public void GrantRewards(IReadOnlyList<ResourceAmount> rewards)
    {
        if (_resourceManager == null || rewards == null || rewards.Count == 0)
        {
            return;
        }

        _resourceManager.Add(rewards);
    }

    /// <summary>
    /// 오늘 보여야 할 목록을 다시 만든다. 권장 일차가 지난 퀘스트는 <b>완료 여부와 무관하게</b> 남는다 -
    /// 못 한 것을 지우는 목록이 아니라 아직 남았다고 보여주는 목록이기 때문이다.
    /// </summary>
    private void RebuildVisibleQuests()
    {
        _visibleQuests.Clear();

        if (_catalog == null)
        {
            return;
        }

        int today = CurrentDayNumber;

        foreach (GuideQuestSO quest in _catalog.Quests)
        {
            if (quest != null && quest.RecommendedDay <= today)
            {
                _visibleQuests.Add(quest);
            }
        }

        QuestsChanged.Invoke();
    }

    private void HandleDayReady(int _) => RebuildVisibleQuests();

    private void HandleBuildingAdded(Building building) =>
        TryCompleteMatching(TutorialConditionType.BuildingConstructed,
            quest => quest.CompletionTrigger.MatchesBuilding(building));

    private void HandleBuildingRemoved(Building building) =>
        TryCompleteMatching(TutorialConditionType.BuildingRemoved,
            quest => quest.CompletionTrigger.MatchesBuilding(building));

    private void HandleBuildingMoved(Building building) =>
        TryCompleteMatching(TutorialConditionType.BuildingMoved,
            quest => quest.CompletionTrigger.MatchesBuilding(building));

    // 어느 노드인지 가리지 않는다 - 퀘스트는 "연구를 굴린다"이지 특정 연구가 아니다.
    private void HandleResearchNodeCompleted(ResearchNodeData _) =>
        TryCompleteMatching(TutorialConditionType.ResearchNodeCompleted, null);

    private void HandleExpeditionSent(Vector2Int _, ResourceCost __) =>
        TryCompleteMatching(TutorialConditionType.ExpeditionSent, null);

    private void HandleConquestCompleted(Vector2Int _) =>
        TryCompleteMatching(TutorialConditionType.ConquestCompleted, null);

    private void HandleLandmarkClaimed(LandmarkDataSO _) =>
        TryCompleteMatching(TutorialConditionType.LandmarkClaimed, null);

    private void HandleEggGranted(DragonType _) =>
        TryCompleteMatching(TutorialConditionType.DragonEggGranted, null);

    private void HandleEggHatched(DragonType type) =>
        TryCompleteMatching(TutorialConditionType.DragonEggHatched,
            quest => quest.CompletionTrigger.MatchesDragon(type));

    // 어느 스킬인지 가리지 않는다 - 퀘스트는 "용을 전력으로 만든다"이지 특정 스킬이 아니다.
    // 세이브 복원이 이 이벤트를 재발화하지만(RestoreUnlockedNodes) 그때 완료되는 것도 옳다 -
    // 그 런에서 이미 해금했다는 뜻이기 때문이다.
    private void HandleDragonSkillUnlocked(ProgressionNodeData _) =>
        TryCompleteMatching(TutorialConditionType.DragonSkillUnlocked, null);

    private void HandleSkillUsed(Skill _) =>
        TryCompleteMatching(TutorialConditionType.SkillUsed, null);

    // 예상 증감이 바뀔 때마다 확인한다. "지금 흑자인가"는 상태이므로 이벤트 시점이 언제든 결과가 같다.
    private void HandleForecastChanged() =>
        TryCompleteMatching(TutorialConditionType.ResourceForecastNonNegative, IsForecastNonNegative);

    private bool IsForecastNonNegative(GuideQuestSO quest)
    {
        // 배선이나 데이터가 빠지면 이 퀘스트는 조용히 영영 미완료로 남는다 - 그 사실을 알린다.
        if (_resourceForecast == null)
        {
            WarnOnce(quest, $"{nameof(_resourceForecast)}가 배선되지 않아 예상 증감을 물어볼 수 없습니다.");
            return false;
        }

        ResourceType type = quest.CompletionTrigger.TargetResource;

        // 자원을 지정하지 않았으면 무엇을 흑자로 만들라는 것인지 알 수 없다.
        if (type == ResourceType.None)
        {
            WarnOnce(quest, "흑자로 만들 자원(TargetResource)을 지정하지 않았습니다.");
            return false;
        }

        return _resourceForecast.GetDailyNetChange(type) >= 0;
    }

    /// <summary>
    /// 퀘스트 하나당 한 번만 경고한다. 판정은 게임 이벤트마다 돌아가므로 그냥 LogWarning을 부르면
    /// 같은 문구가 초당 수십 줄씩 쌓여 정작 봐야 할 로그를 덮는다.
    /// </summary>
    private void WarnOnce(GuideQuestSO quest, string reason)
    {
        if (quest == null || !_warnedQuests.Add(quest))
        {
            return;
        }

        Debug.LogWarning(
            $"[GuideQuestController] {quest.name}은 완료될 수 없습니다 - {reason}", quest);
    }

    private void HandleWaveCycleCompleted(int _) =>
        TryCompleteMatching(TutorialConditionType.WaveCycleCompleted, null);

    private void HandleNightEnd(int _) =>
        TryCompleteMatching(TutorialConditionType.NightSurvived, null);

    private void HandlePopulationChanged(PopulationState _)
    {
        // 이 둘은 건물 종류를 가려야 하므로 아래 CountProgress가 그리드를 직접 센다 -
        // PopulationChanged가 넘기는 것은 전체 합계뿐이라 그것만 보면 농장에 넣어도 "타워에 배치"가 통과한다.
        TryCompleteMatching(TutorialConditionType.PopulationAssignedToBuilding, null);
        TryCompleteMatching(TutorialConditionType.SelectedBuildingFullyStaffed, null);

        TryCompleteMatching(TutorialConditionType.AllPopulationAssigned, _ => HasNoIdlePopulation());
    }

    /// <summary>
    /// 놀고 있는 인구가 하나도 없는가. 식량 유지비가 배치 인구가 아니라 <b>총인구</b> 기준이라
    /// (PopulationUpkeepSystem) 미배치 인구는 매일 식량만 먹는 순손실이다.
    /// 총인구 0인 시작 프레임에 통과해 버리지 않도록 인구가 있는지도 함께 본다.
    /// </summary>
    private bool HasNoIdlePopulation()
    {
        return _populationManager != null &&
               _populationManager.MaxPopulation > 0 &&
               _populationManager.AvailablePopulation <= 0;
    }

    // 이 이벤트는 새날 갱신·복원으로도 발화하므로, 값이 실제로 달라졌을 때만 실제 변경으로 본다.
    private void HandleAttributeChanged(DragonType type)
    {
        bool isRealChange = _lastSeenAttribute.HasValue && _lastSeenAttribute.Value != type;
        _lastSeenAttribute = type;

        if (isRealChange)
        {
            TryCompleteMatching(TutorialConditionType.MotherDragonAttributeChanged, null);
        }
    }

    private void TryCompleteMatching(TutorialConditionType condition, Predicate<GuideQuestSO> extraFilter)
    {
        if (_catalog == null)
        {
            return;
        }

        foreach (GuideQuestSO quest in _catalog.Quests)
        {
            if (quest == null || quest.CompletionTrigger.Condition != condition || IsCompleted(quest))
            {
                continue;
            }

            if (extraFilter != null && !extraFilter(quest))
            {
                continue;
            }

            if (CountProgress(quest) < quest.RequiredCount)
            {
                continue;
            }

            TryCompleteQuest(quest);
        }
    }

    /// <summary>
    /// 지금까지 몇 개나 해냈는지. <b>이벤트 횟수를 세지 않고 지금 세계의 상태를 센다</b> -
    /// 카운터를 들고 있으면 세이브·로드에서 진행도가 어긋나고 저장할 필드가 늘어나는데,
    /// 상태를 세면 그 둘이 모두 사라진다.
    ///
    /// 상태로 셀 수 없는 조건(원정 발진처럼 지나가면 흔적이 남지 않는 사건)은 1만 돌려준다.
    /// 그런 조건에 <see cref="GuideQuestSO.RequiredCount"/>를 2 이상 주면 영영 완료되지 않으므로,
    /// 퀘스트 에셋 쪽에서 1로 두어야 한다.
    /// </summary>
    private int CountProgress(GuideQuestSO quest)
    {
        switch (quest.CompletionTrigger.Condition)
        {
            case TutorialConditionType.BuildingConstructed:
                return CountBuildings(quest.CompletionTrigger, CountMode.Any);

            case TutorialConditionType.PopulationAssignedToBuilding:
                return CountBuildings(quest.CompletionTrigger, CountMode.Staffed);

            case TutorialConditionType.SelectedBuildingFullyStaffed:
                return CountBuildings(quest.CompletionTrigger, CountMode.FullyStaffed);

            case TutorialConditionType.ResearchNodeCompleted:
                return _researchManager == null ? 0 : _researchManager.CompletedNodeIds.Count;

            default:
                // 상태로 셀 수 없는 사건이다. 이벤트가 왔다는 것 자체가 1이다.
                if (quest.RequiredCount > 1)
                {
                    WarnOnce(quest,
                        $"{quest.CompletionTrigger.Condition} 조건은 지금 상태로 개수를 셀 수 없습니다 - " +
                        "필요 개수를 1로 두거나 CountProgress에 세는 방법을 추가하세요.");
                    return 0;
                }

                return 1;
        }
    }

    private enum CountMode
    {
        Any,
        Staffed,
        FullyStaffed,
    }

    private int CountBuildings(TutorialTriggerSpec trigger, CountMode mode)
    {
        if (_gridMap == null)
        {
            return 0;
        }

        int count = 0;

        foreach (Building building in _gridMap.Buildings)
        {
            if (building == null || !trigger.MatchesBuilding(building))
            {
                continue;
            }

            if (mode == CountMode.Any)
            {
                count++;
                continue;
            }

            var target = building.GetComponent<IPopulationAllocationTarget>();

            if (target == null || !target.IsInitialized || target.AssignedPopulation <= 0)
            {
                continue;
            }

            bool isFull = target.AvailableCapacity <= 0;

            if (mode == CountMode.Staffed || isFull)
            {
                count++;
            }
        }

        return count;
    }
}
