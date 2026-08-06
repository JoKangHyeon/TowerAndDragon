using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 1일차 튜토리얼의 순서 진행과 완료 조건 감시. 표시는 UI_GuideOverlay에 맡기고,
/// 여기서는 "지금 몇 번째 단계인지"와 "그 단계가 언제 끝나는지"만 다룬다.
/// 이 컴포넌트의 활성 체크박스가 곧 튜토리얼 on/off 스위치다(새끼용 가이드와 같은 관례).
/// 도는 동안에는 아직 설명하지 않은 배타 창을 열지 못하게 막고(IExclusiveModeOpenQuery),
/// 토스트도 멈춰 둔다 - 안내와 다른 정보가 뒤섞이면 무엇을 하라는 것인지 알 수 없어진다.
/// 새끼용 가이드보다 우선순위가 높아 표시권을 빼앗기지 않으므로, 오버레이의 DisplayReleased는 구독하지 않는다 -
/// 구독하면 스스로 Release한 직후 다시 그리려 들어 끝난 안내가 되살아난다.
/// </summary>
public sealed class TutorialRunner : MonoBehaviour, IExclusiveModeOpenQuery, IDayEndBlockQuery
{
    private const float DEFAULT_HAND_OVER_DELAY = 1.5f;

    [SerializeField] private TutorialSequenceSO _sequence;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private UI_GuideOverlay _overlay;

    [Header("완료 조건 판정에 쓰는 관리자")]
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private BuildingPlacementController _placementController;

    [Tooltip("튜토리얼이 도는 동안 밤으로 넘어가지 못하게 막는 데 쓴다. 비우면 막지 않는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("점령 파병을 기다리는 단계에 필요하다.")]
    [SerializeField] private ConquestManager _conquestManager;

    [Tooltip("연구 해금을 기다리는 단계에 필요하다.")]
    [SerializeField] private ResearchManager _researchManager;

    [Tooltip("점령지 선택을 기다리는 단계에 필요하다.")]
    [SerializeField] private UI_ConquestWindow _conquestWindow;

    [Tooltip("인벤토리 슬롯을 가리키는 단계에 필요하다. 슬롯은 런타임 생성이라 앵커로 잡을 수 없다.")]
    [SerializeField] private UI_DragonInventoryWindow _dragonInventoryWindow;

    [Tooltip("건설 패널 슬롯을 가리키는 단계에 필요하다. 슬롯은 런타임 생성이라 GuideAnchor로 잡을 수 없다.")]
    [SerializeField] private UI_BuildModeWindow _buildModeWindow;

    [Tooltip("마지막 안내가 끝나고 다음 가이드에 넘기기까지 쉬는 시간(초). 0이면 곧바로 이어져 숨 돌릴 틈이 없다.")]
    [Min(0f)]
    [SerializeField] private float _handOverDelaySeconds = DEFAULT_HAND_OVER_DELAY;

    [Header("시작 자원 지급")]
    [Tooltip("행동형 단계는 그 행동이 실제로 가능해야 성립한다. 자원이 모자라면 안내해도 못 하고 단계가 영영 안 넘어간다.")]
    [SerializeField] private bool _grantStartingResources;

    [Tooltip("씬의 ResourceManager 초기 자원을 올리는 대신 튜토리얼이 지급한다 - 껐을 때 평소 밸런스가 오염되지 않아야 한다.")]
    [SerializeField] private List<ResourceAmount> _startingResources = new();

    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("끝났을 때 '튜토리얼을 봤음'으로 표시할지. 여러 챕터로 나눠 이어갈 때는 마지막 챕터만 켠다 - " +
             "RunData.IsTutorialDismissed는 런 전체에 하나뿐이라, 중간 챕터가 표시해버리면 " +
             "뒤 챕터가 '이미 봤음' 판정으로 아무것도 실행하지 않는다.")]
    [SerializeField] private bool _marksScenarioDismissedOnFinish = true;

    private int _currentIndex;
    private bool _isRunning;
    private bool _hasBegun;

    // 늦게 도착한 자동 진행을 버리고 조건 판정의 기준으로 쓴다.
    private TutorialStepSO _activeStep;

    // 인구 조건은 절대값이 아니라 진입 시점부터의 증가분으로 본다.
    private int _populationBaseline;

    // 안내가 지나간 창만 열 수 있다. 지금 단계의 것만 허용하면 플레이어가 그 창을 닫았을 때 다시 열 수 없어 갇힌다.
    private readonly HashSet<TutorialExclusiveModeKind> _unlockedModes = new();

    /// <summary>
    /// 튜토리얼이 끝났다(완주·건너뛰기·시작조차 안 함 모두 포함). 다음 가이드는 이 신호로 시작한다 -
    /// 종료 시점 하나에 매달면 완주와 건너뛰기가 같은 경로가 되어, 스킵했을 때만 순서가 이상해지는 일이 없다.
    /// 이 컴포넌트가 새끼용 같은 개별 도메인을 알지 않게 하기 위한 통로다.
    /// </summary>
    public UnityEvent TutorialEnded = new();


    private RunData CurrentRun => _gameManager == null ? null : _gameManager.CurrentRun;

    // 이 체크박스를 껐다 켜는 것이 디버그용 on/off다. 다시 켜면 멈춰 있던 단계부터 이어간다 -
    // Start는 이미 지나갔으므로 여기서 복구하지 않으면 튜토리얼이 되살아나지 않는다.
    private void OnEnable()
    {
        // 창 열림·밤 시작 판정은 Start가 아니라 여기서 건다 - Start끼리는 순서가 보장되지 않아
        // 첫 단계가 뜨기 전에 플레이어가 앞질러 갈 수 있으면 안 된다.
        if (_uiManager != null)
        {
            _uiManager.OpenQuery = this;
        }

        if (_cycleManager != null)
        {
            _cycleManager.DayEndBlockQuery = this;
        }

        if (_overlay != null)
        {
            _overlay.ConfirmClicked += HandleConfirmClicked;
        }

        RunData run = CurrentRun;
        if (!_hasBegun || (run != null && run.IsTutorialDismissed))
        {
            return;
        }

        _isRunning = true;
        EnterStep(_currentIndex);
    }


    /// <summary>
    /// 안내가 아직 설명하지 않은 창은 열지 않는다 - 튜토리얼 도중 점령·연구 창이 열리면
    /// 무엇을 하라는 안내인지 알 수 없게 된다. 한 번 안내한 창은 계속 열 수 있다.
    /// </summary>
    /// <summary>
    /// 튜토리얼이 도는 동안에는 밤으로 넘어가지 못한다. 되돌릴 수 없는 조작이고,
    /// 밤에는 건설·인구 배치가 전부 잠겨 남은 단계를 아무것도 할 수 없게 된다.
    /// 마지막 안내가 밤 버튼을 가리키므로 그 단계에서 풀어주는 방식도 가능하지만,
    /// 아직 준비가 안 된 플레이어가 잘못 눌러 밤을 맞이하는 쪽이 더 큰 손해라 끝까지 막는다.
    /// </summary>
    bool IDayEndBlockQuery.CanEndDay()
    {
        return !_isRunning;
    }

    bool IExclusiveModeOpenQuery.CanOpen(MonoBehaviour mode)
    {
        if (!_isRunning)
        {
            return true;
        }

        foreach (TutorialExclusiveModeKind unlocked in _unlockedModes)
        {
            if (MatchesMode(mode, unlocked))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 안내가 이 창 안을 가리키는 동안에는 단축키로 닫지 못하게 한다 - 대상이 사라지면
    /// 무엇을 하라는 안내인지 알 수 없다. "닫으세요" 단계에 이르면 그때 풀린다.
    /// </summary>
    bool IExclusiveModeOpenQuery.CanClose(MonoBehaviour mode)
    {
        if (!_isRunning || _activeStep == null)
        {
            return true;
        }

        // 닫으라고 시키는 단계면 당연히 닫을 수 있어야 한다.
        if (_activeStep.Condition == TutorialConditionType.ExclusiveModeClosed)
        {
            return true;
        }

        // 인벤토리 슬롯을 가리키는 단계는 TargetMode가 비어 있다(가리키는 것이 창이 아니라 그 안의 슬롯이라서).
        if (_activeStep.TargetsDragonInventorySlot &&
            MatchesMode(mode, TutorialExclusiveModeKind.BabyDragonInventory))
        {
            return false;
        }

        // 이 창 안을 가리키는 중이 아니면 막을 이유가 없다.
        return !MatchesMode(mode, _activeStep.TargetMode);
    }

    private void OnDisable()
    {
        UnsubscribeConditions();
        _isRunning = false;
        _activeStep = null;

        // 끄면 창·밤 잠금도 같이 풀어준다 - 안 그러면 영영 막힌 채로 남는다.
        ReleaseOpenQuery();

        // 끄면 떠 있던 딤·말풍선도 같이 걷는다. 새끼용 가이드가 기다리고 있었다면 이때 표시권을 넘겨받는다.
        if (_overlay != null)
        {
            _overlay.ConfirmClicked -= HandleConfirmClicked;
            _overlay.Release(this);
        }
    }

    // 확인 버튼은 오버레이가 공용이라 남의 단계에서도 눌릴 수 있다 - 지금 내 설명형 단계일 때만 받는다.
    private void HandleConfirmClicked()
    {
        if (_isRunning && _activeStep != null &&
            _activeStep.Kind == TutorialStepKind.Acknowledge && _activeStep.WaitForConfirm)
        {
            Advance();
        }
    }

    private void Start()
    {
        BeginAsync().Forget();
    }

    // DragonEggInventorySystem도 같은 이유로 알 지급을 한 프레임 미룬다 - 다른 오브젝트의 구독이
    // Start까지 끝난 뒤 시작해야 하고(CLAUDE.md 이벤트 규칙), 두 안내가 같은 프레임에 화면을 다투지 않아야 한다.
    private async UniTaskVoid BeginAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());

        // 시작하지 않기로 한 경우엔 잠금을 풀고, 튜토리얼에 매달린 일(알 지급 등)이 진행되도록 종료를 알린다.
        if (_sequence == null || _sequence.Steps.Count == 0)
        {
            Debug.LogWarning("[TutorialRunner] 시퀀스가 비어 있어 튜토리얼을 시작하지 않습니다.", this);
            ReleaseOpenQuery();
            TutorialEnded.Invoke();
            return;
        }

        RunData run = CurrentRun;
        if (run != null && run.IsTutorialDismissed)
        {
            ReleaseOpenQuery();
            TutorialEnded.Invoke();
            return;
        }

        GrantStartingResources();

        _hasBegun = true;
        _isRunning = true;

        // 토스트는 OnEnable에서 이미 멈춰 뒀다 - 여기서 멈추면 알 지급보다 늦을 수 있다.
        EnterStep(ResolveResumeIndex());
    }

    /// <summary>건너뛰기. 이 튜토리얼만 끝내고 새끼용 가이드는 그대로 살려둔다.</summary>
    public void Skip()
    {
        Finish();
    }

    // 이미 진입한 단계 중 가장 뒤에서 다시 시작한다 - "진입"을 기록하므로 그 단계를 한 번 더 보여주는 게 맞다.
    private int ResolveResumeIndex()
    {
        RunData run = CurrentRun;
        if (run == null)
        {
            return 0;
        }

        int resumeIndex = 0;
        for (int i = 0; i < _sequence.Steps.Count; i++)
        {
            TutorialStepSO step = _sequence.Steps[i];
            if (step != null && run.HasEnteredTutorialStep(step.StepId))
            {
                resumeIndex = i;
            }
        }

        return resumeIndex;
    }

    private void EnterStep(int index)
    {
        UnsubscribeConditions();

        if (index >= _sequence.Steps.Count)
        {
            Finish();
            return;
        }

        _currentIndex = index;
        _activeStep = _sequence.Steps[index];

        // 빈 칸에서 멈추면 그 뒤 단계가 전부 사라지므로 건너뛴다.
        if (_activeStep == null)
        {
            Debug.LogWarning($"[TutorialRunner] 시퀀스 {index}번 항목이 비어 있어 건너뜁니다.", this);
            Advance();
            return;
        }

        CurrentRun?.TryEnterTutorialStep(_activeStep.StepId);

        // 이 단계가 열라고 시키는 창은 이제부터 열 수 있다(그 뒤로도 계속).
        if (_activeStep.TargetMode != TutorialExclusiveModeKind.None)
        {
            _unlockedModes.Add(_activeStep.TargetMode);
        }

        CaptureConditionBaseline(_activeStep);

        // 안내보다 먼저 해버린 행동은 이 자리에서 흘려보낸다 - 이벤트만 기다리면
        // 이미 지나간 행동은 다시 오지 않아 그 단계에서 영영 멈춘다(창을 미리 닫은 경우 등).
        if (IsConditionAlreadySatisfied(_activeStep))
        {
            Advance();
            return;
        }

        SubscribeCondition(_activeStep);
        Render();

        // 확인 버튼을 쓰는 설명은 누를 때까지 기다린다(ConfirmClicked 구독). 그 외에는 시간으로 넘긴다.
        if (_activeStep.Kind == TutorialStepKind.Acknowledge && !_activeStep.WaitForConfirm)
        {
            AutoAdvanceAsync(_activeStep).Forget();
        }
    }

    private void Advance()
    {
        EnterStep(_currentIndex + 1);
    }

    private void Finish()
    {
        UnsubscribeConditions();
        _isRunning = false;
        _activeStep = null;
        ReleaseOpenQuery();

        // 끝까지 봤든 건너뛰었든 다시 뜨지 않는다. 다만 뒤에 이어질 챕터가 있으면 여기서 표시하지 않는다.
        RunData run = CurrentRun;
        if (run != null && _marksScenarioDismissedOnFinish)
        {
            run.IsTutorialDismissed = true;
        }

        // 마지막 단계를 끝낸 순간 화면은 곧바로 걷는다 - 딤이 남아 있으면 조작이 막힌 것처럼 보인다.
        // 다만 표시권은 계속 쥐고 있어야 다음 가이드가 알림과 겹쳐 뜨지 않는다.
        if (_overlay != null)
        {
            _overlay.Suspend(this);
        }

        HandOverAsync().Forget();
    }

    /// <summary>
    /// 마지막 안내를 읽을 틈을 두고 표시권을 넘긴다. 화면은 Finish에서 이미 걷었으므로 이 동안은 비어 있다.
    /// </summary>
    private async UniTaskVoid HandOverAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        if (_handOverDelaySeconds > 0f)
        {
            await UniTask.WaitForSeconds(_handOverDelaySeconds, ignoreTimeScale: true, cancellationToken: token);
        }

        // 기다리는 동안 컴포넌트가 꺼졌다면 OnDisable이 이미 정리했다.
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_overlay != null)
        {
            _overlay.Release(this);
        }

        // 인계 단계를 거치지 않고 끝났다면(건너뛰기 등) 여기서라도 매달린 일이 진행돼야 한다.
        TutorialEnded.Invoke();
    }

    // 남이 걸어둔 것을 지우지 않도록 내가 건 경우에만 뗀다.
    private void ReleaseOpenQuery()
    {
        if (_uiManager != null && ReferenceEquals(_uiManager.OpenQuery, this))
        {
            _uiManager.OpenQuery = null;
        }

        if (_cycleManager != null && ReferenceEquals(_cycleManager.DayEndBlockQuery, this))
        {
            _cycleManager.DayEndBlockQuery = null;
        }
    }

    private async UniTaskVoid AutoAdvanceAsync(TutorialStepSO step)
    {
        // 일시정지 중에도 안내는 흘러야 한다(오버레이·토스트의 SetUpdate(true)와 짝).
        await UniTask.WaitForSeconds(
            step.AutoAdvanceSeconds,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        // 기다리는 동안 단계가 바뀌었으면(건너뛰기 등) 늦게 도착한 이 호출은 버린다.
        if (_isRunning && _activeStep == step)
        {
            Advance();
        }
    }

    private void Render()
    {
        if (_overlay == null || _activeStep == null)
        {
            return;
        }

        RectTransform target = ResolveAnchor(_activeStep);

        // 대상을 못 찾았을 때 입력까지 막으면 오버레이가 아무것도 그리지 않는다 - 문구만이라도 띄운다.
        _overlay.Show(
            this,
            GuidePriority.DAY_ONE_TUTORIAL,
            target,
            _activeStep.MessageLocKey,
            // 대상이 없어도 확인 버튼이 있으면 화면 전체를 막을 수 있다 - 읽는 동안 뒤쪽이 눌리면 안 된다.
            _activeStep.BlocksInput && (target != null || _activeStep.ShowsConfirmButton),
            _activeStep.BlocksTargetInteraction,
            _activeStep.ShowsConfirmButton,
            _activeStep.BubbleSlot);
    }

    // 대상을 못 찾아도 단계는 진행돼야 하므로 문구만 띄우고(null), 나중에 나타나면 그때 다시 그린다.
    private RectTransform ResolveAnchor(TutorialStepSO step)
    {
        // 인벤토리 슬롯도 런타임 생성이라 창에서 찾아온다. 어느 탭이 열려 있느냐가 곧 어느 슬롯인지다.
        if (step.TargetsDragonInventorySlot)
        {
            if (_dragonInventoryWindow != null &&
                _dragonInventoryWindow.TryGetFirstSlotRect(out RectTransform inventorySlotRect))
            {
                return inventorySlotRect;
            }

            // 창이 닫혀 있거나 아직 안 그려졌다 - 다시 그려질 때 조준한다.
            SubscribeInventorySlotViewOnce();
            return null;
        }

        // 건설 패널 슬롯은 런타임 생성이라 앵커가 아니라 창에서 찾아온다. 지정돼 있으면 이쪽이 우선.
        if (step.TargetBuildingSlot != null)
        {
            if (_buildModeWindow != null &&
                _buildModeWindow.TryGetSlotRect(step.TargetBuildingSlot, out RectTransform slotRect))
            {
                return slotRect;
            }

            // 패널이 닫혀 있으면 슬롯이 아직 없다 - 다시 그려질 때 조준한다.
            SubscribeSlotViewOnce();
            return null;
        }

        if (step.AnchorId == GuideAnchorId.None)
        {
            return null;
        }

        if (GuideAnchorRegistry.TryGet(step.AnchorId, out RectTransform anchor))
        {
            return anchor;
        }

        // 중복 구독을 막고 나서 다시 건다 - 앵커를 못 찾은 단계가 연달아 나올 수 있다.
        GuideAnchorRegistry.AnchorRegistered -= HandleAnchorRegistered;
        GuideAnchorRegistry.AnchorRegistered += HandleAnchorRegistered;
        return null;
    }

    private void GrantStartingResources()
    {
        if (!_grantStartingResources)
        {
            return;
        }

        if (_resourceManager == null || _startingResources.Count == 0)
        {
            Debug.LogWarning("[TutorialRunner] 시작 자원 지급이 켜져 있는데 ResourceManager 또는 목록이 비어 있습니다.", this);
            return;
        }

        // ResourceManager.Add(type, amount)는 복합 플래그에 Assert가 걸리므로 종류별로 나뉜 목록을 그대로 넘긴다.
        _resourceManager.Add(_startingResources);
    }

    /// <summary>
    /// 인구 조건은 "총 배치 인구가 N 이상"이 아니라 "이 단계에서 N명을 새로 넣었다"여야 한다.
    /// PopulationChanged가 전체 합계만 주므로, 절대값으로 보면 성이나 기존 시설에 이미 인구가 있을 때
    /// 단계가 진입 즉시 통과하고 그대로 튜토리얼이 끝나버린다.
    /// </summary>
    private void CaptureConditionBaseline(TutorialStepSO step)
    {
        bool needsBaseline = step.Condition == TutorialConditionType.PopulationAssigned ||
                             step.Condition == TutorialConditionType.PopulationUnassigned;

        if (needsBaseline && _populationManager != null)
        {
            _populationBaseline = _populationManager.AssignedPopulation;
        }
    }

    /// <summary>
    /// 창 열기/닫기는 이벤트가 아니라 상태를 매 프레임 확인한다.
    /// 이벤트는 "그 순간"에만 오므로, 안내보다 먼저 한 행동이나 발행 경로가 하나라도 어긋나는 창에서는
    /// 신호를 놓쳐 그 단계에 갇힌다. 상태로 보면 어떤 경로로 여닫았든 결과가 같다.
    /// 나머지 조건(인구 증감·건설 등)은 상태로 되돌아볼 수 없어 이벤트를 그대로 쓴다.
    /// </summary>
    private void Update()
    {
        if (_isRunning && _activeStep != null && IsConditionAlreadySatisfied(_activeStep))
        {
            Advance();
        }
    }

    /// <summary>
    /// 지금 이 조건이 충족돼 있는지. 창 열기/닫기처럼 "상태"로 확인할 수 있는 것만 본다 -
    /// 인구 배치처럼 증가분으로 보는 조건은 진입 시점이 곧 기준이라 여기서 판정할 것이 없다.
    /// </summary>
    private bool IsConditionAlreadySatisfied(TutorialStepSO step)
    {
        if (step.Kind != TutorialStepKind.WaitForAction || _uiManager == null)
        {
            return false;
        }

        MonoBehaviour openMode = _uiManager.CurrentOpenExclusiveMode;

        switch (step.Condition)
        {
            case TutorialConditionType.ExclusiveModeOpened:
                return MatchesMode(openMode, step.TargetMode);

            case TutorialConditionType.ExclusiveModeClosed:
                return !MatchesMode(openMode, step.TargetMode);

            default:
                return false;
        }
    }

    private void SubscribeCondition(TutorialStepSO step)
    {
        if (step.Kind != TutorialStepKind.WaitForAction)
        {
            return;
        }

        switch (step.Condition)
        {
            case TutorialConditionType.ExclusiveModeOpened:
                if (_uiManager != null)
                {
                    _uiManager.ExclusiveModeOpened.AddListener(HandleExclusiveModeOpened);
                }
                break;

            case TutorialConditionType.ExclusiveModeClosed:
                if (_uiManager != null)
                {
                    _uiManager.ExclusiveModeClosed.AddListener(HandleExclusiveModeClosed);
                }
                break;

            case TutorialConditionType.BuildingSelectedForPlacement:
                if (_placementController != null)
                {
                    _placementController.BuildingToPlaceChanged.AddListener(HandleBuildingToPlaceChanged);
                }
                break;

            case TutorialConditionType.BuildingConstructed:
                if (_gridMap != null)
                {
                    _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
                }
                break;

            case TutorialConditionType.PopulationAssigned:
                if (_populationManager != null)
                {
                    _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
                }
                break;

            case TutorialConditionType.PopulationUnassigned:
                if (_populationManager != null)
                {
                    _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
                }
                break;

            // 인구를 넣어야 정원이 차므로 인구 변화를 본다.
            case TutorialConditionType.SelectedBuildingFullyStaffed:
                if (_populationManager != null)
                {
                    _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
                }
                break;

            case TutorialConditionType.BuildingSelectedOnGrid:
            case TutorialConditionType.BuildingDeselectedOnGrid:
                if (_placementController != null)
                {
                    _placementController.SelectedBuildingChanged.AddListener(HandleSelectedBuildingChanged);
                }
                break;

            case TutorialConditionType.BuildPanelTabSelected:
                if (_buildModeWindow != null)
                {
                    _buildModeWindow.OnTabSelected.AddListener(HandleTabSelected);
                }
                break;

            case TutorialConditionType.ExpeditionSent:
                if (_conquestManager != null)
                {
                    _conquestManager.OnExpeditionSent.AddListener(HandleExpeditionSent);
                }
                break;

            case TutorialConditionType.BuildingRemoved:
                if (_gridMap != null)
                {
                    _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoved);
                }
                break;

            case TutorialConditionType.BuildingMoved:
                if (_gridMap != null)
                {
                    _gridMap.OnBuildingMoved.AddListener(HandleBuildingMoved);
                }
                break;

            case TutorialConditionType.ResearchNodeCompleted:
                if (_researchManager != null)
                {
                    _researchManager.NodeCompleted.AddListener(HandleResearchNodeCompleted);
                }
                break;

            case TutorialConditionType.ConquestChunkSelected:
                if (_conquestWindow != null)
                {
                    _conquestWindow.ChunkSelected.AddListener(HandleChunkSelected);
                }
                break;

            case TutorialConditionType.DragonInventoryDragonTabSelected:
                if (_dragonInventoryWindow != null)
                {
                    _dragonInventoryWindow.OnTabDisplayed.AddListener(HandleDragonTabDisplayed);
                }
                break;

            default:
                Debug.LogWarning(
                    $"[TutorialRunner] 단계 '{step.StepId}'는 행동형인데 완료 조건이 없어 스스로 넘어가지 않습니다.", this);
                break;
        }
    }

    // 어느 조건을 걸었는지 따로 기억하지 않고 전부 떼어낸다 - 걸지 않은 리스너를 떼도 무해하고,
    // 종류가 늘 때 해제를 빼먹는 실수를 막는다.
    private void UnsubscribeConditions()
    {
        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.RemoveListener(HandleExclusiveModeOpened);
            _uiManager.ExclusiveModeClosed.RemoveListener(HandleExclusiveModeClosed);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoved);
            _gridMap.OnBuildingMoved.RemoveListener(HandleBuildingMoved);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchNodeCompleted);
        }

        if (_conquestWindow != null)
        {
            _conquestWindow.ChunkSelected.RemoveListener(HandleChunkSelected);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_placementController != null)
        {
            _placementController.BuildingToPlaceChanged.RemoveListener(HandleBuildingToPlaceChanged);
            _placementController.SelectedBuildingChanged.RemoveListener(HandleSelectedBuildingChanged);
        }

        if (_buildModeWindow != null)
        {
            _buildModeWindow.OnSlotViewChanged.RemoveListener(Render);
            _buildModeWindow.OnTabSelected.RemoveListener(HandleTabSelected);
        }

        if (_dragonInventoryWindow != null)
        {
            _dragonInventoryWindow.OnSlotViewChanged.RemoveListener(Render);
            _dragonInventoryWindow.OnTabDisplayed.RemoveListener(HandleDragonTabDisplayed);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
        }

        GuideAnchorRegistry.AnchorRegistered -= HandleAnchorRegistered;
    }

    // 고스트가 붙은 시점. 취소하면 null이 오므로 종류가 맞을 때만 넘어간다.
    private void HandleBuildingToPlaceChanged(Building prefab)
    {
        if (_isRunning && _activeStep != null && prefab != null && MatchesBuilding(prefab, _activeStep))
        {
            Advance();
        }
    }

    // 그리드의 건물을 클릭해 고른 시점. 선택이 풀리면 null이 온다(패널 닫기).
    private void HandleSelectedBuildingChanged(Building building)
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        bool isSatisfied = _activeStep.Condition == TutorialConditionType.BuildingDeselectedOnGrid
            ? building == null
            : building != null && MatchesBuilding(building, _activeStep);

        if (isSatisfied)
        {
            Advance();
        }
    }

    // 중복 구독을 막고 나서 다시 건다 - 슬롯을 못 찾은 단계가 연달아 나올 수 있다.
    private void SubscribeSlotViewOnce()
    {
        if (_buildModeWindow == null)
        {
            return;
        }

        _buildModeWindow.OnSlotViewChanged.RemoveListener(Render);
        _buildModeWindow.OnSlotViewChanged.AddListener(Render);
    }

    private void SubscribeInventorySlotViewOnce()
    {
        if (_dragonInventoryWindow == null)
        {
            return;
        }

        _dragonInventoryWindow.OnSlotViewChanged.RemoveListener(Render);
        _dragonInventoryWindow.OnSlotViewChanged.AddListener(Render);
    }

    // 어느 땅으로 보냈는지는 묻지 않는다 - 안내는 "파병하는 법"을 알려주는 것이지 목표를 지정하지 않는다.
    private void HandleExpeditionSent(Vector2Int _, ResourceCost __)
    {
        if (_isRunning && _activeStep != null)
        {
            Advance();
        }
    }

    // 이 단계가 가리키던 탭이 골라졌는지. 안내 대상과 같은 오브젝트인지로 판정하므로 탭 순서에 의존하지 않는다.
    private void HandleTabSelected(RectTransform tabRect)
    {
        if (!_isRunning || _activeStep == null || tabRect == null ||
            !GuideAnchorRegistry.TryGet(_activeStep.AnchorId, out RectTransform expected))
        {
            return;
        }

        if (tabRect == expected)
        {
            Advance();
        }
    }

    // 닫힌 창 안의 앵커는 진입 시점에 없다 - 창이 열려 등록되는 순간 그때 강조를 붙인다.
    private void HandleAnchorRegistered(GuideAnchorId id)
    {
        if (_isRunning && _activeStep != null && _activeStep.AnchorId == id)
        {
            GuideAnchorRegistry.AnchorRegistered -= HandleAnchorRegistered;
            Render();
        }
    }

    private void HandleExclusiveModeOpened(MonoBehaviour mode)
    {
        if (_isRunning && _activeStep != null && MatchesMode(mode, _activeStep.TargetMode))
        {
            Advance();
        }
    }

    private void HandleExclusiveModeClosed(MonoBehaviour mode)
    {
        if (_isRunning && _activeStep != null && MatchesMode(mode, _activeStep.TargetMode))
        {
            Advance();
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (_isRunning && _activeStep != null && MatchesBuilding(building, _activeStep))
        {
            Advance();
        }
    }

    private void HandleBuildingRemoved(Building building)
    {
        if (_isRunning && _activeStep != null && MatchesBuilding(building, _activeStep))
        {
            Advance();
        }
    }

    private void HandleBuildingMoved(Building building)
    {
        if (_isRunning && _activeStep != null && MatchesBuilding(building, _activeStep))
        {
            Advance();
        }
    }

    // 어느 연구를 골랐는지는 묻지 않는다 - 안내는 "연구하는 법"이지 특정 노드가 아니다.
    private void HandleResearchNodeCompleted(ResearchNodeData _)
    {
        if (_isRunning && _activeStep != null)
        {
            Advance();
        }
    }

    // 어느 땅을 골랐는지도 묻지 않는다 - 고르는 법을 알려주는 것이지 목표를 지정하지 않는다.
    private void HandleChunkSelected(Vector2Int _)
    {
        if (_isRunning && _activeStep != null)
        {
            Advance();
        }
    }

    // true가 용 탭이다. 알 탭으로 되돌아간 경우는 아직 시킨 것을 하지 않은 것이므로 넘기지 않는다.
    private void HandleDragonTabDisplayed(bool isDragonTab)
    {
        if (_isRunning && _activeStep != null && isDragonTab)
        {
            Advance();
        }
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        if (_activeStep == null || !_isRunning)
        {
            return;
        }

        int delta = state.AssignedPopulation - _populationBaseline;

        switch (_activeStep.Condition)
        {
            case TutorialConditionType.PopulationAssigned:
                if (delta >= _activeStep.RequiredPopulation)
                {
                    Advance();
                }
                break;

            case TutorialConditionType.PopulationUnassigned:
                if (-delta >= _activeStep.RequiredPopulation)
                {
                    Advance();
                }
                break;

            case TutorialConditionType.SelectedBuildingFullyStaffed:
                if (IsSelectedBuildingFullyStaffed())
                {
                    Advance();
                }
                break;
        }
    }

    /// <summary>
    /// 지금 고른 건물이 정원을 다 채웠는지. 전체 인구 합계로는 알 수 없어 그 건물의 배치 대상을 직접 읽는다.
    /// 타워·연구소·생산시설이 같은 인터페이스를 구현하므로 종류를 가리지 않는다.
    /// </summary>
    private bool IsSelectedBuildingFullyStaffed()
    {
        Building selected = _placementController == null ? null : _placementController.SelectedBuilding;
        if (selected == null)
        {
            return false;
        }

        var target = selected.GetComponent<IPopulationAllocationTarget>();

        // 정원이 0이면 "다 찼다"가 성립하지 않는다 - 초기화 전이거나 인구를 받지 않는 건물이다.
        return target != null && target.IsInitialized && target.Capacity > 0 &&
               target.AssignedPopulation >= target.Capacity;
    }

    private static bool MatchesMode(MonoBehaviour mode, TutorialExclusiveModeKind kind)
    {
        switch (kind)
        {
            case TutorialExclusiveModeKind.BuildMode: return mode is UI_BuildModeWindow;
            case TutorialExclusiveModeKind.WorkerMode: return mode is WorkerModeController;
            case TutorialExclusiveModeKind.Conquest: return mode is ConquestModeController;
            case TutorialExclusiveModeKind.Research: return mode is UI_ResearchWindow;
            case TutorialExclusiveModeKind.BabyDragonInventory: return mode is UI_DragonInventoryWindow;
            case TutorialExclusiveModeKind.DragonSkill: return mode is UI_DragonSkillWindow;
            default: return false;
        }
    }

    // 성은 게임 시작 시 RegisterFootprint로 같은 이벤트를 발행하므로(GridMap의 사전 배치 경로),
    // 종류를 반드시 확인해야 시작 즉시 오발화하지 않는다.
    private static bool MatchesBuilding(Building building, TutorialStepSO step)
    {
        switch (step.TargetBuilding)
        {
            case TutorialBuildingKind.AnyTower:
                return building is Tower && !(building is BabyDragonTower);

            case TutorialBuildingKind.BabyDragonTower:
                return building is BabyDragonTower;

            case TutorialBuildingKind.ResearchLab:
                return building is ResearchLab;

            case TutorialBuildingKind.Factory:
                // 종류를 지정하지 않았으면 아무 생산시설이나 통과시킨다.
                return building is Factory factory &&
                       (step.TargetFactoryData == null || factory.Data == step.TargetFactoryData);

            case TutorialBuildingKind.Castle:
                return building is Castle;

            default:
                return false;
        }
    }
}
