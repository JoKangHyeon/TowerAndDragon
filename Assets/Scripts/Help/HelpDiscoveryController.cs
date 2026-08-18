using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임 이벤트를 듣다가 아직 만난 적 없는 요소를 발견하면 도감에 등록하고 설명 팝업을 띄운다.
/// 판정 로직은 TutorialObjectiveController와 같은 형태지만 상속하지 않는다 -
/// 저쪽은 "런 하나 동안의 진행"이고 이쪽은 "플레이어 평생의 이력"이라 수명이 다르고,
/// 합치면 튜토리얼 씬에만 있는 컴포넌트가 본게임의 필수 의존이 된다.
/// 실제 대상 판정은 양쪽 다 TutorialTargetMatcher에 위임하므로 중복되는 것은 배선뿐이다.
///
/// <b>SampleScene에만 둔다.</b> 튜토리얼 씬에는 이 컴포넌트가 없으므로 그쪽에서는 해금이 일어나지 않는다.
/// </summary>
public sealed class HelpDiscoveryController : MonoBehaviour
{
    [Tooltip("해금 후보 전체 목록.")]
    [SerializeField] private HelpCatalogSO _catalog;

    [Tooltip("최초 조우 설명을 띄울 팝업.")]
    [SerializeField] private UI_HelpPopup _popup;

    [Header("해금 조건을 듣는 대상")]
    [Tooltip("무장 시점과 밤 보류 판정에 쓴다. 없으면 팝업이 영영 뜨지 않는다.")]
    [WiringOptional]
    [SerializeField] private CycleManager _cycleManager;

    [WiringOptional]
    [SerializeField] private GridMap _gridMap;

    [WiringOptional]
    [SerializeField] private ResourceManager _resourceManager;

    [WiringOptional]
    [SerializeField] private PopulationManager _populationManager;

    [WiringOptional]
    [SerializeField] private ResearchManager _researchManager;

    [WiringOptional]
    [SerializeField] private ConquestManager _conquestManager;

    [WiringOptional]
    [SerializeField] private LandmarkManager _landmarkManager;

    [WiringOptional]
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [WiringOptional]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    [WiringOptional]
    [SerializeField] private WaveCycleProgression _cycleProgression;

    [WiringOptional]
    [SerializeField] private UIManager _uiManager;

    // 속성 변경 알림이 실제 변경인지 새날·복원 갱신인지 가르는 기준값
    // (TutorialObjectiveController.HandleAttributeChanged와 같은 처리).
    private DragonType? _lastSeenAttribute;

    // 팝업을 띄워도 되는 상태인가. 세이브 복원이 평소와 같은 이벤트를 대량으로 발화하기 때문에
    // 필요하다(RestoreAmounts는 카탈로그 전 종류에, RestoreBuilding은 건물마다 쏜다).
    //
    // 세이브에 그 자원·건물이 있다는 것은 그 플레이에서 이미 만났다는 뜻이므로 <b>등록은 옳고
    // 팝업만 틀리다</b> - 미무장 구간에서도 해금은 그대로 하고 팝업만 건너뛴다.
    private bool _isArmed;
    private bool _isArmScheduled;

    // 랜드마크 점령 한 번으로 여러 조건이 같은 프레임에 터진다. 팝업은 하나씩 순서대로 보여준다.
    private readonly Queue<HelpEntrySO> _pendingPopups = new();

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleDayReady);
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleResearchNodeCompleted);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        // 이 이벤트들은 인라인 초기화가 없어 인스펙터에서 접혀 있으면 null일 수 있다.
        if (_conquestManager != null && _conquestManager.OnConquestCompleted != null)
        {
            _conquestManager.OnConquestCompleted.AddListener(HandleConquestCompleted);
        }

        if (_conquestManager != null && _conquestManager.OnExpeditionSent != null)
        {
            _conquestManager.OnExpeditionSent.AddListener(HandleExpeditionSent);
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

            // 구독 직후 현재 값을 한 번 반영해 둔다 - 이 값이 없으면 구독 후 첫 발화가
            // 새날 갱신인지 실제 변경인지 가릴 수 없다.
            _lastSeenAttribute = _dragonTreeManager.ActiveAttribute;
        }

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.AddListener(HandleWaveCycleCompleted);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.AddListener(HandleExclusiveModeOpened);
        }

        if (_popup != null)
        {
            _popup.Closed.AddListener(HandlePopupClosed);
        }
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.RemoveListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleDayReady);
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchNodeCompleted);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_conquestManager != null && _conquestManager.OnConquestCompleted != null)
        {
            _conquestManager.OnConquestCompleted.RemoveListener(HandleConquestCompleted);
        }

        if (_conquestManager != null && _conquestManager.OnExpeditionSent != null)
        {
            _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
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
        }

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.RemoveListener(HandleWaveCycleCompleted);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.RemoveListener(HandleExclusiveModeOpened);
        }

        if (_popup != null)
        {
            _popup.Closed.RemoveListener(HandlePopupClosed);
        }
    }

    // 처음부터 열려 있어야 하는 항목(조작키·낮밤 개념)은 조건 없이 등록한다. 팝업은 띄우지 않는다 -
    // 아직 무장 전이므로 큐에도 들어가지 않는다.
    private void Start()
    {
        if (!WiringGuard.Require(_catalog, nameof(_catalog), this))
        {
            return;
        }

        foreach (HelpEntrySO entry in _catalog.Entries)
        {
            if (entry != null && entry.UnlockedFromStart)
            {
                HelpProfile.TryUnlock(entry.EntryId);
            }
        }
    }

    /// <summary>
    /// 무장 신호로 OnDayReady를 고른 이유: 새 게임(StartDay)·이어하기(ResumeDay)·
    /// 로드 실패 폴백(StartNewRun) 세 경로가 전부 여기를 지난다.
    /// SaveService.LoadCompleted는 로드가 실패하면 발화하지 않아 그 경로에서 영영 무장되지 않고,
    /// SaveRestore.Apply는 ResumeDay보다 앞서므로 복원 이벤트는 전부 무장 전에 떨어진다.
    /// </summary>
    private void HandleDayReady(int dayNumber)
    {
        ScheduleArming();
    }

    private void ScheduleArming()
    {
        if (_isArmed || _isArmScheduled)
        {
            return;
        }

        _isArmScheduled = true;
        ArmNextFrameAsync().Forget();
    }

    // 복원 이벤트가 OnDayReady와 같은 프레임에 남아 있을 수 있으므로 한 프레임 미룬다.
    private async UniTaskVoid ArmNextFrameAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        _isArmed = true;
    }

    private void HandleBuildingAdded(Building building)
    {
        DiscoverMatching(TutorialConditionType.BuildingConstructed,
            entry => entry.UnlockTrigger.MatchesBuilding(building));
    }

    // 넘어오는 값은 변경 후 보유량이다. 복원은 보유량 0인 자원까지 전부 쏘므로 수량을 반드시 본다.
    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        DiscoverMatching(TutorialConditionType.ResourceGained,
            entry => entry.UnlockTrigger.TargetResource == type);
    }

    // 어느 노드인지 가리지 않는다 - 도감 항목은 "연구란 무엇인가"를 설명하는 것이지
    // 특정 연구를 가리키지 않는다.
    private void HandleResearchNodeCompleted(ResearchNodeData _) =>
        DiscoverMatching(TutorialConditionType.ResearchNodeCompleted, null);

    private void HandleConquestCompleted(Vector2Int _) =>
        DiscoverMatching(TutorialConditionType.ConquestCompleted, null);

    private void HandleExpeditionSent(Vector2Int _, ResourceCost __) =>
        DiscoverMatching(TutorialConditionType.ExpeditionSent, null);

    // 총량으로 본다 - 한 명이라도 어딘가에 들어가 있으면 인구 배치를 해 본 것으로 친다.
    private void HandlePopulationChanged(PopulationState _)
    {
        if (_populationManager != null && _populationManager.AssignedPopulation > 0)
        {
            DiscoverMatching(TutorialConditionType.AnyPopulationAssigned, null);
        }
    }

    private void HandleLandmarkClaimed(LandmarkDataSO _) =>
        DiscoverMatching(TutorialConditionType.LandmarkClaimed, null);

    private void HandleEggGranted(DragonType type) =>
        DiscoverMatching(TutorialConditionType.DragonEggGranted,
            entry => entry.UnlockTrigger.MatchesDragon(type));

    private void HandleEggHatched(DragonType type) =>
        DiscoverMatching(TutorialConditionType.DragonEggHatched,
            entry => entry.UnlockTrigger.MatchesDragon(type));

    private void HandleWaveCycleCompleted(int _) =>
        DiscoverMatching(TutorialConditionType.WaveCycleCompleted, null);

    private void HandleNightStart(int _) =>
        DiscoverMatching(TutorialConditionType.NightStarted, null);

    private void HandleExclusiveModeOpened(MonoBehaviour mode) =>
        DiscoverMatching(TutorialConditionType.ExclusiveModeOpened,
            entry => TutorialTargetMatcher.MatchesMode(mode, entry.UnlockTrigger.TargetMode));

    // 이 이벤트는 새날 갱신·복원으로도 발화하므로, 값이 실제로 달라졌을 때만 실제 변경으로 본다.
    private void HandleAttributeChanged(DragonType type)
    {
        bool isRealChange = _lastSeenAttribute.HasValue && _lastSeenAttribute.Value != type;
        _lastSeenAttribute = type;

        if (isRealChange)
        {
            DiscoverMatching(TutorialConditionType.MotherDragonAttributeChanged,
                entry => entry.UnlockTrigger.MatchesDragon(type));
        }
    }

    private void DiscoverMatching(TutorialConditionType condition, Predicate<HelpEntrySO> extraFilter)
    {
        if (_catalog == null)
        {
            return;
        }

        foreach (HelpEntrySO entry in _catalog.Entries)
        {
            if (entry == null || entry.UnlockedFromStart || entry.UnlockTrigger.Condition != condition)
            {
                continue;
            }

            if (extraFilter != null && !extraFilter(entry))
            {
                continue;
            }

            Discover(entry);
        }
    }

    private void Discover(HelpEntrySO entry)
    {
        // 이미 만난 항목이면 false - 팝업이 두 번 뜨지 않는 것은 여기 한 곳에서 보장된다.
        if (!HelpProfile.TryUnlock(entry.EntryId))
        {
            return;
        }

        if (!_isArmed || !entry.AutoPopup)
        {
            return;
        }

        _pendingPopups.Enqueue(entry);
        TryShowNextPopup();
    }

    private void HandlePopupClosed()
    {
        TryShowNextPopup();
    }

    private void TryShowNextPopup()
    {
        // 밤이라고 미루지 않는다. 팝업이 열려 있는 동안 GameSpeedManager의 창 정지로 시간이 멈추므로
        // 읽는 사이에 성이 깨지지 않고, 무엇보다 "밤과 웨이브" 같은 안내는 그 밤이 시작될 때 떠야
        // 쓸모가 있다 - 다음 날 아침에 뜨면 이미 겪고 난 뒤라 설명이 될 수 없다.
        if (_popup == null || _popup.IsOpen || _pendingPopups.Count == 0)
        {
            return;
        }

        _popup.Show(_pendingPopups.Dequeue());
    }
}
