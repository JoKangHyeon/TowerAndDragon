using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 새끼용 가이드(획득 → 인벤토리 → 부화 → 배치)의 단계 판정. 표시는 UI_GuideOverlay가 맡고
/// 여기서는 "지금 어느 단계인지"와 "어디를 가리킬지"만 정한다.
/// 진행도는 RunData.EnteredGuideSteps에 남겨 같은 런에서 같은 안내가 두 번 뜨지 않게 한다.
/// 획득 토스트(BabyDragonEggNotifier)는 이 컨트롤러와 무관하게 매번 뜬다 - 안내는 1회성,
/// 알림은 상시라는 구분이다.
/// </summary>
public class BabyDragonGuideController : MonoBehaviour, IDayEndBlockQuery
{
    private const string OPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_open_inventory";
    private const string REOPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_reopen_inventory";
    private const string SWITCH_TAB_LOC_KEY = "baby_dragon_guide_switch_tab";
    private const string WAIT_HATCH_LOC_KEY = "baby_dragon_guide_wait_hatch";
    private const string PLACE_DRAGON_LOC_KEY = "baby_dragon_guide_place_dragon";
    private const string PLACE_DRAGON_TILE_LOC_KEY = "baby_dragon_guide_place_dragon_tile";
    private const string COMPLETED_LOC_KEY = "baby_dragon_guide_completed";
    private const string MANAGE_HINT_LOC_KEY = "baby_dragon_guide_manage_hint";

    // 배치가 끝난 뒤 순서대로 보여줄 마무리 문구. 확인 버튼으로 한 컷씩 넘긴다.
    private static readonly string[] COMPLETION_LOC_KEYS = { COMPLETED_LOC_KEY, MANAGE_HINT_LOC_KEY };

    private const float DEFAULT_GUIDE_START_DELAY = 0f;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [Tooltip("알·새끼용 목록이 있는 용 창. 통합 전에는 별도 창(UI_DragonInventoryWindow)이었다.")]
    [SerializeField] private UI_DragonWindow _inventoryWindow;
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새끼용을 골라 배치 대기 상태가 됐는지 판정하는 데 쓴다. 골랐으면 딤을 걷어 그리드를 그대로 보여준다.")]
    [SerializeField] private BuildingPlacementController _placementController;

    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("행동을 요구하는 안내 도중 밤으로 넘어가지 못하게 막는 데 쓴다. 비우면 막지 않는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("안내가 끝났을 때 남길 문구를 띄운다. 획득 토스트와 같은 오브젝트를 써도 된다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("HUD의 새끼용 인벤토리 버튼. OpenInventory 단계에서 이것만 남기고 화면을 덮는다.")]
    [SerializeField] private RectTransform _inventoryButton;

    [Tooltip("인벤토리 토글 액션. 안내 문구에 실제 키 이름을 넣는 데만 쓴다 - 바인딩을 바꾸면 문구도 따라간다.")]
    [SerializeField] private InputActionReference _inventoryToggleAction;

    [Tooltip("부화 토스트가 사라진 뒤 배치 안내를 시작하기까지의 추가 여유(초). " +
             "0이면 토스트가 사라지는 즉시 다음 행동을 안내한다. 토스트 자체의 길이는 포함하지 않는다.")]
    [SerializeField] private float _guideStartDelay = DEFAULT_GUIDE_START_DELAY;

    /// <summary>
    /// 새 단계에 들어섰다. 건너뛴 중간 단계까지 한 번에 지나갈 수 있으므로 인자는 "도달한 단계"다 -
    /// 특정 지점을 기다리는 쪽은 == 이 아니라 >= 로 판정해야 한다.
    /// </summary>
    public UnityEvent<BabyDragonGuideStep> StepEntered = new();

    // 아직 시작 전이면 값이 없다 - 첫 알을 얻는 순간 OpenInventory로 들어간다.
    private BabyDragonGuideStep? _currentStep;

    // 마무리 문구 중 지금 보여줄 컷. 길이를 넘어서면 안내를 놓는다.
    private int _completionIndex;

    // 부화 대기 안내를 한 번이라도 띄웠는지 / 그것을 읽고 창을 닫아 끝났는지.
    // 이 단계는 며칠 동안 이어지므로, 끝난 뒤에는 창을 다시 열어도 조용해야 한다.
    private bool _hasShownWaitHatchGuide;
    private bool _hasFinishedWaitHatchGuide;

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

    private void OnEnable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatched);
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.AddListener(HandleTabDisplayed);
            _inventoryWindow.OnSlotViewChanged.AddListener(Render);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_placementController != null)
        {
            _placementController.BuildingToPlaceChanged.AddListener(HandleBuildingToPlaceChanged);
        }

        // 더 높은 우선순위(1일차 튜토리얼)에 표시권을 양보한 동안에도 단계는 계속 전진한다.
        // 그쪽이 놓는 순간 옛 요청을 되살리는 게 아니라 지금 단계로 다시 유도해야 하므로 Render를 태운다.
        if (_overlay != null)
        {
            _overlay.DisplayReleased += Render;
            _overlay.ConfirmClicked += HandleConfirmClicked;
        }

        // 이 컴포넌트의 활성 체크박스가 곧 가이드 on/off 스위치다. 플레이 중 다시 켜면 현재 단계 안내를
        // 즉시 복구한다(최초 활성 시점엔 아직 단계가 없어 아무것도 안 뜨고, 실제 안내는 Start 이후에 나온다).
        Render();
    }

    private void OnDisable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatched);
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.RemoveListener(HandleTabDisplayed);
            _inventoryWindow.OnSlotViewChanged.RemoveListener(Render);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_placementController != null)
        {
            _placementController.BuildingToPlaceChanged.RemoveListener(HandleBuildingToPlaceChanged);
        }

        // 끄면 밤 잠금도 같이 풀어준다 - 안 그러면 영영 막힌 채로 남는다.
        if (_cycleManager != null)
        {
            _cycleManager.RemoveDayEndBlocker(this);
        }

        // 끄면 떠 있던 딤·말풍선도 같이 걷는다 - 안 그러면 화면에 그대로 남는다.
        // 구독을 먼저 끊어야 Release가 부르는 DisplayReleased가 방금 끈 이 컨트롤러를 다시 그리지 않는다.
        if (_overlay != null)
        {
            _overlay.DisplayReleased -= Render;
            _overlay.ConfirmClicked -= HandleConfirmClicked;
            _overlay.Release(this);
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
        Render();
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

        if (_currentStep.Value == BabyDragonGuideStep.Completed)
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

    private void HandleEggHatched(DragonType _) =>
        AdvanceAfterHatchToastAsync().Forget();

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

    /// <summary>
    /// 부화 결과를 확인하기 전에 배치 안내가 덮이지 않도록 부화 토스트만 사라질 때까지 기다린다.
    /// 알 획득은 다음 행동을 즉시 알려야 하므로 이 경로를 거치지 않는다.
    /// </summary>
    private async UniTaskVoid AdvanceAfterHatchToastAsync()
    {
        // 토스트 길이를 여기에 베껴 두면 토스트만 고쳤을 때 조용히 어긋나므로 토스트에서 직접 읽는다.
        float toastDuration = _toast == null ? 0f : _toast.TotalDuration;

        // 토스트가 일시정지 중에도 진행되므로(UI_NotificationToast의 SetUpdate(true)) 여기도 실시간으로 센다.
        await UniTask.WaitForSeconds(
            toastDuration + _guideStartDelay,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        Advance(BabyDragonGuideStep.PlaceDragon);
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

        _currentStep = step;

        if (step == BabyDragonGuideStep.Completed)
        {
            AnnounceCompletion();
        }

        Render();
        StepEntered.Invoke(step);
    }

    // 완료 안내도 오버레이로 낸다 - 강제 안내인데 토스트로 흘려보내면 말풍선과 겹쳐 둘 다 읽히지 않는다.
    // 가리킬 대상이 없으므로 딤 없이 말풍선만 띄우고, 확인 버튼으로 두 컷을 순서대로 넘긴다.
    private void AnnounceCompletion()
    {
        _completionIndex = 0;
    }

    private void HandleConfirmClicked()
    {
        if (_currentStep != BabyDragonGuideStep.Completed || _completionIndex >= COMPLETION_LOC_KEYS.Length)
        {
            return;
        }

        // 확인 버튼은 오버레이가 공용이라 튜토리얼 러너의 클릭도 여기로 온다. 이 가이드는 우선순위가 낮아
        // 양보하고 있을 때가 있는데, 그때 남의 클릭을 받으면 뜨지도 않은 완료 문구가 넘어가 버린다.
        if (_overlay != null && !_overlay.IsDisplaying(this))
        {
            return;
        }

        _completionIndex++;
        Render();
    }

    /// <summary>
    /// 밤 잠금을 지금 단계에 맞춘다. 등록은 여럿이 함께 걸 수 있으므로 남이 건 것과 다투지 않는다 -
    /// 하나라도 막고 있으면 밤으로 넘어가지 않는다.
    /// </summary>
    private void UpdateDayEndGate()
    {
        if (_cycleManager == null)
        {
            return;
        }

        if (BlocksDayEnd)
        {
            _cycleManager.AddDayEndBlocker(this);
            return;
        }

        _cycleManager.RemoveDayEndBlocker(this);
    }

    private void Render()
    {
        // 표시권을 못 잡아 아무것도 그리지 못하는 동안에도 잠금은 단계를 따라가야 한다.
        UpdateDayEndGate();

        if (_overlay == null)
        {
            return;
        }

        if (!_currentStep.HasValue)
        {
            _overlay.Release(this);
            return;
        }

        switch (_currentStep.Value)
        {
            case BabyDragonGuideStep.OpenInventory:
                // 유일하게 입력을 막는 단계 - 버튼 한 번 누르면 끝나는 행동이라 막아도 갇히지 않는다.
                ShowGuide(_inventoryButton, OPEN_INVENTORY_LOC_KEY, blocksInput: true,
                    dimsBackground: true, showConfirmButton: false, ResolveToggleKeyLabel());
                break;

            case BabyDragonGuideStep.WaitHatch:
                // 창을 닫은 건 시킨 대로 한 것이다 - 다시 열라고 하면 안내가 제자리를 돈다.
                if (!IsInventoryOpen)
                {
                    // 안내를 본 뒤 창을 닫았다면 이 단계에서 가르칠 것은 끝났다.
                    // 부화까지 며칠이 걸리는 동안 창을 열 때마다 같은 말을 다시 띄우면,
                    // 이미 읽은 안내가 지워지지 않고 계속 따라다닌다.
                    _hasFinishedWaitHatchGuide |= _hasShownWaitHatchGuide;
                    _overlay.Release(this);
                    break;
                }

                if (_hasFinishedWaitHatchGuide)
                {
                    _overlay.Release(this);
                    break;
                }

                _hasShownWaitHatchGuide = true;

                // 알 슬롯은 눌러도 반응이 없으므로 "창을 닫고 하루를 보내라"까지 같이 알려준다.
                // 닫기는 창의 X 버튼으로 한다 - 토글 키로 닫는 경로가 없어서 키 이름을 알려주면 헛짚는다.
                // 여기서는 배경을 어둡게 하지 않는다. X는 누구나 아는 표시라 가려서 몰아갈 이유가 없고,
                // 알 슬롯 테두리만으로 "이게 네 알이다"는 충분히 전달된다.
                ShowSlotGuide(wantDragonSlot: false, WAIT_HATCH_LOC_KEY, dimsBackground: false);
                break;

            case BabyDragonGuideStep.PlaceDragon:
                // 새끼용을 이미 골랐다면 남은 일은 타일을 찍는 것뿐이다 - 가리킬 대상 없이 말풍선만 띄워
                // 딤을 걷는다. 그리드를 덮은 채로 "타일을 클릭하라"고 하면 어디를 눌러야 할지 가려진다.
                if (IsPlacingBabyDragon)
                {
                    ShowGuide(null, PLACE_DRAGON_TILE_LOC_KEY, blocksInput: false);
                    break;
                }

                // 부화는 WaitHatch에서 창을 닫아둔 상태로 맞이하므로, 여기서 다시 열도록 HUD 버튼을 강조한다.
                // 이미 하루를 굴려본 시점이라 입력까지 막지는 않는다.
                if (!IsInventoryOpen)
                {
                    ShowGuide(_inventoryButton, REOPEN_INVENTORY_LOC_KEY, blocksInput: false,
                        dimsBackground: true, showConfirmButton: false, ResolveToggleKeyLabel());
                    break;
                }

                // 배치는 슬롯의 위치 버튼을 누른 뒤 그리드를 눌러야 끝나므로 화면을 막으면 진행이 막힌다.
                ShowSlotGuide(wantDragonSlot: true, PLACE_DRAGON_LOC_KEY, dimsBackground: true);
                break;

            case BabyDragonGuideStep.Completed:
                // 가리킬 대상이 없다 - 딤 없이 말풍선만 띄우고 확인 버튼으로 넘긴다.
                if (_completionIndex < COMPLETION_LOC_KEYS.Length)
                {
                    ShowGuide(null, COMPLETION_LOC_KEYS[_completionIndex],
                        blocksInput: false, dimsBackground: false, showConfirmButton: true);
                    break;
                }

                _overlay.Release(this);
                break;

            default:
                _overlay.Release(this);
                break;
        }
    }

    // 표시권을 못 잡으면(1일차 튜토리얼이 화면을 쓰는 중) 그냥 넘어간다 - 단계는 이미 전진해 있고,
    // 튜토리얼이 놓을 때 DisplayReleased로 Render가 다시 돌아 그 시점의 단계부터 유도한다.
    private void ShowGuide(RectTransform target, string locKey, bool blocksInput,
        bool dimsBackground = true, bool showConfirmButton = false, params object[] args)
    {
        // 용을 배치할 때만 그리드를 가리지 않게 말풍선을 아래로 내린다.
        GuideBubbleSlot slot = locKey == PLACE_DRAGON_TILE_LOC_KEY
            ? GuideBubbleSlot.Bottom
            : GuideBubbleSlot.Default;

        // 새끼용 안내는 전부 눌러보게 하는 단계라 대상을 막지 않는다.
        _overlay.Show(this, GuidePriority.BABY_DRAGON_GUIDE, target, locKey, blocksInput,
            blocksTargetInteraction: false, showConfirmButton, slot, dimsBackground, args);
    }

    // 참조가 비어 있어도 문구 자체는 떠야 하므로 키 이름만 빈 문자열로 대체한다.
    private string ResolveToggleKeyLabel()
    {
        return _inventoryToggleAction == null || _inventoryToggleAction.action == null
            ? string.Empty
            : _inventoryToggleAction.action.GetBindingDisplayString();
    }

    private bool IsInventoryOpen => _inventoryWindow != null && _inventoryWindow.IsOpen;

    // 새끼용 고스트가 커서를 따라다니는 중. ESC로 창을 닫아도 고스트는 살아있으므로
    // (용 창은 창만 닫고 배치를 취소하지 않는다) 창 열림 여부보다 이 판정이 먼저다.
    private bool IsPlacingBabyDragon =>
        _placementController != null && _placementController.BuildingToPlace is BabyDragonTower;

    private void HandleBuildingToPlaceChanged(Building _) => Render();

    // 창이 열려 있는 동안 그 안의 대상을 강조한다. 창이 닫힌 동안 무엇을 할지는 단계마다 다르므로
    // 여기서 정하지 않고 호출부(Render)가 미리 걸러낸다.
    //
    // 알 탭과 용 탭이 따로였을 때는 "원하는 탭으로 바꾸게 한 뒤 그 탭의 첫 슬롯"이었다.
    // 지금은 둘이 새끼용 탭 한 패널에 함께 있으므로, 탭 유도는 한 번뿐이고 그 뒤에는
    // 어느 목록의 슬롯을 가리킬지 단계가 직접 고른다.
    private void ShowSlotGuide(bool wantDragonSlot, string slotLocKey, bool dimsBackground)
    {
        if (!IsInventoryOpen)
        {
            _overlay.Release(this);
            return;
        }

        // 막지는 않는다 - 알 슬롯은 눌러도 반응이 없고(SetupEgg에서 interactable=false),
        // 새끼용 쪽은 버튼을 누른 뒤 그리드까지 눌러야 배치가 끝나므로 막으면 진행 자체가 불가능해진다.
        if (!_inventoryWindow.IsBabyTabShown)
        {
            ShowGuide(_inventoryWindow.BabyTabRect, SWITCH_TAB_LOC_KEY, blocksInput: false, dimsBackground);
            return;
        }

        // 새끼용은 슬롯 전체가 아니라 슬롯 안의 위치 표시 버튼을 눌러야 배치가 시작된다 -
        // 슬롯을 통째로 가리키면 어디를 눌러야 하는지 알 수 없다(폐기한 27단계 챕터의 day2_select_dragon이
        // 가리키던 대상이 이 버튼이다). 아직 안 그려졌으면 슬롯으로 물러난다.
        bool hasSlot = wantDragonSlot
            ? _inventoryWindow.TryGetFirstBabyDragonFocusButtonRect(out RectTransform slotRect) ||
              _inventoryWindow.TryGetFirstBabyDragonSlotRect(out slotRect)
            : _inventoryWindow.TryGetFirstEggSlotRect(out slotRect);

        if (hasSlot)
        {
            ShowGuide(slotRect, slotLocKey, blocksInput: false, dimsBackground);
            return;
        }

        _overlay.Release(this);
    }
}
