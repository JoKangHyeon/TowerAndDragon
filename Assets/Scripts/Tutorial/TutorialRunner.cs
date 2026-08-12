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
///
/// 러너는 한 번에 하나가 아니다 - TutorialScenarioController의 챕터와 TutorialTipChainController의
/// 팁 체인이 같은 프레임에 켜질 수 있고(3일차 아침의 보스 예고 + 정산 팁), 둘 다 같은 우선순위라
/// 나중에 켜진 쪽은 표시권을 못 얻는다. 그래서 <b>확인 클릭은 표시권을 가진 러너만 받고</b>,
/// 진 쪽은 DisplayReleased를 듣고 자기 차례에 다시 그린다(HandleConfirmClicked · HandleDisplayReleased).
/// </summary>
public sealed class TutorialRunner : MonoBehaviour, IExclusiveModeOpenQuery, IDayEndBlockQuery,
    IHudControlBlockQuery, IBuildModeInteractionQuery, IShortcutBlockQuery
{
    private const float DEFAULT_HAND_OVER_DELAY = 1.5f;
    private const float DEFAULT_STALL_ESCAPE_SECONDS = 45f;

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

    [Tooltip("알·새끼용 슬롯을 가리키는 단계와 새끼용 탭 선택을 기다리는 단계에 필요하다. " +
             "슬롯은 런타임 생성이라 앵커로 잡을 수 없다.")]
    [SerializeField] private UI_DragonWindow _dragonWindow;

    [Tooltip("건설 패널 슬롯을 가리키는 단계에 필요하다. 슬롯은 런타임 생성이라 GuideAnchor로 잡을 수 없다.")]
    [SerializeField] private UI_BuildModeWindow _buildModeWindow;

    [Header("안내 중 막을 HUD 조작")]
    [Tooltip("배타 창도 밤 시작도 아니라 기존 관문에 걸리지 않는 것들. 비우면 막지 않는다.")]
    [SerializeField] private UI_SpeedSettingWindow _speedSettingWindow;

    [SerializeField] private MinimapController _minimapController;

    [Tooltip("마지막 안내가 끝나고 다음 가이드에 넘기기까지 쉬는 시간(초). 0이면 곧바로 이어져 숨 돌릴 틈이 없다.")]
    [Min(0f)]
    [SerializeField] private float _handOverDelaySeconds = DEFAULT_HAND_OVER_DELAY;

    [Tooltip("행동형 단계가 이 시간(초) 동안 진행되지 않으면 확인 버튼을 띄워 넘어갈 수 있게 한다. " +
             "0이면 쓰지 않는다 - 배선이 잘못됐을 때 플레이어가 갇히는 것을 막는 마지막 장치다.")]
    [Min(0f)]
    [SerializeField] private float _stallEscapeSeconds = DEFAULT_STALL_ESCAPE_SECONDS;

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

    [Tooltip("도는 동안 밤 진입·창 열기·HUD를 잠글지. 1일차 강제 안내만 켠다 - " +
             "플레이어가 스스로 시작한 팁 체인은 언제든 그만둘 수 있어야 하므로 꺼 둔다.")]
    [SerializeField] private bool _holdsGates = true;

    [Tooltip("플레이어가 열어서 시작한 팁 체인에서 현재 배타 모드를 닫거나 다른 모드로 바꾸지 못하게 한다. " +
             "전체 관문과 달리 시작할 때 열려 있는 창을 닫거나 밤·HUD 조작까지 막지는 않는다.")]
    [SerializeField] private bool _holdsOpenedExclusiveMode;

    // Render를 부른 경로. 같은 단계가 두 번 그려질 때만 로그에 남는다(Render 참고).
    private const string RENDER_REASON_ENTER_STEP = "단계 진입";
    private const string RENDER_REASON_ANCHOR_REGISTERED = "앵커 등록";
    private const string RENDER_REASON_SLOT_VIEW = "슬롯 목록 갱신";
    private const string RENDER_REASON_STALL_ESCAPE = "스톨 탈출(확인 버튼 제공)";
    private const string RENDER_REASON_DISPLAY_RELEASED = "표시권 반납으로 재시도";
    private const string RENDER_REASON_TARGET_WAIT_TIMEOUT = "대상 대기 시간 초과";

    // 단계가 가리킬 대상이 나타나기를 기다리는 시간(초). 창이 열리는 데 필요한 한두 프레임만 넘기면 되고,
    // 이보다 길어지면 배선 문제로 보고 대상 없이 그린다.
    private const float TARGET_WAIT_SECONDS = 0.5f;

    // 마지막으로 그린 단계와 그 횟수. 재렌더를 세기 위한 것이라 단계가 바뀌면 1로 돌아간다.
    private TutorialStepSO _renderedStep;
    private int _renderCount;

    // 이 단계에서는 대상을 더 기다리지 않는다. 유예가 지났거나 이미 한 번 포기한 경우로,
    // 단계를 넘길 때마다 풀린다.
    private bool _hasWaivedTargetWait;

    private int _currentIndex;
    private bool _isRunning;
    private bool _hasBegun;

    // 늦게 도착한 자동 진행을 버리고 조건 판정의 기준으로 쓴다.
    private TutorialStepSO _activeStep;

    // 인구 조건은 절대값이 아니라 진입 시점부터의 증가분으로 본다.
    private int _populationBaseline;

    // 지금 단계가 오래 진행되지 않아 확인 버튼을 내준 상태. 단계를 넘길 때마다 풀린다.
    private bool _isStalled;

    // 마지막 단계를 확인 버튼으로 넘겼는지. 눌러서 "다 읽었다"고 답한 뒤에도 인계를 기다리게 하면
    // 버튼이 먹지 않은 것처럼 보이므로, 그때는 HandOverAsync의 읽을 틈을 건너뛴다.
    private bool _isConfirmedByClick;

    // 팁 체인을 시작하게 한 배타 모드. 체인이 끝나기 전까지 이 모드만 유지한다.
    private MonoBehaviour _heldExclusiveMode;

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
        if (_buildModeWindow != null)
        {
            _buildModeWindow.AddInteractionQuery(this);
        }

        if (_placementController != null)
        {
            _placementController.AddInteractionQuery(this);
        }

        // 단축키 관문만은 _holdsGates와 무관하게 언제나 건다. 아래 분기에 묶으면 팁 체인 러너가
        // (둘 다 꺼져 있어) 어느 쪽에도 걸리지 않아 관문에 등록조차 되지 않는데, 그동안 딤은
        // 마우스를 막고 있으므로 키보드로만 창을 여닫을 수 있는 상태가 된다.
        // 등록해도 창 열기·밤 진입은 건드리지 않으므로 팁 체인이 게임을 잠그지는 않는다.
        if (_uiManager != null)
        {
            _uiManager.AddShortcutQuery(this);
        }

        // 창 열림·밤 시작 판정은 Start가 아니라 여기서 건다 - Start끼리는 순서가 보장되지 않아
        // 첫 단계가 뜨기 전에 플레이어가 앞질러 갈 수 있으면 안 된다.
        //
        // 팁 체인으로 도는 러너는 이 관문을 걸지 않는다. 플레이어가 스스로 시작한 안내이고
        // 언제든 그만둘 수 있어야 하는데, 관문을 걸면 안내를 켠 대가로 게임이 잠기는 셈이 된다.
        if (_holdsGates)
        {
            if (_uiManager != null)
            {
                // 관문을 걸기 전에 화면을 정리한다. 열린 채로 안내가 시작되면 그 창은 딤에 덮여 버튼이 죽고,
                // 닫기 관문(CanClose)이 "지금 이 창을 닫아라" 단계가 아닌 한 닫는 것도 거절해 갇힌다.
                // 순서가 중요하다 - OpenQuery를 먼저 걸면 CloseAllExcept가 그 관문에 스스로 막힌다.
                _uiManager.CloseAllExcept(null);

                _uiManager.AddOpenQuery(this);
            }

            if (_cycleManager != null)
            {
                _cycleManager.AddDayEndBlocker(this);
            }

            if (_speedSettingWindow != null)
            {
                _speedSettingWindow.BlockQuery = this;
            }

            if (_minimapController != null)
            {
                _minimapController.BlockQuery = this;
            }
        }
        else if (_holdsOpenedExclusiveMode && _uiManager != null)
        {
            _heldExclusiveMode = _uiManager.CurrentOpenExclusiveMode;
            _uiManager.AddOpenQuery(this);
        }
        else if (_uiManager != null)
        {
            // 관문을 걸지 않는 팁 체인도 창 열기만은 막는다 - 안내가 화면을 덮고 있는데 새 창이 열리면
            // 그 창이 딤에 덮여 닫지도 못하고, 가리키던 대상은 창 뒤로 사라진다
            // (타워 클릭 안내 중에 Tab으로 용 창을 열어 실제로 그렇게 갇혔다).
            // 밤 진입·HUD는 그대로 열어 두므로 안내를 켠 대가로 게임이 잠기지는 않는다.
            _uiManager.AddOpenQuery(this);
        }

        if (_overlay != null)
        {
            _overlay.ConfirmClicked += HandleConfirmClicked;
            _overlay.DisplayReleased += HandleDisplayReleased;
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

    /// <summary>
    /// 안내가 도는 동안에는 지금 유도하는 것 외의 HUD 조작을 받지 않는다 - 속도를 바꾸거나
    /// 미니맵을 만지는 것이 진행을 망치지는 않지만, 무엇을 하라는 안내인지 흐려진다.
    /// 창 열기와 밤 시작은 각각 전용 관문이 따로 막는다.
    /// </summary>
    bool IHudControlBlockQuery.CanUseHudControl()
    {
        return !_isRunning;
    }

    // OnEnable이 else-if라 _holdsGates가 켜져 있으면 _heldExclusiveMode는 채워지지 않는다.
    // 두 플래그를 같이 켠 러너를 "창 하나만 붙잡는" 쪽으로 보내면 붙잡을 창이 없는 것으로
    // 오인해 관문이 통째로 풀린다.
    private bool HoldsOpenedExclusiveModeOnly => !_holdsGates && _holdsOpenedExclusiveMode;

    // 붙잡을 창이 없는 채로 도는 안내(트리거 창이 닫힌 뒤에야 시작된 큐 체인 등).
    // 이때는 열기만 풀어주면 안 된다 - 닫기까지 같이 풀지 않으면 플레이어가 연 창을
    // ESC로 닫지 못하고 X 버튼만 남는다.
    private bool HoldsNoExclusiveMode => HoldsOpenedExclusiveModeOnly && _heldExclusiveMode == null;

    bool IExclusiveModeOpenQuery.CanOpen(MonoBehaviour mode)
    {
        // mode가 null이면 "여는 것"이 아니라 CloseAllExcept(null) - 화면을 치우려는 쪽이다.
        // 막으면 뒤에 시작하는 안내(챕터)가 화면을 정리하지 못해 제 딤에 남의 창이 덮인 채로 돈다.
        if (mode == null || !_isRunning)
        {
            return true;
        }

        // 이미 열려 있던 창 안에서 도는 안내다. 그 창 하나만 붙잡고, 그 외에는 관여하지 않는다.
        if (HoldsOpenedExclusiveModeOnly)
        {
            return _heldExclusiveMode == null || ReferenceEquals(mode, _heldExclusiveMode);
        }

        // 팁 체인은 말풍선이 실제로 떠 있는 동안만 막는다. 챕터와 달리 플레이어가 스스로 켠 안내라,
        // 화면에서 걷힌 뒤(대상을 잃었거나 인계를 기다리는 중)까지 창을 잠그면 안 된다.
        if (!_holdsGates && _overlay != null && !_overlay.IsShowingFor(this))
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
    /// 키보드 단축키는 오버레이의 입력 차단을 통과하므로, 안내가 정확히 이 창을 닫으라고
    /// 요구하는 단계가 아니면 막는다. 버튼은 오버레이가 현재 유도 대상만 통과시킨다.
    /// 붙잡은 창이 없는 안내는 예외다 - CanOpen이 열기를 허용하므로 닫기도 같이 풀어야 한다.
    /// </summary>
    bool IExclusiveModeOpenQuery.CanClose(MonoBehaviour mode)
    {
        if (!_isRunning || _activeStep == null || HoldsNoExclusiveMode)
        {
            return true;
        }

        return IsStepRequestedShortcut(mode);
    }

    /// <summary>
    /// 1일차 강제 안내는 딤이 없는 설명 단계에서도 단축키를 막는다 - 순서대로 따라오게 하는 것이
    /// 목적이라 읽는 동안 창을 열어보는 것까지 막아야 한다.
    ///
    /// 팁 체인(<see cref="_holdsGates"/>가 꺼진 러너)은 여기서 막지 않는다. 플레이어가 스스로 켠
    /// 안내라 게임을 잠글 이유가 없고, 딤이 떠 있는 동안에는 오버레이가 마우스와 함께 알아서 막는다.
    /// </summary>
    bool IShortcutBlockQuery.BlocksShortcuts()
    {
        return _isRunning && _holdsGates;
    }

    /// <summary>
    /// 막힌 상태에서도 지금 단계가 시킨 키 하나는 통과시킨다. 닫기 안내에서는 ESC뿐 아니라
    /// 해당 패널의 토글 키로도 닫을 수 있다는 문구에 맞춰야 한다.
    /// </summary>
    bool IShortcutBlockQuery.AllowsShortcut(MonoBehaviour mode)
    {
        // 표시권을 가진 러너만 예외를 말할 수 있다. 러너는 둘 이상 동시에 돌 수 있는데(챕터 + 팁 체인),
        // 진 쪽은 화면에 뜨지도 않은 채 살아 있다. 그 상태의 단계가 "이 창을 닫아라"이면
        // 지금 화면을 쓰는 안내가 전 구간을 막고 있어도 그 키만 열려버린다
        // (확인 클릭을 IsDisplaying으로 거르는 것과 같은 이유 - HandleConfirmClicked 참고).
        return _isRunning &&
               (_overlay == null || _overlay.IsShowingFor(this)) &&
               IsStepRequestedShortcut(mode);
    }

    // 지금 단계가 명시적으로 "이 창을 닫아라"라고 시켰는지. 닫기 관문과 단축키 예외가 같은 판정을
    // 써야 그 규칙이 한 곳에서 관리된다 - 본문을 복제해 두면 한쪽만 고쳐져 갈라진다.
    // mode가 null인 단축키(일시정지 등)는 MatchesMode가 어떤 종류에도 걸리지 않아 자연히 거절된다.
    private bool IsStepRequestedShortcut(MonoBehaviour mode)
    {
        return _activeStep != null &&
               _activeStep.Condition == TutorialConditionType.ExclusiveModeClosed &&
               MatchesMode(mode, _activeStep.TargetMode);
    }

    bool IBuildModeInteractionQuery.CanSelectFilter(RectTransform filterTab)
    {
        if (!IsWaitingForAction)
        {
            return true;
        }

        if (_activeStep.Condition != TutorialConditionType.BuildPanelTabSelected ||
            !GuideAnchorRegistry.TryGet(_activeStep.AnchorId, out RectTransform targetTab))
        {
            return false;
        }

        return ReferenceEquals(filterTab, targetTab);
    }

    bool IBuildModeInteractionQuery.CanSelectBuilding(Building prefab) =>
        !IsWaitingForAction ||
        (_activeStep.Condition == TutorialConditionType.BuildingSelectedForPlacement &&
         MatchesBuilding(prefab, _activeStep));

    bool IBuildModeInteractionQuery.CanMoveSelectedBuilding() =>
        !IsWaitingForAction || _activeStep.Condition == TutorialConditionType.BuildingMoved;

    bool IBuildModeInteractionQuery.CanRemoveSelectedBuilding() =>
        !IsWaitingForAction || _activeStep.Condition == TutorialConditionType.BuildingRemoved;

    bool IBuildModeInteractionQuery.CanCancelPlacement() => !IsWaitingForAction;

    bool IBuildModeInteractionQuery.CanRotatePlacement() => !IsWaitingForAction;

    bool IBuildModeInteractionQuery.CanUseLongPressMove() => !IsWaitingForAction;

    private bool IsWaitingForAction =>
        _isRunning && _activeStep != null && _activeStep.Kind == TutorialStepKind.WaitForAction;

    private void OnDisable()
    {
        if (_buildModeWindow != null)
        {
            _buildModeWindow.RemoveInteractionQuery(this);
        }

        if (_placementController != null)
        {
            _placementController.RemoveInteractionQuery(this);
        }

        UnsubscribeConditions();
        _isRunning = false;
        _activeStep = null;

        // 끄면 창·밤 잠금도 같이 풀어준다 - 안 그러면 영영 막힌 채로 남는다.
        ReleaseOpenQuery();

        // 끄면 떠 있던 딤·말풍선도 같이 걷는다. 새끼용 가이드가 기다리고 있었다면 이때 표시권을 넘겨받는다.
        if (_overlay != null)
        {
            _overlay.ConfirmClicked -= HandleConfirmClicked;
            _overlay.DisplayReleased -= HandleDisplayReleased;
            _overlay.Release(this);
        }
    }

    /// <summary>
    /// 표시권이 비었다 - 양보하고 기다리던 단계를 이제 그린다.
    ///
    /// 예전 주석은 "우선순위가 높아 빼앗기지 않으니 구독하지 않는다"였는데, 그 전제는 러너가 하나일 때만
    /// 성립했다. 지금은 챕터 러너와 팁 체인 러너가 같은 우선순위로 동시에 돌 수 있고, 진 쪽은 이것을
    /// 듣지 않으면 이긴 쪽이 끝난 뒤에도 영영 그려지지 않는다(확인 클릭도 이제 걸러지므로 그대로 멈춘다).
    ///
    /// 스스로 Release한 직후 되살아나는 것은 두 가드로 막는다 - Finish는 Release 전에 _isRunning을 내리고,
    /// 이미 내가 그리고 있는 동안 온 신호는 무시한다.
    /// </summary>
    private void HandleDisplayReleased()
    {
        if (!_isRunning || _activeStep == null || _overlay == null || _overlay.IsDisplaying(this))
        {
            return;
        }

        Render(RENDER_REASON_DISPLAY_RELEASED);
    }

    // 확인 버튼은 오버레이가 공용이라 남의 단계에서도 눌릴 수 있다 - 지금 내 단계일 때만 받는다.
    // 설명형은 원래 확인 버튼으로 넘기고, 행동형은 막혀서 버튼을 내준 경우에만 받는다.
    private void HandleConfirmClicked()
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        // "내 단계인가"만 보면 부족하다 - 러너가 둘 이상 동시에 돌 수 있고(챕터 + 팁 체인),
        // 둘 다 GuidePriority.DAY_ONE_TUTORIAL이라 진 쪽은 그려지지 않은 채 살아 있다.
        // 그 상태에서 이긴 쪽의 확인 클릭을 함께 받으면 화면에 뜬 적 없는 안내가 전부 지나가 버린다.
        if (_overlay != null && !_overlay.IsDisplaying(this))
        {
            return;
        }

        bool isAcknowledged = _activeStep.Kind == TutorialStepKind.Acknowledge && _activeStep.WaitForConfirm;

        if (isAcknowledged || _isStalled)
        {
            _isConfirmedByClick = true;
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

        // 새 단계는 막히지 않은 상태에서 시작한다 - 앞 단계에서 내준 확인 버튼이 따라오면
        // 행동형 단계를 눌러서 건너뛸 수 있게 된다.
        _isStalled = false;
        _hasWaivedTargetWait = false;

        // 마지막 단계인지는 아래에서 갈리므로, 여기서 지우면 확인 버튼으로 끝낸 것을 Finish가 알 수 없다.
        if (index >= _sequence.Steps.Count)
        {
            Finish();
            return;
        }

        _isConfirmedByClick = false;

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

        // 어떤 순서로 안내가 나갔는지는 화면만 봐서는 되짚을 수 없다 - 같은 문구가 두 번 나오거나
        // 한 단계를 건너뛴 것을 이 로그로 구분한다(TutorialScenarioController의 챕터 로그와 같은 이유).
        Debug.Log($"[TutorialRunner] {name} 단계 {index}: {_activeStep.StepId} ({_activeStep.MessageLocKey})", this);

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
        Render(RENDER_REASON_ENTER_STEP);

        // 확인 버튼을 쓰는 설명은 누를 때까지 기다린다(ConfirmClicked 구독). 그 외에는 시간으로 넘긴다.
        if (_activeStep.Kind == TutorialStepKind.Acknowledge && !_activeStep.WaitForConfirm)
        {
            AutoAdvanceAsync(_activeStep).Forget();
        }
        else if (_activeStep.Kind == TutorialStepKind.WaitForAction && _stallEscapeSeconds > 0f)
        {
            WatchStallAsync(_activeStep).Forget();
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
    /// 다만 확인 버튼으로 넘긴 경우엔 기다리지 않는다 - 다 읽었다고 답한 뒤에 또 멈춰 있으면
    /// 버튼이 먹지 않은 것처럼 보이고, 뒤늦게 다음 안내가 떠 같은 문구가 다시 나온 것처럼 읽힌다.
    /// </summary>
    private async UniTaskVoid HandOverAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        if (_handOverDelaySeconds > 0f && !_isConfirmedByClick)
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
        if (_uiManager != null)
        {
            _uiManager.RemoveOpenQuery(this);
            _uiManager.RemoveShortcutQuery(this);
        }

        if (_cycleManager != null)
        {
            _cycleManager.RemoveDayEndBlocker(this);
        }

        if (_speedSettingWindow != null && ReferenceEquals(_speedSettingWindow.BlockQuery, this))
        {
            _speedSettingWindow.BlockQuery = null;
        }

        if (_minimapController != null && ReferenceEquals(_minimapController.BlockQuery, this))
        {
            _minimapController.BlockQuery = null;
        }

        _heldExclusiveMode = null;
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

    // UnityEvent(OnSlotViewChanged)가 붙일 수 있는 인자 없는 형태. 그쪽은 창이 목록을 다시 그릴 때 온다.
    private void Render() => Render(RENDER_REASON_SLOT_VIEW);

    /// <summary>이 단계가 가리킬 곳을 지정했는지. 지정하지 않은 설명은 대상 없이 그리는 것이 정상이다.</summary>
    private static bool DeclaresTarget(TutorialStepSO step) =>
        step.AnchorId != GuideAnchorId.None ||
        step.TargetBuildingSlot != null ||
        step.DynamicTarget != TutorialDynamicTargetKind.None;

    /// <summary>
    /// 대상이 나타나기를 잠깐 기다렸다가, 그래도 없으면 대상 없이라도 그린다.
    ///
    /// 기다리는 동안에는 앞 단계의 화면이 그대로 남는다 - 한두 프레임이면 눈에 띄지 않지만,
    /// 앵커 배선이 잘못돼 영영 오지 않는 경우까지 기다리면 엉뚱한 안내가 화면에 남는다.
    /// 그래서 유예를 두고, 지나면 포기하고 그린다(예전 동작). 45초 스톨 탈출은 그 뒤에 따로 작동한다.
    /// </summary>
    private async UniTaskVoid WaitForTargetThenRenderAsync(TutorialStepSO step)
    {
        await UniTask.WaitForSeconds(
            TARGET_WAIT_SECONDS,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        // 그 사이 대상이 나타나 이미 그려졌거나 단계가 넘어갔으면 할 일이 없다.
        if (!_isRunning || _activeStep != step || _hasWaivedTargetWait)
        {
            return;
        }

        Debug.LogWarning(
            $"[TutorialRunner] '{step.StepId}' 단계가 가리킬 대상을 {TARGET_WAIT_SECONDS}초 안에 찾지 못해 " +
            "문구만 띄웁니다. 앵커 배선을 확인하세요.", this);

        _hasWaivedTargetWait = true;
        Render(RENDER_REASON_TARGET_WAIT_TIMEOUT);
    }

    /// <summary>
    /// 지금 단계를 화면에 그린다.
    ///
    /// <paramref name="reason"/>은 진단용이다. 한 단계가 <b>두 번 이상 그려지는</b> 경로가 다섯 개나 되는데
    /// (단계 진입·앵커 등록·슬롯 갱신·스톨 탈출·표시권 반납) 로그에는 단계 진입만 남아서,
    /// "같은 안내가 두 번 떴다"는 제보가 들어와도 어느 경로였는지 되짚을 수 없었다.
    /// 두 번째부터만 남기므로 정상 흐름에서는 조용하다.
    /// </summary>
    private void Render(string reason)
    {
        if (_overlay == null || _activeStep == null)
        {
            return;
        }

        RectTransform target = ResolveAnchor(_activeStep);
        Renderer worldTarget = target == null ? ResolveWorldTarget(_activeStep) : null;
        bool hasTarget = target != null || worldTarget != null;

        // 가리킬 곳을 선언한 단계인데 그 대상이 아직 화면에 없으면 이번 프레임은 그리지 않는다.
        //
        // 대부분은 "창이 지금 열리는 중"이다 - 건물을 클릭한 프레임에 단계가 넘어가지만
        // 인구 패널은 다음 프레임에 열리므로 그 사이 앵커가 등록돼 있지 않다. 그 상태로 그리면
        // 대상이 없어 blocksInput이 꺼지고, 오버레이가 딤을 통째로 걷었다가(SetSpotlightActive(false))
        // 다음 프레임에 구멍 뚫린 딤을 다시 깔아 화면이 한 번 번쩍인다.
        //
        // ResolveAnchor가 이미 등록 콜백을 걸어 두었으므로 창이 열리는 순간 다시 불린다.
        // 그 콜백이 영영 오지 않는 경우(앵커 배선 실수)를 대비해 유예 시간을 두고 한 번은 그린다.
        if (!hasTarget && !_isStalled && !_hasWaivedTargetWait && DeclaresTarget(_activeStep))
        {
            WaitForTargetThenRenderAsync(_activeStep).Forget();
            return;
        }

        // 재렌더 집계는 실제로 그리는 것이 확정된 뒤에 한다 - 위에서 미룬 것까지 세면
        // 화면에 뜨지도 않은 호출이 "두 번째로 그림"으로 기록돼 진단이 어긋난다.
        if (ReferenceEquals(_renderedStep, _activeStep))
        {
            _renderCount++;
            Debug.Log(
                $"[TutorialRunner] {name} 단계 '{_activeStep.StepId}'를 {_renderCount}번째로 그립니다 (사유: {reason}). " +
                "같은 안내가 다시 뜬 것으로 보이면 이 사유가 원인입니다.", this);
        }
        else
        {
            _renderedStep = _activeStep;
            _renderCount = 1;
        }

        // 갇힌 단계에서는 행동형에도 확인 버튼을 띄운다 - 그것이 유일한 빠져나갈 길이다.
        bool showsConfirmButton = _activeStep.ShowsConfirmButton || _isStalled;

        // 행동형은 대상이 있으면 데이터 설정과 무관하게 대상 밖을 자동 차단한다. 이 규칙을 에셋마다
        // 수동으로 켜게 두면 한 단계가 빠졌을 때 다른 탭·건물을 눌러 튜토리얼 순서가 무너진다.
        bool blocksInput = (_activeStep.BlocksInput || _activeStep.Kind == TutorialStepKind.WaitForAction) &&
                           (hasTarget || showsConfirmButton);

        if (worldTarget != null)
        {
            _overlay.ShowWorldTarget(
                this,
                GuidePriority.DAY_ONE_TUTORIAL,
                worldTarget,
                _activeStep.MessageLocKey,
                blocksInput,
                _activeStep.BlocksTargetInteraction,
                showsConfirmButton,
                _activeStep.BubbleSlot,
                _activeStep.DimsBackground);
            return;
        }

        // 대상을 못 찾았을 때 입력까지 막으면 오버레이가 아무것도 그리지 않는다 - 문구만이라도 띄운다.
        _overlay.Show(
            this,
            GuidePriority.DAY_ONE_TUTORIAL,
            target,
            _activeStep.MessageLocKey,
            blocksInput,
            _activeStep.BlocksTargetInteraction,
            showsConfirmButton,
            _activeStep.BubbleSlot,
            _activeStep.DimsBackground);
    }

    private Renderer ResolveWorldTarget(TutorialStepSO step)
    {
        if (step.Condition != TutorialConditionType.BuildingSelectedOnGrid || _gridMap == null)
        {
            return null;
        }

        Building recent = _gridMap.LastAddedBuilding;
        if (recent != null && MatchesBuilding(recent, step))
        {
            return recent.GetComponentInChildren<Renderer>(true);
        }

        Building selected = _placementController == null ? null : _placementController.SelectedBuilding;
        if (selected != null && MatchesBuilding(selected, step))
        {
            return selected.GetComponentInChildren<Renderer>(true);
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building != null && MatchesBuilding(building, step))
            {
                return building.GetComponentInChildren<Renderer>(true);
            }
        }

        return null;
    }

    /// <summary>
    /// 행동형 단계가 오래 진행되지 않으면 확인 버튼을 띄워 넘어갈 길을 만든다.
    ///
    /// 자동으로 넘기지 않는 이유: 천천히 하는 플레이어의 단계를 멋대로 건너뛰면 안내가 어긋난다.
    /// 게이트를 여는 방식도 쓰지 않는다 - 아직 설명하지 않은 창이 열리거나 밤으로 넘어가면
    /// 무엇을 하라는 안내인지 알 수 없게 되고, 그건 갇히는 것보다 나쁘다.
    /// 버튼만 내주면 플레이어가 고를 수 있고, 어느 단계에서 막혔는지는 로그로 남는다.
    /// </summary>
    private async UniTaskVoid WatchStallAsync(TutorialStepSO step)
    {
        await UniTask.WaitForSeconds(
            _stallEscapeSeconds,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        // 그 사이 넘어갔으면(다른 단계이거나 끝났으면) 할 일이 없다.
        if (!_isRunning || _activeStep != step)
        {
            return;
        }

        _isStalled = true;
        Debug.LogError(
            $"[TutorialRunner] '{step.StepId}' 단계가 {_stallEscapeSeconds}초 동안 진행되지 않아 " +
            "확인 버튼으로 넘어갈 수 있게 합니다. 완료 조건 배선을 확인하세요.", this);

        Render(RENDER_REASON_STALL_ESCAPE);
    }

    // 대상을 못 찾아도 단계는 진행돼야 하므로 문구만 띄우고(null), 나중에 나타나면 그때 다시 그린다.
    private RectTransform ResolveAnchor(TutorialStepSO step)
    {
        // 알·새끼용 슬롯도 런타임 생성이라 창에서 찾아온다. 둘이 한 패널에 있으므로 단계가 어느 쪽인지 지정한다.
        if (step.DynamicTarget != TutorialDynamicTargetKind.None)
        {
            if (TryResolveDynamicTarget(step.DynamicTarget, out RectTransform slotRect))
            {
                return slotRect;
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

    private bool TryResolveDynamicTarget(TutorialDynamicTargetKind kind, out RectTransform slotRect)
    {
        slotRect = null;

        if (_dragonWindow == null)
        {
            return false;
        }

        switch (kind)
        {
            case TutorialDynamicTargetKind.DragonEggSlot:
                return _dragonWindow.TryGetFirstEggSlotRect(out slotRect);

            case TutorialDynamicTargetKind.BabyDragonSlot:
                return _dragonWindow.TryGetFirstBabyDragonSlotRect(out slotRect);

            case TutorialDynamicTargetKind.BabyDragonFocusButton:
                return _dragonWindow.TryGetFirstBabyDragonFocusButtonRect(out slotRect);

            default:
                return false;
        }
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
    /// 지금 이 조건이 충족돼 있는지. "상태"로 확인할 수 있는 것만 본다.
    ///
    /// 이벤트만 듣는 조건은 안내가 뜨기 전에 플레이어가 이미 그 행동을 해버리면 영영 감지하지 못하고
    /// 그 단계에 갇힌다. 그리고 갇히면 창 열기·밤 시작 관문이 계속 거절해 게임 전체가 잠긴다.
    /// 그래서 상태로 되돌아볼 수 있는 것은 전부 여기서 본다.
    ///
    /// <b>조건을 새로 추가하면 반드시 이 switch에 넣을 것.</b> 빠뜨리면 조용히 이벤트 전용이 되고,
    /// 그게 이 계열 버그가 반복된 이유다. 상태로 볼 수 없는 조건도 아래에 명시적으로 적어 둔다.
    /// </summary>
    private bool IsConditionAlreadySatisfied(TutorialStepSO step)
    {
        if (step.Kind != TutorialStepKind.WaitForAction)
        {
            return false;
        }

        // _uiManager는 아래 두 조건에서만 쓴다. 위에서 통째로 막으면 배선되지 않은 러너가
        // 나머지 조건의 폴백까지 전부 잃고, 이벤트가 죽어 있는 조건은 그대로 갇힌다.
        switch (step.Condition)
        {
            case TutorialConditionType.ExclusiveModeOpened:
                return _uiManager != null && MatchesMode(_uiManager.CurrentOpenExclusiveMode, step.TargetMode);

            case TutorialConditionType.ExclusiveModeClosed:
                return _uiManager != null && !MatchesMode(_uiManager.CurrentOpenExclusiveMode, step.TargetMode);

            case TutorialConditionType.BuildingSelectedForPlacement:
                return _placementController != null &&
                       MatchesBuilding(_placementController.BuildingToPlace, step);

            case TutorialConditionType.BuildingSelectedOnGrid:
                return _placementController != null &&
                       MatchesBuilding(_placementController.SelectedBuilding, step);

            case TutorialConditionType.BuildingDeselectedOnGrid:
                return _placementController != null && _placementController.SelectedBuilding == null;

            case TutorialConditionType.SelectedBuildingFullyStaffed:
                return IsSelectedBuildingFullyStaffed();

            case TutorialConditionType.ConquestChunkSelected:
                return _conquestWindow != null && _conquestWindow.HasSelectedChunk;

            case TutorialConditionType.DragonInventoryDragonTabSelected:
                return _dragonWindow != null && _dragonWindow.IsBabyTabShown;

            case TutorialConditionType.DragonWindowMotherTabSelected:
                return _dragonWindow != null && _dragonWindow.IsMotherTabShown;

            // --- 여기부터는 상태로 판정할 수 없다. 이벤트 구독으로만 넘어간다. ---
            // 진입 시점 대비 증감이거나(인구), 흔적이 남지 않는 1회성 입력이다.
            case TutorialConditionType.None:
            case TutorialConditionType.PopulationAssigned:
            case TutorialConditionType.PopulationUnassigned:
                return false;

            // 이미 같은 종류의 건물이 서 있으면 진입 즉시 통과해버린다(두 번째 타워를 짓게 하는 단계).
            case TutorialConditionType.BuildingConstructed:
            case TutorialConditionType.BuildingRemoved:
            case TutorialConditionType.BuildingMoved:
                return false;

            // 건설 패널은 열 때 기본 탭이 이미 골라져 있어, 상태로 보면 안내가 화면에 뜨지도 못하고 지나간다.
            case TutorialConditionType.BuildPanelTabSelected:
                return false;

            case TutorialConditionType.ResearchNodeCompleted:
            case TutorialConditionType.ExpeditionSent:
                return false;

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
            case TutorialConditionType.DragonWindowMotherTabSelected:
                if (_dragonWindow != null)
                {
                    _dragonWindow.OnTabDisplayed.AddListener(HandleDragonTabDisplayed);
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

        if (_dragonWindow != null)
        {
            _dragonWindow.OnSlotViewChanged.RemoveListener(Render);
            _dragonWindow.OnTabDisplayed.RemoveListener(HandleDragonTabDisplayed);
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
        if (_dragonWindow == null)
        {
            return;
        }

        _dragonWindow.OnSlotViewChanged.RemoveListener(Render);
        _dragonWindow.OnSlotViewChanged.AddListener(Render);
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
            Render(RENDER_REASON_ANCHOR_REGISTERED);
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

    // true가 새끼용 탭이다. 어느 쪽을 기다리는지는 단계의 조건이 정하므로, 반대 탭으로 간 경우는
    // 아직 시킨 것을 하지 않은 것이라 넘기지 않는다.
    private void HandleDragonTabDisplayed(bool isBabyTab)
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        // isBabyTab만 믿지 않는다 - UI_DragonWindow.OpenAtMotherTab은 창이 실제로 열리기 전에
        // 탭 이벤트를 먼저 쏘는데, 안내 중이면 그 열기가 관문에 거절될 수 있다.
        // 상태 폴백과 같은 판정을 쓰면 두 경로가 어긋나지 않는다.
        if (IsConditionAlreadySatisfied(_activeStep))
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

    // 판정 본문은 TutorialTargetMatcher에 있다 - 자유 목표(TutorialObjectiveController)가 같은 판정을
    // 써야 하는데, 복제하면 한쪽만 고쳐져 갈라진다. 여기 남은 두 함수는 호출부를 그대로 두기 위한 위임이다.
    private static bool MatchesMode(MonoBehaviour mode, TutorialExclusiveModeKind kind) =>
        TutorialTargetMatcher.MatchesMode(mode, kind);

    private static bool MatchesBuilding(Building building, TutorialStepSO step) =>
        TutorialTargetMatcher.MatchesBuilding(building, step.TargetBuilding, step.TargetFactoryData);
}
