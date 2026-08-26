using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 새끼용 가이드(획득 → 인벤토리 → 부화 → 배치)의 단계 판정. 표시는 UI_GuideOverlay가 맡고
/// 여기서는 "지금 어느 단계인지"와 "어디를 가리킬지"만 정한다.
/// 진행도는 RunData.EnteredGuideSteps에 남겨 같은 런에서 같은 안내가 두 번 뜨지 않게 한다.
/// 획득 알림(ProgressionNotificationPresenter)은 이 컨트롤러와 무관하게 매번 뜬다 - 안내는 1회성,
/// 알림은 상시라는 구분이다.
/// </summary>
public class BabyDragonGuideController : MonoBehaviour, IDayEndBlockQuery, IShortcutBlockQuery,
    IExclusiveModeOpenQuery, IGuideRequestProvider
{
    private const string OPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_open_inventory";
    private const string REOPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_reopen_inventory";
    private const string SWITCH_TAB_LOC_KEY = "baby_dragon_guide_switch_tab";
    private const string WAIT_HATCH_LOC_KEY = "baby_dragon_guide_wait_hatch";
    private const string CLOSE_INVENTORY_LOC_KEY = "baby_dragon_guide_close_inventory";
    private const string PLACE_DRAGON_LOC_KEY = "baby_dragon_guide_place_dragon";
    private const string PLACE_DRAGON_TILE_LOC_KEY = "baby_dragon_guide_place_dragon_tile";
    private const string COMPLETED_LOC_KEY = "baby_dragon_guide_completed";
    private const string MANAGE_HINT_LOC_KEY = "baby_dragon_guide_manage_hint";
    private const string MODE_HINT_LOC_KEY = "baby_dragon_guide_mode_hint";

    // 막힌 조작을 알리는 사유 줄의 노출 규칙. 연타해도 한 번만 뜨고 잠깐 뒤 사라진다.
    // 문구 키는 튜토리얼 러너와 공유하므로 Defines에 있다.
    private const float BLOCKED_HINT_COOLDOWN_SECONDS = 1.5f;
    private const float BLOCKED_HINT_DURATION_SECONDS = 2f;

    // 배치가 끝난 뒤 순서대로 보여줄 마무리 문구. 확인 버튼으로 한 컷씩 넘긴다.
    private static readonly string[] COMPLETION_LOC_KEYS = { COMPLETED_LOC_KEY, MANAGE_HINT_LOC_KEY };

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("부화 알림을 실제로 확인한 뒤 배치 안내를 시작하는 데 쓴다. 비우면 장면에서 찾는다.")]
    [WiringOptional]
    [SerializeField] private ProgressionNotificationPresenter _eggNotifier;

    [Tooltip("알·새끼용 목록이 있는 용 창. 통합 전에는 별도 창(UI_DragonInventoryWindow)이었다.")]
    [SerializeField] private UI_DragonWindow _inventoryWindow;
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새끼용을 골라 배치 대기 상태가 됐는지 판정하는 데 쓴다. 골랐으면 딤을 걷어 그리드를 그대로 보여준다.")]
    [SerializeField] private BuildingPlacementController _placementController;

    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("행동을 요구하는 안내 도중 밤으로 넘어가지 못하게 막는 데 쓴다. 비우면 막지 않는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("HUD의 새끼용 인벤토리 버튼. OpenInventory 단계에서 이것만 남기고 화면을 덮는다.")]
    [SerializeField] private RectTransform _inventoryButton;

    [Tooltip("인벤토리 토글 액션. 안내 문구에 실제 키 이름을 넣는 데만 쓴다 - 바인딩을 바꾸면 문구도 따라간다.")]
    [SerializeField] private InputActionReference _inventoryToggleAction;

    [Tooltip("안내 문구가 알려준 토글 키를 딤이 막지 않도록 예외를 등록하는 관문. 비우면 등록하지 않는다.")]
    [SerializeField] private UIManager _uiManager;

    [Tooltip("배치한 새끼용을 클릭하면 뜨는 관리창. 모드 안내를 이 창이 열렸을 때만 띄우는 데 쓴다.")]
    [SerializeField] private UI_BabyDragonManageWindow _manageWindow;

    [Tooltip("관리창의 공격/버프 모드 버튼을 감싸는 영역(ModeButtons). 모드 안내가 가리킬 대상이다.")]
    [SerializeField] private RectTransform _modeButtonsRect;

    [Tooltip("배치 이후(완료 문구·모드 안내)를 튜토리얼 챕터가 대신 안내한다. 켜면 이 가이드는 배치까지만 말한다 - " +
             "챕터가 우선순위로 덮어 가리는 것에 기대면 같은 내용을 두 곳이 들고 있게 되고, " +
             "문구 정리 때 어느 쪽이 죽은 것인지 알 수 없다.")]
    [SerializeField] private bool _chapterOwnsPostPlacement;

    /// <summary>
    /// 새 단계에 들어섰다. 건너뛴 중간 단계까지 한 번에 지나갈 수 있으므로 인자는 "도달한 단계"다 -
    /// 특정 지점을 기다리는 쪽은 == 이 아니라 >= 로 판정해야 한다.
    /// </summary>
    public UnityEvent<BabyDragonGuideStep> StepEntered = new();

    /// <summary>
    /// 알 확인 안내(WaitHatch)를 읽고 인벤토리를 닫았다. 부화·배치는 며칠 뒤지만 이것은 1일차에 끝나므로,
    /// "용의 알을 받아 확인한다" 목표가 완료를 판정하는 시점이다.
    /// 알을 받기 전에 인벤토리를 열었다 닫은 것은 여기 해당하지 않는다 - 그 경로로는 이 안내에 도달하지 못한다.
    /// </summary>
    public UnityEvent EggCheckGuideFinished = new();

    public bool HasFinishedEggCheckGuide => _hasFinishedWaitHatchGuide;

    // 아직 시작 전이면 값이 없다 - 첫 알을 얻는 순간 OpenInventory로 들어간다.
    private BabyDragonGuideStep? _currentStep;

    // 마무리 문구 중 지금 보여줄 컷. 길이를 넘어서면 안내를 놓는다.
    private int _completionIndex;

    // 부화 대기 안내를 한 번이라도 띄웠는지 / 그것을 읽고 창을 닫아 끝났는지.
    // 이 단계는 며칠 동안 이어지므로, 끝난 뒤에는 창을 다시 열어도 조용해야 한다.
    private bool _hasShownWaitHatchGuide;
    private bool _hasFinishedWaitHatchGuide;

    // 부화 대기는 두 컷이다 - 알 슬롯을 가리켜 "이게 네 알이다"를 알린 뒤(1컷),
    // 확인을 누르면 X 버튼을 가리켜 창을 닫게 한다(2컷). 이 값은 1컷을 읽었는지다.
    //
    // 한 컷에 몰지 않는 이유: 오버레이가 구멍 밖을 막으므로 구멍은 하나뿐이고,
    // 알 슬롯을 가리키면 X를 못 누르고 X를 가리키면 알이 어느 것인지 알 수 없다.
    private bool _hasReadEggSlotInfo;

    // 마지막으로 사유 줄을 낸 시각. 정지 중에도 흘러야 하므로 unscaled를 쓴다.
    private float _lastBlockedHintTime = float.NegativeInfinity;

    // 모드 안내를 이미 보여줬는지. 최초 1회만 뜨고 그 뒤로는 다시 뜨지 않는다 -
    // 새끼용을 클릭할 때마다 같은 안내가 따라붙으면 이미 읽은 문구가 계속 화면을 차지한다.
    // 확인 버튼을 누르든, 그냥 관리창을 닫든 그 한 번으로 끝난 것으로 본다.
    private bool _hasReadModeHint;

    // 직전 프레임의 관리창 열림 상태. 열리고 닫히는 순간만 잡으려는 것이라 값이 바뀔 때만 일한다.
    private bool _wasManageWindowOpen;

    // 지금 밤 잠금을 걸어 둔 상태인지. 값이 바뀔 때만 등록·해제한다.
    private bool _isBlockingDayEnd;

    // X 버튼 배선 누락 경고를 이미 냈는지. 매 프레임 도는 판정이라 한 번만 알린다.
    private bool _hasWarnedMissingExitButton;

    // 문구에 넣을 토글 키 이름. 오버레이가 매 프레임 물어보므로 그때마다 새로 만들면
    // 바뀌지도 않는 문자열과 배열이 계속 쌓인다.
    private object[] _toggleKeyArgs;

    private RunData CurrentRun => _gameManager == null ? null : _gameManager.CurrentRun;

    /// <summary>
    /// 지금 단계가 낮에 끝내야 하는 행동을 요구하는지. 부화 대기는 <b>밤을 넘겨야</b> 진행되므로 막으면 안 되고,
    /// 마무리 문구는 읽고 넘기기만 하면 되므로 붙잡을 이유가 없다.
    /// </summary>
    private bool BlocksDayEnd =>
        _currentStep == BabyDragonGuideStep.OpenInventory || _currentStep == BabyDragonGuideStep.PlaceDragon;

    bool IDayEndBlockQuery.CanEndDay()
    {
        return !BlocksDayEnd;
    }

    /// <summary>
    /// 딤과 별개로 막을 것은 없다. 이 안내는 며칠에 걸쳐 이어지므로(부화 대기) 도는 내내 막으면
    /// 그 기간 동안 게임의 단축키가 통째로 죽는다. 화면을 덮는 단계는 오버레이가 알아서 막는다.
    /// </summary>
    bool IShortcutBlockQuery.BlocksShortcuts()
    {
        return false;
    }

    /// <summary>
    /// 안내가 "이 키로 인벤토리를 열어라"라고 말하는 동안만 그 토글 키를 통과시킨다.
    /// OpenInventory 단계는 화면을 덮으므로(blocksInput) 이 예외가 없으면 <b>문구가 알려준 키가 먹지 않는다</b> -
    /// 문구에 키 이름을 넣어 두고(ResolveToggleKeyLabel) 정작 그 키를 막으면 안내가 거짓말이 된다.
    ///
    /// 창이 이미 열려 있으면 허용하지 않는다. 그때 이 키는 '닫기'가 되어 방금 열라고 시킨 것을 되돌린다.
    /// </summary>
    /// <summary>
    /// 안내가 화면에 떠 있는 동안에는 지금 유도하는 창 말고 다른 배타 창이 열리지 않게 한다 -
    /// 다른 창이 열리면 <see cref="UIManager.CloseAllExcept"/>가 안내가 가리키던 창을 닫아버려
    /// 무엇을 하라는 안내인지 알 수 없게 된다(알 확인 중에 점령 버튼을 눌러 실제로 그렇게 됐다).
    ///
    /// <b>표시 중일 때만 막는다.</b> 부화 대기는 며칠씩 이어지므로 단계가 살아 있다는 이유로 막으면
    /// 그 기간 내내 건설·점령이 잠긴다. 안내를 읽고 창을 닫으면 표시권을 놓으므로 곧바로 풀린다.
    /// </summary>
    bool IExclusiveModeOpenQuery.CanOpen(MonoBehaviour mode)
    {
        // mode가 null이면 "여는 것"이 아니라 CloseAllExcept(null) - 화면을 정리하려는 쪽이므로 막지 않는다.
        // 막으면 1일차 튜토리얼이 시작하며 화면을 치우지 못해 자기 딤에 남의 창이 덮인 채로 돈다.
        //
        // 화면을 쥐고 있는 동안만 막는다 - 대상을 잃어 화면을 비운 상태에서까지 막으면
        // 아무 안내도 없는데 모든 창이 안 열린다.
        if (mode == null || _overlay == null || !_overlay.IsShowingFor(this))
        {
            return true;
        }

        return ReferenceEquals(mode, _inventoryWindow) || Reject();
    }

    /// <summary>
    /// 닫는 것은 언제든 허용한다. 이 안내는 창을 닫아 하루를 보내라고 시키는 단계(WaitHatch)를 포함하고,
    /// 열기만 막으면서 닫기까지 막으면 빠져나갈 길이 사라진다.
    /// </summary>
    bool IExclusiveModeOpenQuery.CanClose(MonoBehaviour mode)
    {
        // 안내가 창 안을 가리키는 동안에는 ESC로 닫지 못하게 한다 - 닫히면 가리킬 곳이 사라져
        // 안내가 앞 단계로 되돌아가고, 플레이어는 방금 시킨 것을 처음부터 다시 해야 한다.
        //
        // 창을 닫아 하루를 보내라고 시키는 단계(WaitHatch)와 마무리 문구는 그대로 열어 둔다 -
        // 여기까지 막으면 시킨 대로 했는데도 진행이 안 되는 상태가 된다.
        if (_overlay == null || !_overlay.IsShowingFor(this) ||
            !ReferenceEquals(mode, _inventoryWindow) || !IsInventoryOpen)
        {
            return true;
        }

        // 부화 대기 단계는 두 컷으로 나뉜다 - 알 슬롯을 설명하는 앞 컷에서 닫으면 설명이 날아가므로,
        // "이제 닫으세요"까지 읽은 뒤(_hasReadEggSlotInfo)에만 연다. 단축키 예외와 같은 판정이다.
        bool isCloseRequested = _currentStep == BabyDragonGuideStep.WaitHatch && _hasReadEggSlotInfo;

        if (isCloseRequested || _currentStep == BabyDragonGuideStep.Completed)
        {
            return true;
        }

        return Reject();
    }

    bool IShortcutBlockQuery.AllowsShortcut(MonoBehaviour mode)
    {
        if (_overlay == null || !_overlay.IsShowingFor(this) || !ReferenceEquals(mode, _inventoryWindow))
        {
            return Reject();
        }

        // 창을 열라고 시키는 단계 - 문구가 이름을 알려준 바로 그 키다.
        if (!IsInventoryOpen)
        {
            return _currentStep == BabyDragonGuideStep.OpenInventory ||
                   _currentStep == BabyDragonGuideStep.PlaceDragon ||
                   Reject();
        }

        // 창을 닫으라고 시키는 컷. 여는 법으로 알려준 키를 닫을 때만 막으면 앞뒤가 맞지 않는다 -
        // 토글 키는 같은 키로 여닫는 것이 당연하므로, 알려준 이상 양쪽 다 통해야 한다.
        return (_currentStep == BabyDragonGuideStep.WaitHatch && _hasReadEggSlotInfo) || Reject();
    }

    /// <summary>
    /// 막았다는 사유를 그 자리에서 알리고 거절한다. 무반응으로 두면 플레이어가 버그로 읽는다 -
    /// 항상 false를 돌려주므로 거절 지점에 <c>|| Reject()</c> 꼴로 붙인다.
    /// </summary>
    private bool Reject()
    {
        NotifyBlocked();
        return false;
    }

    /// <summary>
    /// 사유 줄을 말풍선 아래에 낸다. 딤에 삼켜진 클릭(<see cref="IGuideRequestProvider.OnBlockedClicked"/>)처럼
    /// 거절값을 돌려줄 곳이 없는 경로도 이것을 직접 부른다.
    ///
    /// 화면을 쓰고 있을 때만 말한다 - 사유 줄은 말풍선 안에 있어 표시권이 없으면 띄워도 보이지 않고,
    /// 튜토리얼 러너와 이 가이드가 같은 관문에 함께 등록돼 있어 그러지 않으면 둘이 같이 말한다.
    /// </summary>
    private void NotifyBlocked()
    {
        if (_overlay == null || !_overlay.IsShowingFor(this))
        {
            return;
        }

        if (Time.unscaledTime - _lastBlockedHintTime < BLOCKED_HINT_COOLDOWN_SECONDS)
        {
            return;
        }

        _lastBlockedHintTime = Time.unscaledTime;
        _overlay.ShowHint(this, Defines.TUTORIAL_BLOCKED_HINT_LOC_KEY, BLOCKED_HINT_DURATION_SECONDS);
    }

    void IGuideRequestProvider.OnBlockedClicked() => NotifyBlocked();

    int IGuideRequestProvider.Priority => GuidePriority.BABY_DRAGON_GUIDE;

    private void OnEnable()
    {
        EnsureEggNotifier();

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);

            if (_eggNotifier == null)
            {
                _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatchedWithoutNotification);
            }
        }

        if (_eggNotifier != null)
        {
            _eggNotifier.HatchNotificationDismissed += HandleHatchNotificationDismissed;
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.AddListener(HandleTabDisplayed);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.AddShortcutQuery(this);
            _uiManager.AddOpenQuery(this);
        }

        // 이 컴포넌트의 활성 체크박스가 곧 가이드 on/off 스위치다. 등록해 두면 오버레이가 매 프레임 물어보므로,
        // 플레이 중 다시 켜도 그 프레임의 단계가 그대로 화면에 돌아온다.
        if (_overlay != null)
        {
            _overlay.AddProvider(this);
        }
    }

    private void OnDisable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatchedWithoutNotification);
        }

        if (_eggNotifier != null)
        {
            _eggNotifier.HatchNotificationDismissed -= HandleHatchNotificationDismissed;
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.RemoveListener(HandleTabDisplayed);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.RemoveShortcutQuery(this);
            _uiManager.RemoveOpenQuery(this);
        }

        // 끄면 밤 잠금도 같이 풀어준다 - 안 그러면 영영 막힌 채로 남는다.
        if (_cycleManager != null)
        {
            _cycleManager.RemoveDayEndBlocker(this);
        }

        // 방금 뗐으므로 래치도 같이 내린다 - 안 그러면 배치 도중 껐다 켰을 때
        // UpdateDayEndGate가 "이미 막는 중"으로 보고 잠금을 다시 걸지 않는다.
        _isBlockingDayEnd = false;

        // 목록에서 빠지면 오버레이가 더 이상 묻지 않으므로 떠 있던 딤·말풍선도 다음 프레임에 걷힌다.
        if (_overlay != null)
        {
            _overlay.RemoveProvider(this);
        }
    }

    private void Start()
    {
        // 비어 있으면 배치 대기 판정이 늘 false가 되어 딤이 걷히지 않는데, 그게 조용히 넘어가면 원인을 찾기 어렵다.
        if (_placementController == null)
        {
            Debug.LogWarning("[BabyDragonGuideController] _placementController가 비어 있어 새끼용 배치 중에도 딤이 그대로 남습니다.", this);
        }

        RestoreFromRunData();
    }

    // 인스펙터로 미리 채워둔 진행도나 이어하기 상태에서 시작해도 안내가 처음부터 다시 뜨지 않게 한다.
    private void RestoreFromRunData()
    {
        RunData run = CurrentRun;
        if (run == null || run.EnteredGuideSteps == null)
        {
            return;
        }

        foreach (BabyDragonGuideStep step in run.EnteredGuideSteps)
        {
            if (!_currentStep.HasValue || step > _currentStep.Value)
            {
                _currentStep = step;
            }
        }

        if (!_currentStep.HasValue)
        {
            return;
        }

        // 단계만 되돌리면 그 단계 안의 진행도가 0으로 남아 이미 읽은 안내가 다시 뜬다.
        // 지나온 단계에서 파생되는 값이므로 저장하지 않고 여기서 도로 계산한다.
        _hasShownWaitHatchGuide = _currentStep.Value > BabyDragonGuideStep.WaitHatch;
        _hasFinishedWaitHatchGuide = _hasShownWaitHatchGuide;
        _hasReadEggSlotInfo = _hasShownWaitHatchGuide;

        // 완료 문구는 Completed에서 다 읽고 넘어가는 것이므로, 그 뒤 단계로 복구했다면 이미 읽은 것이다.
        // >= 가 아니라 == 로 두면 ChangeMode에서 이어할 때 완료 문구 두 컷이 처음부터 다시 뜬다.
        if (_currentStep.Value >= BabyDragonGuideStep.Completed)
        {
            _completionIndex = COMPLETION_LOC_KEYS.Length;
        }
    }

    // 알 획득 토스트는 결과 알림이고, 인벤토리 버튼 안내는 다음 행동이므로 함께 보여도 역할이 겹치지 않는다.
    // 토스트 전체 길이를 기다리면 플레이어는 그동안 다음 행동을 알 수 없으므로 지급 프레임에 바로 유도한다.
    private void HandleEggGranted(DragonType _)
    {
        Advance(BabyDragonGuideStep.OpenInventory);

        // 알을 받기 전에 이미 인벤토리를 열어 둔 플레이어에게 HUD 버튼을 다시 누르게 하지 않는다.
        if (IsInventoryOpen)
        {
            Advance(BabyDragonGuideStep.WaitHatch);
        }
    }

    private void HandleHatchNotificationDismissed(DragonType _) =>
        Advance(BabyDragonGuideStep.PlaceDragon);

    private void HandleEggHatchedWithoutNotification(DragonType _) =>
        Advance(BabyDragonGuideStep.PlaceDragon);

    // 사용자가 방금 누른 결과라 즉시 반응해야 한다 - 지연시키면 조작이 먹지 않은 것처럼 보인다.
    //
    // 여기서 기다리는 것은 "새끼용 탭을 눌렀다"가 아니라 "창을 열었다"이다. 창은 마지막에 보던 탭으로
    // 열리므로(기본값은 어미용) 탭 종류로 판정하면 안 된다 - 그렇게 막았더니 창을 열어도 단계가
    // 그대로라, 입력을 차단한 채 HUD 버튼만 가리키는 상태로 갇혔다. 어느 탭이든 창이 열렸으면
    // WaitHatch로 넘어가고, 새끼용 탭으로 옮기는 유도는 그 단계의 ShowSlotGuide가 맡는다.
    //
    // 다만 UI_DragonWindow는 Awake에서 탭 시각 상태를 맞추려고 SelectTab을 한 번 부르고 그것도
    // 발화한다 - 창이 열리기도 전이다. 그것까지 받으면 게임 시작 시점에 안내가 건너뛰어
    // 첫 안내(인벤토리를 열어라)가 통째로 사라진다. 그래서 창이 열려 있는지를 본다.
    private void HandleTabDisplayed(bool _)
    {
        // 알을 받기 전에 인벤토리를 열어 본 것은 이 안내의 완료 조건이 아니다.
        // 시작 전 탭 이벤트를 받아 WaitHatch까지 기록하면, 이후 알 획득 시 OpenInventory가
        // 과거 단계로 취급되어 안내가 영영 시작되지 않는다.
        if (!IsInventoryOpen || !_currentStep.HasValue)
        {
            return;
        }

        Advance(BabyDragonGuideStep.WaitHatch);
    }

    private void EnsureEggNotifier()
    {
        if (_eggNotifier == null)
        {
            _eggNotifier = FindFirstObjectByType<ProgressionNotificationPresenter>();
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (building is BabyDragonTower)
        {
            Advance(BabyDragonGuideStep.Completed);
        }
    }

    /// <summary>
    /// 단조 전진 - 뒤로는 가지 않고, 건너뛴 중간 단계는 진입한 것으로 함께 기록한다.
    /// 1일차에 인벤토리를 한 번도 열지 않고 밤을 넘기면 부화 시점의 현재 단계가 아직 OpenInventory인데,
    /// 엄격히 순차로 짜면 그 부화를 놓쳐 안내가 HUD 버튼에 붙박이로 남는다.
    /// </summary>
    private void Advance(BabyDragonGuideStep step)
    {
        if (_currentStep.HasValue && step <= _currentStep.Value)
        {
            return;
        }

        RunData run = CurrentRun;
        BabyDragonGuideStep from = _currentStep.HasValue ? _currentStep.Value + 1 : BabyDragonGuideStep.OpenInventory;
        for (BabyDragonGuideStep passed = from; passed <= step; passed++)
        {
            run?.TryEnterGuideStep(passed);
        }

        BabyDragonGuideStep? previous = _currentStep;
        _currentStep = step;

        if (step == BabyDragonGuideStep.Completed)
        {
            AnnounceCompletion();
        }

        // 어느 단계까지 갔는지는 화면만 봐서는 되짚을 수 없다 - 안내가 안 뜬다는 제보가 오면
        // 단계가 안 넘어간 것인지 넘어갔는데 못 그린 것인지를 이 로그로 가른다
        // (TutorialRunner가 단계마다 로그를 남기는 것과 같은 이유).
        Debug.Log($"[BabyDragonGuideController] 단계 진입: {previous?.ToString() ?? "-"} → {step}", this);

        StepEntered.Invoke(step);
    }

    // 완료 안내도 오버레이로 낸다 - 강제 안내인데 토스트로 흘려보내면 말풍선과 겹쳐 둘 다 읽히지 않는다.
    // 가리킬 대상이 없으므로 딤 없이 말풍선만 띄우고, 확인 버튼으로 두 컷을 순서대로 넘긴다.
    private void AnnounceCompletion()
    {
        _completionIndex = 0;
    }

    // 오버레이가 지금 그린 그림의 주인에게만 보내므로 남의 클릭이 섞이지 않는다 -
    // 예전에는 이벤트가 구독자 전원에게 가서 뜨지도 않은 완료 문구가 함께 넘어갔다.
    void IGuideRequestProvider.OnConfirmClicked()
    {
        // 부화 대기 1컷(알 슬롯)을 읽었다 - 이제 닫는 법을 알려준다.
        if (_currentStep == BabyDragonGuideStep.WaitHatch && !_hasReadEggSlotInfo)
        {
            _hasReadEggSlotInfo = true;
            return;
        }

        if (_currentStep == BabyDragonGuideStep.Completed && _completionIndex < COMPLETION_LOC_KEYS.Length)
        {
            _completionIndex++;

            // 완료 문구를 다 읽었으면 모드 안내로 넘어간다. 바로 뜨지는 않는다 -
            // 직전 문구가 "새끼용을 클릭하라"이고, 실제로 클릭해 관리창이 열려야 버튼을 가리킬 수 있다.
            if (_completionIndex >= COMPLETION_LOC_KEYS.Length)
            {
                Advance(BabyDragonGuideStep.ChangeMode);
            }

            return;
        }

        if (_currentStep == BabyDragonGuideStep.ChangeMode)
        {
            _hasReadModeHint = true;
        }
    }

    /// <summary>
    /// 밤 잠금을 지금 단계에 맞춘다. 등록은 여럿이 함께 걸 수 있으므로 남이 건 것과 다투지 않는다 -
    /// 하나라도 막고 있으면 밤으로 넘어가지 않는다.
    ///
    /// <b>이것은 상태 전이라 Update에서만 한다.</b> 안내를 그릴지 묻는 자리(TryGetRequest)에 두면
    /// 게이트 질의가 도는 것만으로 밤 잠금이 붙었다 떨어진다.
    /// </summary>
    private void UpdateDayEndGate()
    {
        if (_cycleManager == null || BlocksDayEnd == _isBlockingDayEnd)
        {
            return;
        }

        _isBlockingDayEnd = BlocksDayEnd;

        if (_isBlockingDayEnd)
        {
            _cycleManager.AddDayEndBlocker(this);
            return;
        }

        _cycleManager.RemoveDayEndBlocker(this);
    }

    /// <summary>
    /// 부화 대기 안내의 진행을 단계에 맞춘다. 예전에는 그리는 자리에서 함께 했는데, 그러면
    /// "지금 누가 그리는가"를 묻기만 해도 안내가 끝난 것으로 기록되고 목표 완료가 발화한다.
    /// </summary>
    private void UpdateWaitHatchProgress()
    {
        if (_currentStep != BabyDragonGuideStep.WaitHatch || _hasFinishedWaitHatchGuide)
        {
            return;
        }

        // 창을 닫은 건 시킨 대로 한 것이다 - 이 단계에서 가르칠 것은 거기서 끝난다.
        // 부화까지 며칠이 걸리는 동안 창을 열 때마다 같은 말을 다시 띄우면 이미 읽은 안내가 계속 따라다닌다.
        if (!IsInventoryOpen)
        {
            FinishWaitHatchGuideIfShown();
            return;
        }

        _hasShownWaitHatchGuide = true;

        // 가리킬 X 버튼이 없으면 안내를 낼 수 없다. 매 프레임 도는 판정이라 한 번만 알린다.
        if (_hasReadEggSlotInfo && _inventoryWindow != null && _inventoryWindow.ExitButtonRect == null &&
            !_hasWarnedMissingExitButton)
        {
            _hasWarnedMissingExitButton = true;
            Debug.LogWarning("[BabyDragonGuideController] 용 창의 X 버튼을 찾지 못해 닫기 안내를 띄우지 못합니다.", this);
        }
    }

    /// <summary>
    /// 지금 낼 안내. <b>읽기만 한다</b> - 오버레이는 게이트 질의 때문에 한 프레임에 여러 번 물을 수 있고,
    /// 여기서 상태가 바뀌면 "누가 그리는가"를 묻는 것만으로 안내가 전진한다.
    ///
    /// 낼 것이 없으면 false를 돌려 화면을 넘긴다 - 이 가이드는 우선순위가 가장 낮으므로,
    /// 넘기지 않고 붙들고 있으면 그 위의 안내가 아니라 아무것도 뜨지 않는다.
    /// </summary>
    bool IGuideRequestProvider.TryGetRequest(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        if (!_currentStep.HasValue)
        {
            return false;
        }

        switch (_currentStep.Value)
        {
            case BabyDragonGuideStep.OpenInventory:
                // HUD 버튼 하나만 통로로 남는다 - 한 번 누르면 끝나는 행동이라 막혀도 갇히지 않는다.
                request = BuildRequest(_inventoryButton, OPEN_INVENTORY_LOC_KEY,
                    showsConfirmButton: false, ToggleKeyArgs);
                return true;

            case BabyDragonGuideStep.WaitHatch:
                // 창을 닫은 건 시킨 대로 한 것이다 - 다시 열라고 하면 안내가 제자리를 돈다.
                if (!IsInventoryOpen || _hasFinishedWaitHatchGuide)
                {
                    return false;
                }

                // 알 슬롯 → X 버튼 두 컷으로 나눠 보여준다. 닫기는 X 버튼으로 한다 -
                // 토글 키로 닫는 경로가 없어서 키 이름을 알려주면 헛짚는다.
                return TryGetWaitHatchRequest(out request);

            case BabyDragonGuideStep.PlaceDragon:
                // 새끼용을 이미 골랐다면 남은 일은 타일을 찍는 것뿐이다 - 가리킬 대상 없이 말풍선만 띄워
                // 딤을 걷는다. 그리드를 덮은 채로 "타일을 클릭하라"고 하면 어디를 눌러야 할지 가려진다.
                if (IsPlacingBabyDragon)
                {
                    request = BuildRequest(null, PLACE_DRAGON_TILE_LOC_KEY, showsConfirmButton: false);
                    return true;
                }

                // 부화는 WaitHatch에서 창을 닫아둔 상태로 맞이하므로, 여기서 다시 열도록 HUD 버튼을 강조한다.
                // 대상이 있으니 오버레이가 그 밖을 막는다 - 이 안내 중에 건설 버튼이 눌려 농장이 지어진 적이 있다.
                // 구멍이 HUD 버튼이라 그건 눌리고, 토글 키도 AllowsShortcut이 예외로 통과시킨다.
                if (!IsInventoryOpen)
                {
                    request = BuildRequest(_inventoryButton, REOPEN_INVENTORY_LOC_KEY,
                        showsConfirmButton: false, ToggleKeyArgs);
                    return true;
                }

                // 배치는 슬롯의 위치 버튼을 누른 뒤 그리드를 눌러야 끝나므로 화면을 막으면 진행이 막힌다.
                return TryGetSlotRequest(PLACE_DRAGON_LOC_KEY, out request);

            case BabyDragonGuideStep.Completed:
                // 가리킬 대상이 없다 - 화면을 통째로 덮고 말풍선과 확인 버튼만 남긴다.
                if (_chapterOwnsPostPlacement || _completionIndex >= COMPLETION_LOC_KEYS.Length)
                {
                    return false;
                }

                request = BuildRequest(null, COMPLETION_LOC_KEYS[_completionIndex], showsConfirmButton: true);
                return true;

            case BabyDragonGuideStep.ChangeMode:
                // 관리창이 열려야 가리킬 버튼이 생긴다. 닫혀 있으면 조용히 물러나 플레이어가 새끼용을
                // 클릭할 때까지 기다린다 - 구멍이 모드 버튼이라 안내를 보면서 그 버튼을 바로 눌러볼 수 있다.
                if (_chapterOwnsPostPlacement || _hasReadModeHint || _modeButtonsRect == null ||
                    _manageWindow == null || !_manageWindow.IsOpen)
                {
                    return false;
                }

                request = BuildRequest(_modeButtonsRect, MODE_HINT_LOC_KEY, showsConfirmButton: true);
                return true;

            default:
                return false;
        }
    }

    // 이 판정은 매 프레임 도므로 전이할 때 한 번만 알린다 - 매번 발화하면
    // 같은 완료가 반복해서 흘러간다. 복원(RestoreFromRunData)은 이 길을 타지 않는다 -
    // 이미 끝난 안내를 다시 알릴 이유가 없고, 목표 완료는 RunData에 남아 있다.
    private void FinishWaitHatchGuideIfShown()
    {
        if (_hasFinishedWaitHatchGuide || !_hasShownWaitHatchGuide)
        {
            return;
        }

        _hasFinishedWaitHatchGuide = true;
        EggCheckGuideFinished.Invoke();
    }

    // 새끼용 안내는 전부 눌러보게 하는 단계라 대상을 막지 않는다.
    // 딤과 입력 차단은 넘기지 않는다 - 오버레이가 대상·확인 버튼 유무로 스스로 정한다.
    //
    // 기본값을 두지 않는다 - params 배열 앞의 선택 인자는 호출부가 빠뜨렸을 때 조용히 엉뚱한 자리에 묶인다.
    private static GuideRequest BuildRequest(
        RectTransform target, string locKey, bool showsConfirmButton, params object[] args)
    {
        return GuideRequest.Draw(
            target,
            locKey,
            blocksTargetInteraction: false,
            keepsInputOpen: false,
            showsConfirmButton,
            ResolveBubbleSlot(locKey),
            args);
    }

    // 말풍선 자리는 그 단계가 무엇을 가리지 말아야 하는지로 정한다.
    // 튜토리얼 쪽은 TutorialStepSO에 자리를 데이터로 두지만, 이 가이드는 SO 없이 코드로 도는 대신
    // 여기 한 곳에서 고른다 - 자리를 바꾸려면 이 함수만 보면 된다.
    private static GuideBubbleSlot ResolveBubbleSlot(string locKey) => locKey switch
    {
        // 배치할 타일을 클릭해야 하므로 그리드를 가리면 안 된다.
        PLACE_DRAGON_TILE_LOC_KEY => GuideBubbleSlot.Bottom,

        // 목록에서 새끼용을 고른 다음 타일까지 클릭해야 한다 - 위 단계와 같은 이유로 그리드를 비운다.
        PLACE_DRAGON_LOC_KEY => GuideBubbleSlot.Bottom,

        // 관리창은 화면 왼쪽에 붙으므로 기본 자리와 겹친다 - 가운데로 옮겨 둘 다 보이게 한다.
        MODE_HINT_LOC_KEY => GuideBubbleSlot.Center,

        // 인벤토리 버튼·탭·닫기 버튼은 모두 화면 위쪽에 있어 기본 자리와 겹친다.
        // 가리키는 대상과 말풍선이 붙어 버리면 어느 쪽을 읽어야 할지 알 수 없다.
        OPEN_INVENTORY_LOC_KEY => GuideBubbleSlot.Center,
        REOPEN_INVENTORY_LOC_KEY => GuideBubbleSlot.Center,
        SWITCH_TAB_LOC_KEY => GuideBubbleSlot.Center,
        CLOSE_INVENTORY_LOC_KEY => GuideBubbleSlot.Center,

        _ => GuideBubbleSlot.Default,
    };

    private object[] ToggleKeyArgs => _toggleKeyArgs ??= new object[] { ResolveToggleKeyLabel() };

    // 참조가 비어 있어도 문구 자체는 떠야 하므로 키 이름만 빈 문자열로 대체한다.
    private string ResolveToggleKeyLabel()
    {
        return _inventoryToggleAction == null || _inventoryToggleAction.action == null
            ? string.Empty
            : InputBindingLabel.Resolve(_inventoryToggleAction.action);
    }

    private bool IsInventoryOpen => _inventoryWindow != null && _inventoryWindow.IsOpen;

    // 새끼용 고스트가 커서를 따라다니는 중. ESC로 창을 닫아도 고스트는 살아있으므로
    // (용 창은 창만 닫고 배치를 취소하지 않는다) 창 열림 여부보다 이 판정이 먼저다.
    private bool IsPlacingBabyDragon =>
        _placementController != null && _placementController.BuildingToPlace is BabyDragonTower;

    /// <summary>
    /// 관리창(= 새끼용을 클릭했는지)이 열리고 닫히는 순간을 잡는다.
    ///
    /// 이벤트가 아니라 상태를 보는 이유: 관리창은 열림 이벤트를 발행하지 않고 자기 Update에서
    /// SelectedBuilding을 폴링해 열린다. 선택 변경 이벤트에 맞춰 그리면 그 프레임에는 창이 아직
    /// 닫혀 있을 수 있는데(두 Update의 실행 순서는 보장되지 않는다), 그때 접고 물러나면
    /// 다시 그릴 계기가 없어 안내가 영영 뜨지 않는다. 상태로 보면 순서와 무관해진다.
    ///
    /// 값이 바뀔 때만 일하므로 평소 비용은 bool 비교 하나다.
    /// </summary>
    private void Update()
    {
        // 상태 전이는 전부 여기서 한다. 그리는 자리(TryGetRequest)는 읽기만 하므로,
        // 밤 잠금·안내 완료 발화가 게이트 질의 횟수에 따라 달라지지 않는다.
        UpdateDayEndGate();
        UpdateWaitHatchProgress();
        TrackManageWindow();
    }

    private void TrackManageWindow()
    {
        bool isManageWindowOpen = _manageWindow != null && _manageWindow.IsOpen;
        if (isManageWindowOpen == _wasManageWindowOpen)
        {
            return;
        }

        _wasManageWindowOpen = isManageWindowOpen;

        // 창이 열렸다 - 새끼용을 클릭한 순간이다. 완료 문구의 확인을 눌렀는지는 보지 않는다.
        // 다 읽어야만 볼 수 있게 하면, 문구를 넘기지 않고 먼저 클릭해 본 플레이어는 이 안내를 놓친다.
        if (isManageWindowOpen)
        {
            // 확인으로 이미 이 단계에 와 있으면 Advance는 아무것도 하지 않는다 -
            // 안내는 다음 프레임에 오버레이가 물어보는 것으로 알아서 돌아온다.
            Advance(BabyDragonGuideStep.ChangeMode);
            return;
        }

        // 창이 닫혔다 - 가리키던 버튼이 사라졌으므로 안내를 거두고, 이번 한 번으로 끝낸다.
        // 확인을 눌렀는지는 보지 않는다("첫 클릭 한 번만") - 창을 닫은 것도 다 봤다는 뜻으로 친다.
        if (_currentStep == BabyDragonGuideStep.ChangeMode)
        {
            _hasReadModeHint = true;
        }
    }

    // 창이 열려 있는 동안 그 안의 대상을 강조한다. 창이 닫힌 동안 무엇을 할지는 단계마다 다르므로
    // 여기서 정하지 않고 호출부(TryGetRequest)가 미리 걸러낸다.
    //
    // 알 탭과 용 탭이 따로였을 때는 "원하는 탭으로 바꾸게 한 뒤 그 탭의 첫 슬롯"이었다.
    // 지금은 둘이 새끼용 탭 한 패널에 함께 있으므로, 탭 유도는 한 번뿐이고 그 뒤에는
    // 어느 목록의 슬롯을 가리킬지 단계가 직접 고른다.
    private bool TryGetSlotRequest(string slotLocKey, out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        if (!IsInventoryOpen)
        {
            return false;
        }

        // 차단·딤은 넘기지 않는다 - 오버레이가 "유도한 곳 말고는 막는다"로 통일해 정하므로,
        // 여기서 다시 정하면 규칙이 두 곳으로 갈린다.
        if (!_inventoryWindow.IsBabyTabShown)
        {
            request = BuildRequest(_inventoryWindow.BabyTabRect, SWITCH_TAB_LOC_KEY, showsConfirmButton: false);
            return true;
        }

        // 새끼용은 슬롯 전체가 버튼이라(Icon_Focus는 지금 어느 동작인지 보여주는 그림일 뿐) 슬롯을
        // 통째로 가리킨다 - 오버레이가 구멍 밖을 막으므로 구멍은 실제 클릭 범위와 같아야 한다.
        // 아래 두 호출이 지금은 같은 rect를 돌려주지만, 슬롯이 아직 안 그려졌을 때를 위해 물러날 곳을 남긴다.
        if (_inventoryWindow.TryGetFirstBabyDragonFocusButtonRect(out RectTransform slotRect) ||
            _inventoryWindow.TryGetFirstBabyDragonSlotRect(out slotRect))
        {
            request = BuildRequest(slotRect, slotLocKey, showsConfirmButton: false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 부화 대기 안내. 알 슬롯과 X 버튼 두 곳을 가리켜야 하는데 구멍은 하나뿐이므로 컷을 나눈다 -
    /// 알을 보여주고(확인), 그다음 닫는 법을 알려준다. 완료 문구가 두 컷으로 넘어가는 것과 같은 방식이다.
    ///
    /// 가리킬 곳은 반드시 <b>실제로 눌러야 하는 것</b>이어야 한다. 오버레이가 구멍 밖을 막으므로,
    /// 반응 없는 알 슬롯(SetupEgg에서 interactable=false)만 가리킨 채 닫으라고 하면 그 자리에서 갇힌다.
    /// 1컷에 확인 버튼을 두는 이유가 이것이다 - 알 슬롯이 통로가 되지 못하므로 버튼이 유일한 출구다.
    /// </summary>
    private bool TryGetWaitHatchRequest(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        // 새끼용 탭으로 옮기라는 유도가 먼저다 - 알을 보여주고 나서 닫게 해야 순서가 맞는다.
        if (!_inventoryWindow.IsBabyTabShown)
        {
            request = BuildRequest(_inventoryWindow.BabyTabRect, SWITCH_TAB_LOC_KEY, showsConfirmButton: false);
            return true;
        }

        // 1컷 - 알 슬롯. 아직 안 그려졌으면 기다리지 않고 닫기 안내로 넘어간다(창은 어차피 닫아야 한다).
        if (!_hasReadEggSlotInfo && _inventoryWindow.TryGetFirstEggSlotRect(out RectTransform eggRect))
        {
            request = BuildRequest(eggRect, WAIT_HATCH_LOC_KEY, showsConfirmButton: true);
            return true;
        }

        // 2컷 - X 버튼. 가리킬 것이 없으면 막지 않는다 - 막고서 통로를 못 내면 창을 닫을 방법이 사라진다.
        // (배선이 빠진 경우의 경고는 UpdateWaitHatchProgress가 한 번만 낸다.)
        RectTransform exitRect = _inventoryWindow.ExitButtonRect;
        if (exitRect == null)
        {
            return false;
        }

        // 여는 법으로 알려준 토글 키를 닫는 문구에도 함께 적는다 - 같은 키로 여닫는 것이 당연한데
        // 한쪽만 알려주면 플레이어는 그 키가 닫기에는 안 듣는 줄 안다(실제로 막혀 있기도 했다).
        request = BuildRequest(exitRect, CLOSE_INVENTORY_LOC_KEY, showsConfirmButton: false, ToggleKeyArgs);
        return true;
    }
}
