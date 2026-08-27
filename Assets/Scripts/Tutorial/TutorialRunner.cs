using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 1일차 튜토리얼의 순서 진행과 완료 조건 감시. 표시는 UI_GuideOverlay에 맡기고,
/// 여기서는 "지금 몇 번째 단계인지"와 "그 단계가 언제 끝나는지"만 다룬다.
/// 이 컴포넌트의 활성 체크박스가 곧 튜토리얼 on/off 스위치다(새끼용 가이드와 같은 관례).
/// 도는 동안에는 아직 설명하지 않은 배타 창을 열지 못하게 막고(IExclusiveModeOpenQuery),
/// 토스트도 멈춰 둔다 - 안내와 다른 정보가 뒤섞이면 무엇을 하라는 것인지 알 수 없어진다.
///
/// 챕터는 TutorialScenarioController가 한 번에 하나만 켠다. 화면은 오버레이가 매 프레임 물어보므로
/// (<see cref="IGuideRequestProvider"/>) 이쪽에서 표시권을 잡거나 놓지 않는다 - 새끼용 알 확인처럼
/// 다른 안내에 화면을 넘기는 컷은 "이번 프레임에 낼 요청이 없다"로 자연스럽게 표현된다.
/// </summary>
public sealed class TutorialRunner : MonoBehaviour, IExclusiveModeOpenQuery, IDayEndBlockQuery,
    IDayEndBlockRelaxQuery, IHudControlBlockQuery, IBuildModeInteractionQuery, IShortcutBlockQuery,
    IGuideRequestProvider
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

    [Tooltip("어미용 속성 변경·스킬 해금을 기다리는 단계에 필요하다. 그 조건을 쓰지 않는 챕터는 비워 둔다.")]
    [WiringOptional]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    [Tooltip("스킬트리 노드를 가리키는 단계에 필요하다. 노드는 런타임 생성이라 GuideAnchor로 잡을 수 없다. " +
             "그 단계가 없는 챕터는 비워 둔다.")]
    [WiringOptional]
    [SerializeField] private UI_DragonSkillWindow _dragonSkillWindow;

    [Tooltip("점령지 선택을 기다리는 단계에 필요하다.")]
    [SerializeField] private UI_ConquestWindow _conquestWindow;

    [Tooltip("알·새끼용 슬롯을 가리키는 단계와 새끼용 탭 선택을 기다리는 단계에 필요하다. " +
             "슬롯은 런타임 생성이라 앵커로 잡을 수 없다.")]
    [SerializeField] private UI_DragonWindow _dragonWindow;

    [Tooltip("새끼용 알 확인 안내가 끝날 때까지 표시권을 넘겨주는 단계에 필요하다.")]
    [SerializeField] private BabyDragonGuideController _babyDragonGuideController;

    [Tooltip("건설 패널 슬롯을 가리키는 단계에 필요하다. 슬롯은 런타임 생성이라 GuideAnchor로 잡을 수 없다.")]
    [SerializeField] private UI_BuildModeWindow _buildModeWindow;

    [Header("안내 중 막을 HUD 조작")]
    [Tooltip("배타 창도 밤 시작도 아니라 기존 관문에 걸리지 않는 것들. 비우면 막지 않는다.")]
    [WiringOptional]
    [SerializeField] private UI_SpeedSettingWindow _speedSettingWindow;

    [SerializeField] private MinimapController _minimapController;

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

    [Tooltip("도는 동안 밤 진입·창 열기·HUD를 잠글지. 시나리오 챕터는 항상 켜 둔다.")]
    [SerializeField] private bool _holdsGates = true;

    [Tooltip("플레이어가 열어서 시작한 팁 체인에서 현재 배타 모드를 닫거나 다른 모드로 바꾸지 못하게 한다. " +
             "전체 관문과 달리 시작할 때 열려 있는 창을 닫거나 밤·HUD 조작까지 막지는 않는다.")]
    [SerializeField] private bool _holdsOpenedExclusiveMode;

    // 진행률 문구의 인자 개수(현재/목표).
    private const int PROGRESS_ARG_COUNT = 2;

    // 단계가 가리킬 대상이 나타나기를 기다리는 시간(초). 창이 열리는 데 필요한 한두 프레임만 넘기면 되고,
    // 이보다 길어지면 배선 문제로 보고 대상 없이 그린다.
    private const float TARGET_WAIT_SECONDS = 0.5f;

    // 막힌 버튼·단축키를 눌렀을 때 내는 사유 문구의 노출 규칙.
    // 연타해도 한 번만 뜨게 잠금을 두고(문구가 깜빡이면 그것대로 고장으로 보인다), 잠깐 뒤 지운다.
    // 문구 키는 새끼용 가이드와 공유하므로 Defines에 둔다.
    private const float BLOCKED_HINT_COOLDOWN_SECONDS = 1.5f;
    private const float BLOCKED_HINT_DURATION_SECONDS = 2f;

    // "타워는 충분합니다. 인구를 배치해 정원을 채우세요."
    private const string FREE_BUILD_CAP_LOC_KEY = "tutorial_free_build_cap";

    // 이번 프레임에 이 단계가 가리킬 곳. Update가 갱신하고 TryGetRequest는 읽기만 한다 -
    // 그리는 자리에서 찾으면 오버레이의 해석 패스가 앵커 구독을 걸고 UniTask를 만들게 된다.
    private RectTransform _resolvedTarget;
    private Renderer _resolvedWorldTarget;

    // 이 단계에서 목록을 맞춰 준 건설 슬롯. 같은 슬롯에 두 번 스크롤하지 않기 위한 것이다 -
    // 매 프레임 다시 맞추면 안내가 막지 않는 단계에서 플레이어가 목록을 움직일 수 없다.
    private RectTransform _scrolledSlot;

    // 이 단계가 가리킬 곳을 한 번이라도 잡았는지. "아직 안 나타났다"와 "나타났다가 사라졌다"는
    // 화면 처리가 다르다 - 앞은 앞 그림을 유지하고, 뒤는 걷는다.
    private bool _hasEverResolvedTarget;

    // 단계에 들어선 시각(정지 중에도 흘러야 하므로 unscaled). 대상 대기 유예를 재는 데 쓴다.
    private float _stepEnteredTime;

    // 대상을 못 찾았다는 경고를 이미 냈는지. 매 프레임 도는 판정이라 단계마다 한 번만 낸다.
    private bool _hasWarnedMissingTarget;

    // 진행률 문구에 넣을 인자. 개수가 바뀔 때만 새로 만든다 - 매 프레임 새로 만들면
    // 내용이 같아도 쓰레기가 계속 쌓인다.
    private object[] _progressArgs;

    private int _currentIndex;
    private bool _isRunning;
    private bool _hasBegun;

    // 늦게 도착한 자동 진행을 버리고 조건 판정의 기준으로 쓴다.
    private TutorialStepSO _activeStep;

    // 인구 조건은 절대값이 아니라 진입 시점부터의 증가분으로 본다.
    private int _populationBaseline;

    // 새끼용 모드 조건도 같은 이유로 진입 시점의 값을 기준으로 잰다. 새끼용이 아직 없으면 값이 없다.
    private BabyDragonMode? _babyDragonModeBaseline;

    // 어미용 속성 조건의 진입 시점 값. 어미용이 아직 없으면 값이 없다.
    private DragonType? _motherAttributeBaseline;

    // 이 단계에 들어선 뒤 스킬 노드를 해금했는지. 노드 해금은 흔적을 남기지만 "이번 단계에 했는가"는
    // 총량으로 알 수 없다(이미 해금된 것이 있을 수 있다) - 이벤트를 받아 여기 적는다.
    private bool _hasUnlockedDragonSkillNode;

    // 마지막으로 사유 문구를 낸 시각(정지 중에도 흘러야 하므로 unscaled). 연타 대응 잠금에 쓴다.
    private float _lastBlockedHintTime = float.NegativeInfinity;

    // 지금 단계의 월드 대상(맵 위 건물)이 배타 창에 가려 있는지. Update가 갱신하고,
    // 화면을 그릴지와 그 창의 닫기를 열어 줄지를 함께 판단하는 데 쓴다.
    private bool _isWorldTargetCovered;

    // 마지막 단계를 확인 버튼으로 넘겼는지. 눌러서 "다 읽었다"고 답한 뒤에도 인계를 기다리게 하면
    // 버튼이 먹지 않은 것처럼 보이므로, 그때는 HandOverAsync의 읽을 틈을 건너뛴다.
    private bool _isConfirmedByClick;

    // 팁 체인을 시작하게 한 배타 모드. 체인이 끝나기 전까지 이 모드만 유지한다.
    private MonoBehaviour _heldExclusiveMode;

    // 마지막 단계까지 끝내고 읽을 틈을 두는 중. 이 동안은 화면을 걷되 <b>넘기지는 않는다</b> -
    // 그냥 물러나면 그 1.5초를 새끼용 안내가 차지해, 다 끝난 챕터 뒤에 엉뚱한 말풍선이 뜬다.
    private bool _isHandingOver;

    // BeginAsync가 시작 여부를 정했는지. 정하기 전까지는 앞 챕터의 그림을 그대로 두어야 하는데
    // (새 챕터는 한 프레임 뒤에야 첫 컷을 잡는다), 시작하지 않기로 한 챕터가 그 상태로 눌러앉으면
    // 화면이 영영 앞 그림에 멈추므로 래치로 끊는다.
    private bool _hasResolvedBegin;

    // 읽을 틈을 접으라는 신호. 기다리는 쪽(HandOverAsync)이 매 프레임 보고 빠져나간다.
    private bool _skipsHandOverDelay;

    // 안내가 지나간 창만 열 수 있다. 지금 단계의 것만 허용하면 플레이어가 그 창을 닫았을 때 다시 열 수 없어 갇힌다.
    private readonly HashSet<TutorialExclusiveModeKind> _unlockedModes = new();

    /// <summary>
    /// 튜토리얼이 끝났다(완주·건너뛰기·시작조차 안 함 모두 포함). 다음 가이드는 이 신호로 시작한다 -
    /// 종료 시점 하나에 매달면 완주와 건너뛰기가 같은 경로가 되어, 스킵했을 때만 순서가 이상해지는 일이 없다.
    /// 이 컴포넌트가 새끼용 같은 개별 도메인을 알지 않게 하기 위한 통로다.
    /// </summary>
    public UnityEvent TutorialEnded = new();

    /// <summary>
    /// 현재 단계를 완료했다. 특정 진행 보상을 단계와 맞물려 지급하되 러너가 그 도메인을 직접
    /// 알지 않게 하는 연결점이다. 건너뛰기는 개별 단계를 완료한 것이 아니므로 발화하지 않는다.
    /// </summary>
    public UnityEvent<TutorialStepSO> TutorialStepCompleted = new();


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

        // 등록해 두면 오버레이가 매 프레임 물어본다. 첫 컷은 BeginAsync가 한 프레임 뒤에 잡으므로
        // 그동안은 KeepLast로 앞 챕터의 그림을 이어받는다(TryGetRequest 참고).
        if (_overlay != null)
        {
            _overlay.AddProvider(this);
        }

        RunData run = CurrentRun;
        if (!_hasBegun || (run != null && run.IsTutorialDismissed))
        {
            return;
        }

        _isHandingOver = false;
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
    /// 예외는 밤 버튼을 누르라고 시키는 단계뿐이다(<see cref="IsWaitingForNightStart"/>).
    ///
    /// <b>인계 중(<see cref="_isHandingOver"/>)에도 막는다.</b> Finish는 _isRunning을 내린 뒤
    /// 마지막 안내를 읽을 틈을 두고서야 TutorialEnded를 쏘는데, 그 몇 초 동안 이 관문도,
    /// 오버레이 관문(화면을 걷으므로 IsShowingGuide가 false)도 함께 열린다. 다음 챕터는 아직
    /// 열리지 않았으므로 챕터와 챕터 사이마다 밤으로 새어 나가는 구멍이 생긴다 -
    /// 실제로 2일차 점령 안내를 건너뛰고 밤이 시작돼 튜토리얼이 꼬였다.
    /// </summary>
    /// <remarks>
    /// <b>시작 여부를 정하기 전(<see cref="_hasResolvedBegin"/>)에도 막는다.</b> 챕터를 켜면
    /// OnEnable이 관문을 걸지만 _isRunning은 BeginAsync가 한 프레임 뒤에야 세운다. 그 한 프레임 동안
    /// 이 판정이 "돌지 않는 중"으로 읽혀 관문이 걸린 채로 열려 있었다.
    /// </remarks>
    bool IDayEndBlockQuery.CanEndDay()
    {
        return (!_isRunning && !_isHandingOver && _hasResolvedBegin) || IsWaitingForNightStart;
    }

    /// <summary>
    /// 읽을 틈을 접고 곧바로 다음 챕터에 넘긴다. 밤 버튼을 눌렀다는 것은 마지막 안내를 다 읽었다는
    /// 답이므로, 그 틈 하나 때문에 막힌 것이라면 사유를 띄우는 대신 여기서 푼다.
    /// 넘긴 자리에서 다음 챕터가 곧바로 관문을 다시 걸 수도 있고, 그때는 막히는 것이 맞다.
    /// </summary>
    bool IDayEndBlockRelaxQuery.TryRelaxDayEndBlock()
    {
        if (!_isHandingOver)
        {
            return false;
        }

        _skipsHandOverDelay = true;
        CompleteHandOver();
        return true;
    }

    /// <summary>
    /// 지금 단계가 "밤 버튼을 누르라"고 시키고 있는지. 그 단계만은 밤 관문을 스스로 푼다 -
    /// 막아 두면 시킨 대로 눌러도 아무 일이 없다. 딤은 그대로라 밤 버튼 밖은 눌리지 않으므로,
    /// 준비가 덜 된 채 잘못 눌러 밤을 맞이하는 일은 생기지 않는다.
    /// </summary>
    private bool IsWaitingForNightStart =>
        _isRunning && _activeStep != null &&
        _activeStep.Condition == TutorialConditionType.NightStarted;

    /// <summary>
    /// 안내가 도는 동안에는 지금 유도하는 것 외의 HUD 조작을 받지 않는다 - 속도를 바꾸거나
    /// 미니맵을 만지는 것이 진행을 망치지는 않지만, 무엇을 하라는 안내인지 흐려진다.
    /// 창 열기와 밤 시작은 각각 전용 관문이 따로 막는다.
    /// </summary>
    bool IHudControlBlockQuery.CanUseHudControl()
    {
        return !_isRunning || Reject();
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

        return IsUnlockedMode(mode) || Reject();
    }

    // 안내가 이미 지나간 창인지. 열기 관문과 단축키 예외가 같은 목록을 봐야 마우스와 키가 갈리지 않는다.
    private bool IsUnlockedMode(MonoBehaviour mode)
    {
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

        // 팁 체인은 말풍선이 실제로 떠 있는 동안만 막는다(CanOpen과 같은 판정). 화면에서 걷힌 뒤까지
        // 닫기를 막으면, CanOpen이 열어 준 창을 열어 놓고 X·ESC·토글 어느 것으로도 닫을 수 없게 된다.
        if (!_holdsGates && _overlay != null && !_overlay.IsShowingFor(this))
        {
            return true;
        }

        // 스스로 더 지어야 하는 단계에서는 열기를 허용한 창의 닫기도 함께 허용한다 -
        // 앞 단계가 "건설 모드 버튼을 다시 눌러 닫으세요"라고 가르쳐 놓고 여기서 그 버튼을 거절하면
        // 같은 버튼이 상황에 따라 다르게 동작하는 것으로 읽힌다.
        return IsExternalGuideMode(mode) || IsStepRequestedShortcut(mode) || IsCoveringWindow(mode) ||
               (IsFreeBuildStep && IsUnlockedMode(mode)) || Reject();
    }

    /// <summary>
    /// 지금 안내를 가려서 비워 두게 만든 창인지(ResolveStepTarget 참고). 그 창을 닫아야 안내가 다시 그려지므로
    /// 닫기와 단축키를 열어 둔다 - 막으면 화면에는 아무 안내도 없는데 창도 못 닫는 상태가 된다.
    /// </summary>
    private bool IsCoveringWindow(MonoBehaviour mode) =>
        _isWorldTargetCovered && TutorialTargetMatcher.CoversWorld(mode);

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
        // 외부 가이드에 표시권을 넘긴 동안에는 그 가이드가 쓰는 창의 토글 키도 함께 넘긴다.
        // 열기는 IsUnlockedMode로 통과해도 닫기 단축키가 여기서 막히면, 화면 문구가 Tab을
        // 안내하는데 X 버튼으로만 닫히는 상태가 된다.
        if (_isRunning && IsExternalGuideMode(mode))
        {
            return true;
        }

        // 표시권을 가진 러너만 예외를 말할 수 있다. 러너는 둘 이상 동시에 돌 수 있는데(챕터 + 팁 체인),
        // 진 쪽은 화면에 뜨지도 않은 채 살아 있다. 그 상태의 단계가 "이 창을 닫아라"이면
        // 지금 화면을 쓰는 안내가 전 구간을 막고 있어도 그 키만 열려버린다
        // (확인 클릭이 화면을 그린 러너에게만 가는 것과 같은 이유다).
        // 보류 중에는 IsShowingFor가 거짓이라 아래 조건을 못 넘는다. 그 창을 닫는 키는 따로 열어 준다.
        if (_isRunning && IsCoveringWindow(mode))
        {
            return true;
        }

        // 스스로 더 지어야 하는 단계에서는 이미 지나간 창의 단축키도 연다 - 버튼으로는 되는데
        // 키로는 안 되면 같은 조작이 경로에 따라 갈리고, 사유 문구도 상황과 어긋난다.
        if (_isRunning && IsFreeBuildStep && IsUnlockedMode(mode))
        {
            return true;
        }

        bool allows = _isRunning &&
                      (_overlay == null || _overlay.IsShowingFor(this)) &&
                      IsStepRequestedShortcut(mode);

        return allows || Reject();
    }

    /// <summary>
    /// 지금 단계가 명시적으로 "이 창을 열어라/닫아라"라고 시킨 그 창인지. 관문과 단축키 예외가 같은 판정을
    /// 써야 그 규칙이 한 곳에서 관리된다 - 본문을 복제해 두면 한쪽만 고쳐져 갈라진다.
    ///
    /// <b>열기도 함께 본다.</b> 닫기만 열어 두었더니 "용 창을 열어 슬라임 보유량을 확인하세요" 같은 컷에서
    /// <b>버튼은 되는데 토글 키가 죽었다</b> - 딤 구멍이 HUD 버튼이라 마우스는 통하고, 키보드는 딤을
    /// 통과해 이 관문으로 오는데 여기서 거절당한다. 같은 창을 여는 두 방법이 경로에 따라 갈리면
    /// 플레이어는 키가 고장 났다고 읽는다.
    ///
    /// 열기를 허용하면 그 키로 닫을 수도 있게 되지만 문제되지 않는다 - 창이 열리는 순간
    /// <see cref="TutorialConditionType.ExclusiveModeOpened"/>가 충족돼 단계가 이미 넘어가 있다.
    ///
    /// mode가 null인 단축키(일시정지 등)는 MatchesMode가 어떤 종류에도 걸리지 않아 자연히 거절된다.
    /// </summary>
    private bool IsStepRequestedShortcut(MonoBehaviour mode)
    {
        if (_activeStep == null || !MatchesMode(mode, _activeStep.TargetMode))
        {
            return false;
        }

        return _activeStep.Condition == TutorialConditionType.ExclusiveModeClosed ||
               _activeStep.Condition == TutorialConditionType.ExclusiveModeOpened;
    }

    /// <summary>
    /// 이 컷이 화면을 다른 안내에 넘겼는지. 외부 가이드가 끝나기를 기다리는 컷과
    /// <b>문구가 없는 이음매 컷</b>이 그렇다(둘 다 TryGetRequest가 요청을 내지 않는다).
    ///
    /// 넘긴 컷은 <b>관문도 함께 넘겨야 한다.</b> 화면을 쥔 쪽이 "인벤토리를 여세요"라고 시키는데
    /// 이쪽이 그 창을 막으면 버튼도 단축키도 죽은 채 안내만 남는다 -
    /// 2일차 새끼용 배치 대기 컷에서 실제로 그렇게 갇혔다.
    /// </summary>
    private bool YieldsDisplay =>
        _activeStep != null &&
        (IsWaitingForExternalGuide || string.IsNullOrWhiteSpace(_activeStep.MessageLocKey));

    // 넘겨받은 쪽이 쓰는 창인지. 어느 창인지는 그 컷의 TargetMode가 정한다 -
    // 이음매 컷은 조건에 TargetMode를 쓰지 않으므로 이 용도로 비어 있는 칸이다.
    private bool IsExternalGuideMode(MonoBehaviour mode) =>
        YieldsDisplay && MatchesMode(mode, _activeStep.TargetMode);

    bool IBuildModeInteractionQuery.CanSelectFilter(RectTransform filterTab)
    {
        if (!IsWaitingForAction || IsFreeBuildStep)
        {
            return true;
        }

        if (_activeStep.Condition != TutorialConditionType.BuildPanelTabSelected ||
            !GuideAnchorRegistry.TryGet(_activeStep.AnchorId, out RectTransform targetTab))
        {
            return Reject();
        }

        return ReferenceEquals(filterTab, targetTab) || Reject();
    }

    /// <summary>
    /// 슬롯을 눌러 배치 대상으로 고를 수 있는지.
    ///
    /// <b>자유 건설의 상한도 여기서만 막으면 된다.</b>
    /// <see cref="BuildingPlacementController.TryConstructAt"/>이 한 채를 지을 때마다 끝에서
    /// CancelBuildMode를 부르므로, 한 번 고른 고스트로 여러 채를 잇달아 놓을 수는 없다 -
    /// 짓는 횟수만큼 이 관문을 다시 지난다.
    /// </summary>
    bool IBuildModeInteractionQuery.CanSelectBuilding(Building prefab)
    {
        if (!IsWaitingForAction)
        {
            return true;
        }

        if (_activeStep.Condition == TutorialConditionType.BuildingSelectedForPlacement &&
            MatchesBuilding(prefab, _activeStep))
        {
            return true;
        }

        if (IsFreeBuildStep && MatchesBuilding(prefab, _activeStep))
        {
            if (!IsFreeBuildCapReached)
            {
                return true;
            }

            // 상한은 "안내를 따라오라"가 아니라 "이만하면 됐다"이므로 사유를 따로 낸다.
            NotifyFreeBuildCapReached();
            return false;
        }

        return Reject();
    }

    /// <summary>
    /// 개수를 채우라고 시키는 단계인지. 손잡아 가르치는 단계와 달리 플레이어가 스스로 더 지어야 하므로
    /// 건설 패널의 탭과 그 종류의 건물을 열어 준다 - 막아두면 시킨 것을 할 수 없어 관문에 갇힌다.
    /// </summary>
    private bool IsFreeBuildStep =>
        IsWaitingForAction && _activeStep.Condition == TutorialConditionType.BuildingCountReached;

    /// <summary>
    /// 자유 건설 단계에서 더 지을 수 있는 만큼 다 지었는지. 상한이 0이면 언제나 거짓이다.
    ///
    /// 상한이 필요한 이유: 이 단계는 <b>완료 조건을 채울 때까지</b> 계속 지을 수 있는데,
    /// 정원까지 채워야 끝나는 단계라면 인구를 넣지 않는 것만으로 무한히 지을 수 있다.
    /// 그렇게 땅을 다 덮으면 뒷날 안내가 시키는 건물을 놓을 자리가 없어 되돌릴 방법이 사라진다.
    /// </summary>
    private bool IsFreeBuildCapReached =>
        _activeStep != null && _activeStep.MaxCount > 0 &&
        CountMatchingBuildings(_activeStep, requiresStaffed: false) >= _activeStep.MaxCount;

    bool IBuildModeInteractionQuery.CanMoveSelectedBuilding() =>
        !IsWaitingForAction || _activeStep.Condition == TutorialConditionType.BuildingMoved || Reject();

    bool IBuildModeInteractionQuery.CanRemoveSelectedBuilding() =>
        !IsWaitingForAction || _activeStep.Condition == TutorialConditionType.BuildingRemoved || Reject();

    // 아래 셋은 사유를 내지 않는다 - BuildingPlacementController가 입력을 확인하기 전에 매 프레임 물어보므로
    // (HandleBuildCancelInput · HandleLongPressMove), 여기서 알리면 누르지도 않았는데 문구가 계속 뜬다.
    bool IBuildModeInteractionQuery.CanCancelPlacement() => !IsWaitingForAction;

    bool IBuildModeInteractionQuery.CanRotatePlacement() => !IsWaitingForAction;

    bool IBuildModeInteractionQuery.CanUseLongPressMove() => !IsWaitingForAction;

    private bool IsWaitingForAction =>
        _isRunning && _activeStep != null && _activeStep.Kind == TutorialStepKind.WaitForAction;

    /// <summary>지금 단계가 어미용 스킬 해금을 시키고 있는지. 그 단계만 관문의 예외가 된다.</summary>
    public bool IsRequestingDragonSkillUnlock =>
        IsWaitingForCondition(TutorialConditionType.DragonSkillNodeUnlocked);

    /// <summary>지금 단계가 어미용 속성 변경을 시키고 있는지. 그 단계만 관문의 예외가 된다.</summary>
    public bool IsRequestingDragonAttributeChange =>
        IsWaitingForCondition(TutorialConditionType.MotherDragonAttributeChanged);

    private bool IsWaitingForCondition(TutorialConditionType condition) =>
        _isRunning && _activeStep != null && _activeStep.Condition == condition;

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

        // 목록에서 빠지면 오버레이가 더 이상 묻지 않는다. 다음 챕터가 KeepLast로 화면을 이어받으므로
        // 이 사이에 낮은 우선순위 안내가 끼어들지 않는다.
        if (_overlay != null)
        {
            _overlay.RemoveProvider(this);
        }
    }

    // 오버레이가 지금 그린 그림의 주인에게만 보내므로 남의 클릭이 섞이지 않는다 -
    // 예전에는 이벤트가 러너 전원에게 가서, 화면에 뜬 적 없는 안내가 클릭 한 번에 함께 지나갔다.
    // 설명형은 원래 확인 버튼으로 넘기고, 행동형은 막혀서 버튼을 내준 경우에만 받는다.
    void IGuideRequestProvider.OnConfirmClicked()
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        bool isAcknowledged = _activeStep.Kind == TutorialStepKind.Acknowledge && _activeStep.WaitForConfirm;

        if (isAcknowledged)
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

        // 시작 여부를 정했다. 이 래치를 세우기 전까지는 KeepLast로 앞 챕터의 그림을 붙들고 있으므로,
        // 시작하지 않기로 한 경우에도 반드시 세워야 한다 - 아니면 화면이 앞 그림에 영영 멈춘다.
        _hasResolvedBegin = true;

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

        // 대상 판정도 단계마다 새로 시작한다. 앞 단계의 캐시가 남으면 지나간 곳을 가리킨다.
        _resolvedTarget = null;
        _resolvedWorldTarget = null;
        _scrolledSlot = null;
        _isWorldTargetCovered = false;
        _hasEverResolvedTarget = false;
        _hasWarnedMissingTarget = false;
        _progressArgs = null;
        _hasUnlockedDragonSkillNode = false;
        _stepEnteredTime = Time.unscaledTime;

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

        ResolveShortcutArgs(_activeStep);

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

        // 외부 가이드가 자기 화면과 입력을 써야 하는 단계다. 1일차 러너는 밤 진입 관문만 유지하고
        // 화면은 넘긴다(TryGetRequest가 같은 판정으로 요청을 내지 않는다).
        if (IsWaitingForExternalGuide)
        {
            return;
        }

        // 이 프레임의 LateUpdate가 새 컷을 그리려면 대상이 지금 잡혀 있어야 한다 -
        // 다음 Update까지 미루면 한 프레임 옛 컷이 화면에 남는다.
        ResolveStepTarget();

        // 확인 버튼을 쓰는 설명은 누를 때까지 기다린다(OnConfirmClicked). 그 외에는 시간으로 넘긴다.
        // 행동형은 아무것도 걸지 않는다 - 완료 조건이 올 때까지 기다리는 것이 전부다.
        if (_activeStep.Kind == TutorialStepKind.Acknowledge && !_activeStep.WaitForConfirm)
        {
            AutoAdvanceAsync(_activeStep).Forget();
        }
    }

    private void Advance()
    {
        TutorialStepSO completedStep = _activeStep;
        TutorialStepCompleted.Invoke(completedStep);
        EnterStep(_currentIndex + 1);
    }

    private bool IsWaitingForExternalGuide =>
        _activeStep != null && _activeStep.Condition == TutorialConditionType.BabyDragonEggChecked;

    private void Finish()
    {
        UnsubscribeConditions();
        _isRunning = false;
        _activeStep = null;

        // 밤 관문은 여기서 놓지 않는다 - 인계가 끝날 때까지 _isHandingOver로 계속 막아야 하는데,
        // 목록에서 먼저 빠지면 그 판정을 물어보는 쪽이 사라져 챕터 사이로 밤이 새어 나간다.
        ReleaseOpenQuery(releasesDayEndBlocker: false);

        // 끝까지 봤든 건너뛰었든 다시 뜨지 않는다. 다만 뒤에 이어질 챕터가 있으면 여기서 표시하지 않는다.
        RunData run = CurrentRun;
        if (run != null && _marksScenarioDismissedOnFinish)
        {
            run.IsTutorialDismissed = true;
        }

        // 화면은 여기서 걷지 않는다 - 확인 버튼으로 넘긴 경우엔 곧바로 다음 챕터가 이어받아야 하고,
        // 읽을 틈을 두는 경우에만 HandOverAsync가 화면을 걷는다(그동안에도 넘기지는 않는다).
        _isHandingOver = true;
        HandOverAsync().Forget();
    }

    /// <summary>
    /// 마지막 안내를 읽을 틈을 두고 다음 챕터에 넘긴다. 기다리는 동안 <see cref="_isHandingOver"/>가
    /// 화면을 걷되 <b>넘기지는 않게</b> 한다 - 그냥 물러나면 그 틈을 새끼용 안내가 차지해,
    /// 다 끝난 챕터 뒤에 엉뚱한 말풍선이 뜬다.
    ///
    /// 확인 버튼으로 넘긴 경우엔 기다리지 않는다 - 다 읽었다고 답한 뒤에 또 멈춰 있으면
    /// 버튼이 먹지 않은 것처럼 보이고, 뒤늦게 다음 안내가 떠 같은 문구가 다시 나온 것처럼 읽힌다.
    /// </summary>
    private async UniTaskVoid HandOverAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        _skipsHandOverDelay = false;

        if (_handOverDelaySeconds > 0f && !_isConfirmedByClick)
        {
            // WaitForSeconds 대신 직접 세는 이유: 기다리는 도중 플레이어가 밤 버튼을 누르면 남은 틈을
            // 접어야 하는데(TryRelaxDayEndBlock), 그러려면 매 프레임 확인할 자리가 있어야 한다.
            float remainingSeconds = _handOverDelaySeconds;

            while (remainingSeconds > 0f && !_skipsHandOverDelay)
            {
                await UniTask.Yield(token);
                remainingSeconds -= Time.unscaledDeltaTime;
            }

            // 기다리는 동안 밤 버튼을 눌러 이미 인계를 끝냈다. 여기서 또 알리면 두 번 넘어간다.
            if (_skipsHandOverDelay)
            {
                return;
            }

            // 기다리는 동안 컴포넌트가 꺼졌다. 목록에서는 이미 빠졌으므로 화면은 알아서 정리된다.
            if (!isActiveAndEnabled)
            {
                ReleaseDayEndBlocker();
                _isHandingOver = false;
                return;
            }
        }

        CompleteHandOver();
    }

    // 알리기 전에 붙잡은 것을 놓는다 - 이 신호로 열리는 다음 챕터가 KeepLast로 화면을 이어받으므로
    // 그 사이에 빈 프레임도, 낮은 우선순위 안내가 끼어들 틈도 없다.
    private void CompleteHandOver()
    {
        ReleaseDayEndBlocker();
        _isHandingOver = false;
        TutorialEnded.Invoke();
    }

    private void ReleaseDayEndBlocker()
    {
        if (_cycleManager != null)
        {
            _cycleManager.RemoveDayEndBlocker(this);
        }
    }

    // 남이 걸어둔 것을 지우지 않도록 내가 건 경우에만 뗀다.
    //
    // 밤 관문만은 남겨둘 수 있다(releasesDayEndBlocker). 인계가 끝나기 전에 목록에서 빠지면
    // _isHandingOver를 세워도 그 판정을 물어보는 쪽이 사라져 관문이 그대로 뚫린다.
    private void ReleaseOpenQuery(bool releasesDayEndBlocker = true)
    {
        if (_uiManager != null)
        {
            _uiManager.RemoveOpenQuery(this);
            _uiManager.RemoveShortcutQuery(this);
        }

        if (releasesDayEndBlocker)
        {
            ReleaseDayEndBlocker();
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

    /// <summary>이 단계가 가리킬 곳을 지정했는지. 지정하지 않은 설명은 대상 없이 그리는 것이 정상이다.</summary>
    private static bool DeclaresTarget(TutorialStepSO step) =>
        step.AnchorId != GuideAnchorId.None ||
        step.TargetBuildingSlot != null ||
        step.TargetDragonSkillNode != null ||
        step.DynamicTarget != TutorialDynamicTargetKind.None;

    // 대상이 나타나기를 기다려 준 시간이 지났는지. 대부분은 "창이 지금 열리는 중"이라 한두 프레임이면
    // 끝나지만, 앵커 배선이 잘못돼 영영 오지 않는 경우까지 기다리면 안내가 그 자리에 멈춘다.
    private bool IsTargetWaitExpired => Time.unscaledTime - _stepEnteredTime >= TARGET_WAIT_SECONDS;

    // 지금 확인 버튼을 내주는지. 갇힌 단계에서는 행동형에도 띄운다 - 그것이 유일한 빠져나갈 길이다.
    private bool ShowsConfirmButtonNow => _activeStep != null && _activeStep.ShowsConfirmButton;

    /// <summary>
    /// 이번 프레임에 이 단계가 가리킬 곳과 진행률을 잡아 둔다. <b>Update에서만 부른다</b> -
    /// 앵커 조회는 창이 열리는 순간을 폴링으로 잡는 것이 전부라 부작용이 없지만, 그리는 자리에서
    /// 부르면 오버레이의 해석 패스가 한 프레임에 여러 번 도는 만큼 반복된다.
    /// </summary>
    private void ResolveStepTarget()
    {
        if (_activeStep == null)
        {
            return;
        }

        _resolvedTarget = ResolveAnchor(_activeStep);
        ScrollTargetSlotIntoView();

        // 월드 대상은 건물 순회 + GetComponentInChildren이라 한 번 잡으면 단계가 바뀔 때까지 들고 있는다.
        if (_resolvedTarget != null)
        {
            _resolvedWorldTarget = null;
        }
        else if (_resolvedWorldTarget == null)
        {
            _resolvedWorldTarget = ResolveWorldTarget(_activeStep);
        }

        bool hasTarget = _resolvedTarget != null || _resolvedWorldTarget != null;
        if (hasTarget)
        {
            _hasEverResolvedTarget = true;
        }

        // 월드 대상이 맵을 덮는 창 뒤에 있으면 그리지 않는다 - 그리면 딤이 창 위에 깔려
        // 대상 클릭도 창 닫기도 막힌다(성을 가리키는 단계에서 용 창이 열려 있으면 실제로 그랬다).
        _isWorldTargetCovered = _resolvedWorldTarget != null && IsWorldCoveredByWindow;

        UpdateProgressArgs(_activeStep);

        if (!hasTarget && !_hasEverResolvedTarget && !_hasWarnedMissingTarget &&
            DeclaresTarget(_activeStep) && IsTargetWaitExpired)
        {
            _hasWarnedMissingTarget = true;
            Debug.LogWarning(
                $"[TutorialRunner] '{_activeStep.StepId}' 단계가 가리킬 대상을 {TARGET_WAIT_SECONDS}초 안에 찾지 못해 " +
                "문구만 띄웁니다. 앵커 배선을 확인하세요.", this);
        }
    }

    /// <summary>
    /// 건설 목록에서 가리킨 슬롯이 화면 밖에 있으면 그 자리로 목록을 내려 준다.
    /// 미리 목록을 내려 둔 채로 이 단계에 들어오면 가리킨 슬롯이 화면 밖이라 딤 구멍도 없어
    /// 무엇을 누르라는 것인지 알 수 없었다.
    ///
    /// 슬롯이 새로 잡혔을 때만 한 번 맞춘다. 탭을 바꾸면 슬롯이 통째로 다시 만들어지므로
    /// 그때는 다른 인스턴스가 되어 자동으로 다시 맞춰진다.
    /// </summary>
    private void ScrollTargetSlotIntoView()
    {
        if (_buildModeWindow == null || _resolvedTarget == null ||
            _activeStep.TargetBuildingSlot == null ||
            ReferenceEquals(_resolvedTarget, _scrolledSlot))
        {
            return;
        }

        _scrolledSlot = _resolvedTarget;
        _buildModeWindow.ScrollSlotIntoView(_activeStep.TargetBuildingSlot);
    }

    /// <summary>
    /// 문구에 넣을 포맷 인자. 지금은 개수 채우기 단계의 진행률(현재/목표)뿐이고, 나머지는 인자가 없다.
    /// 개수가 바뀔 때만 새로 만든다 - 매 프레임 새 배열을 만들면 내용이 같아도 쓰레기가 계속 쌓이고,
    /// 오버레이는 내용으로 비교하므로 새로 만들 이유도 없다.
    /// </summary>
    /// <summary>
    /// 창을 열라고 시키는 컷이면 그 창의 토글 키 표기를 문구 인자로 채운다.
    ///
    /// <b>여는 컷에만 붙인다.</b> 닫기 컷은 이미 ESC를 가르치고 있어 한 문장에 조작이 셋이 되고,
    /// 열기에서 배운 토글 키는 닫을 때도 그대로 쓰이므로 같은 말을 두 번 하게 된다.
    ///
    /// 이 컷들에서는 <see cref="IsStepRequestedShortcut"/>이 그 키를 실제로 통과시킨다 -
    /// 문구에 키를 적어 두고 정작 막으면 안내가 거짓말이 된다(그래서 조건을 둘이 같이 본다).
    ///
    /// 단계 진입 때 한 번만 부른다. 키 표기는 그 단계가 도는 동안 바뀌지 않는데,
    /// 매 프레임 다시 만들면 판정식과 배열이 프레임마다 새로 할당된다.
    /// </summary>
    private void ResolveShortcutArgs(TutorialStepSO step)
    {
        if (_uiManager == null || step.Condition != TutorialConditionType.ExclusiveModeOpened)
        {
            return;
        }

        if (_uiManager.TryGetShortcutLabel(mode => MatchesMode(mode, step.TargetMode), out string label))
        {
            _progressArgs = new object[] { label };
        }
    }

    private void UpdateProgressArgs(TutorialStepSO step)
    {
        if (step.Kind != TutorialStepKind.WaitForAction ||
            step.Condition != TutorialConditionType.BuildingCountReached)
        {
            // 진행률을 쓰지 않는 컷이라고 인자를 지우면 안 된다 - 단축키 표기가 여기 담겨 있고,
            // 그것은 EnterStep이 이미 정했다(ResolveShortcutArgs).
            return;
        }

        int count = CountMatchingBuildings(step);
        if (_progressArgs != null && _progressArgs.Length == PROGRESS_ARG_COUNT &&
            _progressArgs[0] is int rendered && rendered == count)
        {
            return;
        }

        _progressArgs = new object[] { count, step.RequiredCount };
    }

    /// <summary>
    /// 지금 낼 안내. <b>읽기만 한다</b> - 상태 전이는 전부 <see cref="Update"/>와 조건 이벤트가 맡는다.
    ///
    /// false는 "화면을 넘긴다"는 뜻이라 우선순위가 낮은 안내(새끼용 가이드)가 그릴 수 있다.
    /// 넘기지 않고 비워두려면 <see cref="GuideRequest.Hidden"/>을 돌려준다.
    /// </summary>
    bool IGuideRequestProvider.TryGetRequest(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        // BeginAsync가 아직 시작 여부를 정하지 못했다. 새 챕터는 한 프레임 뒤에야 첫 컷을 잡으므로
        // 그동안 앞 챕터의 그림을 그대로 두어야 인계하는 프레임이 비지 않는다.
        if (!_hasResolvedBegin)
        {
            request = GuideRequest.KeepLast;
            return true;
        }

        // 마지막 컷을 끝내고 읽을 틈을 두는 중. 화면은 비우되 넘기지는 않는다.
        if (_isHandingOver)
        {
            return true;
        }

        if (!_isRunning || _activeStep == null)
        {
            return false;
        }

        // 외부 가이드(새끼용 알 확인)가 자기 화면과 입력을 써야 하는 단계다. 조건만 기다리며 화면을 넘긴다.
        if (IsWaitingForExternalGuide)
        {
            return false;
        }

        // 문구가 없는 '이음매' 컷도 화면을 잡지 않는다 - 여기서 말풍선을 띄우면 우선순위가 높은 챕터가
        // 화면을 쥐어, 정작 플레이어가 따라야 할 낮은 우선순위 안내가 뜨지 못한다.
        if (string.IsNullOrWhiteSpace(_activeStep.MessageLocKey))
        {
            return false;
        }

        // 월드 대상이 창에 가려 있다. 넘기지 않고 비워둔다 - 넘기면 그 틈에 다른 안내가 끼어들고,
        // 그 창을 닫으면 곧바로 이 컷이 돌아와야 한다(CanClose가 그 창의 닫기를 열어 둔다).
        if (_isWorldTargetCovered)
        {
            return true;
        }

        // 여기부터는 이 컷이 화면을 쥔다. 그릴지 앞 그림을 둘지 걷을지만 남았다.
        ResolveDrawPhase(out request);
        return true;
    }

    /// <summary>
    /// 가리킬 곳이 아직 없을 때 화면을 어떻게 할지. 셋을 구분해야 한다.
    /// <list type="bullet">
    /// <item>한 번도 못 잡았고 유예 안 - 창이 지금 열리는 중이다. 앞 그림을 유지한다
    /// (여기서 대상 없이 그리면 딤이 통째로 걷혔다가 다음 프레임에 다시 깔려 화면이 번쩍인다).</item>
    /// <item>한 번도 못 잡았고 유예 초과 - 배선 문제로 보고 대상 없이 문구만 띄운다.</item>
    /// <item>잡혔다가 사라졌고 확인 버튼도 없다 - 화면을 걷는다. 안 그러면 딤만 남고 빠져나갈 길이 없다.
    /// 그 행동 자체가 대상을 되살리므로 돌아오면 다음 프레임에 다시 그린다.</item>
    /// </list>
    /// </summary>
    private void ResolveDrawPhase(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        bool hasTarget = _resolvedTarget != null || _resolvedWorldTarget != null;

        if (!hasTarget && DeclaresTarget(_activeStep))
        {
            if (!_hasEverResolvedTarget)
            {
                if (!IsTargetWaitExpired)
                {
                    request = GuideRequest.KeepLast;
                    return;
                }
            }
            else if (!ShowsConfirmButtonNow)
            {
                return;
            }
        }

        // 딤과 입력 차단은 넘기지 않는다 - 오버레이가 대상·확인 버튼 유무로 스스로 정한다.
        // 여기서 단계별로 판단하게 두었더니 한 단계가 빠졌을 때 다른 탭·건물을 눌러 순서가 무너졌다.
        if (_resolvedWorldTarget != null)
        {
            request = GuideRequest.DrawWorld(
                _resolvedWorldTarget,
                _activeStep.MessageLocKey,
                _activeStep.BlocksTargetInteraction,
                _activeStep.KeepsInputOpen,
                ShowsConfirmButtonNow,
                _activeStep.BubbleSlot,
                _progressArgs,
                IsWaitingForNightStart);
            return;
        }

        request = GuideRequest.Draw(
            _resolvedTarget,
            _activeStep.MessageLocKey,
            _activeStep.BlocksTargetInteraction,
            _activeStep.KeepsInputOpen,
            ShowsConfirmButtonNow,
            _activeStep.BubbleSlot,
            _progressArgs,
            IsWaitingForNightStart);
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
    /// 이 단계가 가리킬 UI를 찾는다. 못 찾으면 null이고, 그때 무엇을 할지는 호출부가 정한다.
    ///
    /// 예전에는 못 찾을 때마다 "나타나면 알려달라"는 구독을 걸었는데(앵커 등록·슬롯 목록 갱신),
    /// 매 프레임 다시 물어보는 지금은 필요 없다 - 창이 열리는 프레임의 폴링이 알아서 잡는다.
    /// </summary>
    private RectTransform ResolveAnchor(TutorialStepSO step)
    {
        // 알·새끼용 슬롯도 런타임 생성이라 창에서 찾아온다. 둘이 한 패널에 있으므로 단계가 어느 쪽인지 지정한다.
        if (step.DynamicTarget != TutorialDynamicTargetKind.None)
        {
            return TryResolveDynamicTarget(step.DynamicTarget, out RectTransform slotRect) ? slotRect : null;
        }

        // 스킬트리 노드도 런타임 생성이라 창에서 찾아온다. 창을 처음 열 때 트리가 만들어지므로
        // 그전에는 없고, 그때는 대상 대기 유예가 알아서 기다린다.
        if (step.TargetDragonSkillNode != null)
        {
            return _dragonSkillWindow != null &&
                   _dragonSkillWindow.TryGetNodeRect(step.TargetDragonSkillNode, out RectTransform nodeRect)
                ? nodeRect
                : null;
        }

        // 건설 패널 슬롯은 런타임 생성이라 앵커가 아니라 창에서 찾아온다. 지정돼 있으면 이쪽이 우선.
        if (step.TargetBuildingSlot != null)
        {
            return _buildModeWindow != null &&
                   _buildModeWindow.TryGetSlotRect(step.TargetBuildingSlot, out RectTransform slotRect)
                ? slotRect
                : null;
        }

        if (step.AnchorId == GuideAnchorId.None)
        {
            return null;
        }

        return GuideAnchorRegistry.TryGet(step.AnchorId, out RectTransform anchor) ? anchor : null;
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

        // 속성 변경도 진입 시점 대비로 본다 - 절대값(생명인가)으로 판정하면 이미 생명인 상태에서
        // 진입 즉시 통과하고, TryChangeType은 같은 속성이면 실패하므로 시킬 수도 없다.
        if (step.Condition == TutorialConditionType.MotherDragonAttributeChanged)
        {
            _motherAttributeBaseline = _dragonTreeManager == null ? null : _dragonTreeManager.ActiveAttribute;
            return;
        }

        if (step.Condition != TutorialConditionType.BabyDragonModeChanged)
        {
            return;
        }

        _babyDragonModeBaseline = TryGetBabyDragonMode(out BabyDragonMode mode) ? mode : null;

        // 기준값이 없으면 이 단계는 영영 통과하지 못한다(45초 뒤 확인 버튼이 유일한 출구다).
        // 앞 컷이 배치를 기다리므로 정상 흐름에서는 일어나지 않는다 - 순서가 바뀌었다는 신호다.
        if (!_babyDragonModeBaseline.HasValue)
        {
            Debug.LogWarning(
                $"[TutorialRunner] '{step.StepId}' 단계에 들어섰는데 그리드에 새끼용이 없습니다 - " +
                "모드 변경을 기다릴 대상이 없어 이 단계는 확인 버튼으로만 넘어갑니다.", this);
        }
    }

    /// <summary>
    /// 어미용 속성 조건이 충족됐는지. 단계가 속성을 지정했으면 <b>그 속성으로 바뀌었는가</b>를 보고,
    /// 지정하지 않았으면 진입 시점과 다르기만 하면 된다.
    ///
    /// 지정한 경우에도 기준값을 함께 보는 이유: 진입 시점에 이미 그 속성이면 즉시 통과해 안내가
    /// 화면에 뜨지도 못한다. 그때는 시킬 것이 없으므로 통과시키는 것이 맞다 -
    /// 그 상황은 <c>OnValidate</c>가 아니라 씬의 시작 속성이 만든다(M0-3에서 얼음으로 고정한 이유).
    /// </summary>
    private bool IsMotherAttributeSatisfied(TutorialStepSO step)
    {
        if (_dragonTreeManager == null || !_dragonTreeManager.ActiveAttribute.HasValue)
        {
            return false;
        }

        DragonType current = _dragonTreeManager.ActiveAttribute.Value;
        DragonType? required = step.RequiredDragonType;

        if (required.HasValue)
        {
            return current == required.Value;
        }

        return _motherAttributeBaseline.HasValue && current != _motherAttributeBaseline.Value;
    }

    /// <summary>
    /// 그리드에 있는 첫 새끼용의 운용 모드. 튜토리얼은 새끼용이 한 마리뿐이라 "첫 마리"로 충분하다 -
    /// 여러 마리를 다루게 되면 어느 마리인지 단계가 지정해야 한다.
    /// </summary>
    private bool TryGetBabyDragonMode(out BabyDragonMode mode)
    {
        mode = default;

        if (_gridMap == null)
        {
            return false;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is BabyDragonTower babyDragon)
            {
                mode = babyDragon.Mode;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 창 열기/닫기는 이벤트가 아니라 상태를 매 프레임 확인한다.
    /// 이벤트는 "그 순간"에만 오므로, 안내보다 먼저 한 행동이나 발행 경로가 하나라도 어긋나는 창에서는
    /// 신호를 놓쳐 그 단계에 갇힌다. 상태로 보면 어떤 경로로 여닫았든 결과가 같다.
    /// 나머지 조건(인구 증감·건설 등)은 상태로 되돌아볼 수 없어 이벤트를 그대로 쓴다.
    /// </summary>
    private void Update()
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        if (IsConditionAlreadySatisfied(_activeStep))
        {
            // EnterStep이 새 단계의 대상을 곧바로 잡으므로 여기서 더 할 일이 없다.
            Advance();
            return;
        }


        // 대상 해석은 조건 판정 뒤에 한다 - 앞서 하면 방금 넘어간 단계의 대상을 캐시해
        // 그 프레임의 LateUpdate가 한 컷 옛 안내를 그린다.
        ResolveStepTarget();
    }

    /// <summary>
    /// 막았다는 사실을 그 자리에서 알리고 거절한다. 관문이 거절하는 지점은 전부 플레이어가 무언가를
    /// 누른 순간이라(폴링이 아니다) 여기서 사유를 낼 수 있다 - 무반응으로 두면 버그로 읽힌다.
    ///
    /// 항상 false를 돌려주므로 <c>return Reject();</c> 꼴로 거절 지점에 그대로 끼워 넣는다.
    /// 매 프레임 물어보는 질의(배치 취소·롱프레스)에는 쓰지 않는다 - 누르지도 않았는데 문구가 뜬다.
    /// </summary>
    private bool Reject()
    {
        NotifyBlocked();
        return false;
    }

    /// <summary>
    /// 막았다는 사유를 말풍선 아래에 낸다. 딤에 삼켜진 클릭(<see cref="IGuideRequestProvider.OnBlockedClicked"/>)처럼
    /// 거절값을 돌려줄 곳이 없는 경로도 이것을 직접 부른다.
    /// </summary>
    private void NotifyBlocked()
    {
        if (!_isRunning || _overlay == null)
        {
            return;
        }

        // 화면을 쓰고 있는 러너만 말한다. 사유 줄은 말풍선 안에 있어, 표시권이 없으면 띄워도 보이지 않는다.
        if (!_overlay.IsShowingFor(this))
        {
            // 막기는 했는데 알릴 화면이 없는 경우다. 플레이어에게는 여전히 무반응으로 보이므로,
            // 어느 단계에서 그러는지 콘솔에 남겨 둔다(연타로 도배되지 않게 잠금 시간을 함께 쓴다).
            if (Time.unscaledTime - _lastBlockedHintTime >= BLOCKED_HINT_COOLDOWN_SECONDS)
            {
                _lastBlockedHintTime = Time.unscaledTime;
                Debug.Log(
                    $"[TutorialRunner] {name}이 조작을 막았지만 사유를 낼 화면이 없습니다 " +
                    $"(단계 '{(_activeStep == null ? "없음" : _activeStep.StepId)}'). " +
                    "이 러너가 표시권을 갖고 있지 않습니다.", this);
            }

            return;
        }

        if (Time.unscaledTime - _lastBlockedHintTime < BLOCKED_HINT_COOLDOWN_SECONDS)
        {
            return;
        }

        _lastBlockedHintTime = Time.unscaledTime;
        _overlay.ShowHint(this, Defines.TUTORIAL_BLOCKED_HINT_LOC_KEY, BLOCKED_HINT_DURATION_SECONDS);
    }

    // 상한에 닿았다는 말은 따로 한다 - 기본 차단 문구("안내를 먼저 따라와 주세요")는 여기서 거짓말이 된다.
    // 플레이어는 안내를 따르는 중이고, 남은 일은 이미 지은 타워에 인구를 넣는 것이다.

    private void NotifyFreeBuildCapReached()
    {
        if (!_isRunning || _overlay == null ||
            Time.unscaledTime - _lastBlockedHintTime < BLOCKED_HINT_COOLDOWN_SECONDS)
        {
            return;
        }

        _lastBlockedHintTime = Time.unscaledTime;
        _overlay.ShowHint(this, FREE_BUILD_CAP_LOC_KEY, BLOCKED_HINT_DURATION_SECONDS);
    }

    void IGuideRequestProvider.OnBlockedClicked() => NotifyBlocked();

    int IGuideRequestProvider.Priority => GuidePriority.DAY_ONE_TUTORIAL;

    // 일꾼 모드가 열려 있는지. 우클릭 조작은 그 모드 안에서만 뜻이 있으므로 함께 확인한다 -
    // 모드를 보지 않으면 안내와 무관한 곳에서 누른 우클릭(배치 취소 등)으로도 넘어간다.
    private bool IsWorkerModeOpen =>
        _uiManager != null &&
        MatchesMode(_uiManager.CurrentOpenExclusiveMode, TutorialExclusiveModeKind.WorkerMode);

    /// <summary>맵을 덮는 창이 열려 있는지. 건설·일꾼·점령처럼 맵 위에서 조작하는 모드는 가리지 않는다.</summary>
    private bool IsWorldCoveredByWindow =>
        _uiManager != null && TutorialTargetMatcher.CoversWorld(_uiManager.CurrentOpenExclusiveMode);

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

        // 이 단계가 유도하는 창에 이미 들어가 있으면 통로를 다시 지날 수 없다.
        // 성을 클릭하게 하는 단계가 그렇다 - 용 창이 열리는 순간 성 선택이 풀려 조건은 영영 거짓인데,
        // 딤은 창 뒤의 성을 계속 가리켜 클릭도 창 닫기도 막힌다. 목적지에 도착했으면 통로는 건너뛴다.
        if (step.SkipIfModeOpen != TutorialExclusiveModeKind.None && _uiManager != null &&
            MatchesMode(_uiManager.CurrentOpenExclusiveMode, step.SkipIfModeOpen))
        {
            return true;
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

            // 총량으로 세는 조건이라 상태만 보면 된다 - 건물을 짓든 인구를 넣든 결과가 여기 드러난다.
            case TutorialConditionType.BuildingCountReached:
                return CountMatchingBuildings(step) >= step.RequiredCount;

            // 그 프레임의 입력을 본다. Update가 매 프레임 이 판정을 돌리므로 누른 프레임에 잡힌다 -
            // 결과가 남지 않는 조작이라 상태로는 확인할 방법이 없다.
            case TutorialConditionType.WorkerModeRightClicked:
                return IsWorkerModeOpen && Mouse.current != null &&
                       Mouse.current.rightButton.wasPressedThisFrame;

            case TutorialConditionType.ConquestChunkSelected:
                return _conquestWindow != null && _conquestWindow.HasSelectedChunk;

            case TutorialConditionType.DragonInventoryDragonTabSelected:
                return _dragonWindow != null && _dragonWindow.IsBabyTabShown;

            case TutorialConditionType.DragonWindowMotherTabSelected:
                return _dragonWindow != null && _dragonWindow.IsMotherTabShown;

            case TutorialConditionType.BabyDragonEggChecked:
                return _babyDragonGuideController != null &&
                       _babyDragonGuideController.HasFinishedEggCheckGuide;

            // 진입 시점과 다른 모드가 됐는지. 기준값이 없으면(새끼용이 없었다) 판정할 것이 없다.
            case TutorialConditionType.BabyDragonModeChanged:
                return _babyDragonModeBaseline.HasValue &&
                       TryGetBabyDragonMode(out BabyDragonMode currentMode) &&
                       currentMode != _babyDragonModeBaseline.Value;

            // 창을 열어 본 것이 아니라 실제로 바꾼 것을 요구한다.
            // 단계가 속성을 지정했으면 그 속성이어야 하고, 아니면 진입 시점과 다르기만 하면 된다.
            case TutorialConditionType.MotherDragonAttributeChanged:
                return IsMotherAttributeSatisfied(step);

            // 이벤트로만 알 수 있다 - 이미 해금된 노드가 있을 수 있어 총량으로는 "이번에 했는가"를 못 가른다.
            case TutorialConditionType.DragonSkillNodeUnlocked:
                return _hasUnlockedDragonSkillNode;

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

            case TutorialConditionType.DragonSkillNodeUnlocked:
                if (_dragonTreeManager != null)
                {
                    _dragonTreeManager.NodeUnlocked.AddListener(HandleDragonSkillNodeUnlocked);
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

            case TutorialConditionType.BabyDragonEggChecked:
                if (_babyDragonGuideController != null)
                {
                    _babyDragonGuideController.EggCheckGuideFinished.AddListener(HandleEggCheckGuideFinished);
                }
                break;

            case TutorialConditionType.NightStarted:
                if (_cycleManager != null)
                {
                    _cycleManager.OnNightStart.AddListener(HandleNightStarted);
                }
                break;

            // 아래 셋은 상태·입력으로만 판정한다 - 건물 개수는 총량에 드러나고, 우클릭은 그 프레임의
            // 입력이며, 새끼용 모드는 인스턴스 이벤트라 구독할 대상이 런타임에야 생긴다.
            // Update의 매 프레임 확인으로 충분하고, 대신 IsConditionAlreadySatisfied에 반드시 들어 있어야 한다.
            case TutorialConditionType.BuildingCountReached:
            case TutorialConditionType.WorkerModeRightClicked:
            case TutorialConditionType.BabyDragonModeChanged:
            case TutorialConditionType.MotherDragonAttributeChanged:
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

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleDragonSkillNodeUnlocked);
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
            _buildModeWindow.OnTabSelected.RemoveListener(HandleTabSelected);
        }

        if (_dragonWindow != null)
        {
            _dragonWindow.OnTabDisplayed.RemoveListener(HandleDragonTabDisplayed);
        }

        if (_babyDragonGuideController != null)
        {
            _babyDragonGuideController.EggCheckGuideFinished.RemoveListener(HandleEggCheckGuideFinished);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStarted);
        }
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

    // 밤이 시작됐다 = 플레이어가 밤 버튼을 눌렀다. 이 단계가 시킨 일이 끝났으므로 넘어간다.
    private void HandleNightStarted(int _)
    {
        if (_isRunning && _activeStep != null)
        {
            Advance();
        }
    }

    // 어느 스킬 노드인지도 묻지 않는다. 플래그를 먼저 세워 두어야 Update의 상태 판정과 어긋나지 않는다.
    private void HandleDragonSkillNodeUnlocked(ProgressionNodeData _)
    {
        if (!_isRunning || _activeStep == null)
        {
            return;
        }

        _hasUnlockedDragonSkillNode = true;

        if (_activeStep.Condition == TutorialConditionType.DragonSkillNodeUnlocked)
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

    private void HandleEggCheckGuideFinished()
    {
        if (_isRunning && IsWaitingForExternalGuide)
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
    /// <summary>
    /// 지금 서 있는 건물 중 이 단계가 세라고 한 종류가 몇 개인지. 정원 옵션이 켜져 있으면
    /// 인구를 다 채운 것만 센다 - 타워는 충원율이 곧 화력이라 개수만 채운 것은 기준이 되지 않는다.
    /// </summary>
    private int CountMatchingBuildings(TutorialStepSO step) =>
        CountMatchingBuildings(step, step.RequiresStaffed);

    /// <summary>
    /// <paramref name="requiresStaffed"/>를 완료 판정과 따로 받는 이유: 상한(<see cref="MaxCount"/>)은
    /// <b>땅을 얼마나 덮었는가</b>를 재는 값이라 정원과 무관하다. 완료 판정과 같은 셈법을 쓰면,
    /// 인구를 한 명도 넣지 않은 플레이어에게는 개수가 영영 0이라 상한이 걸리지 않는다
    /// - 실제로 그 구멍으로 타워를 무한히 지을 수 있었다.
    /// </summary>
    private int CountMatchingBuildings(TutorialStepSO step, bool requiresStaffed)
    {
        if (_gridMap == null)
        {
            return 0;
        }

        int count = 0;

        foreach (Building building in _gridMap.Buildings)
        {
            if (building == null || !MatchesBuilding(building, step))
            {
                continue;
            }

            if (requiresStaffed && !IsFullyStaffed(building))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool IsFullyStaffed(Building building)
    {
        var target = building.GetComponent<IPopulationAllocationTarget>();

        // 정원이 0이면 "다 찼다"가 성립하지 않는다 - 초기화 전이거나 인구를 받지 않는 건물이다.
        return target != null && target.IsInitialized && target.Capacity > 0 &&
               target.AssignedPopulation >= target.Capacity;
    }

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
