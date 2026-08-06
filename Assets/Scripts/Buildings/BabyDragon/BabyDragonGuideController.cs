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
public class BabyDragonGuideController : MonoBehaviour
{
    private const string OPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_open_inventory";
    private const string REOPEN_INVENTORY_LOC_KEY = "baby_dragon_guide_reopen_inventory";
    private const string SWITCH_TAB_LOC_KEY = "baby_dragon_guide_switch_tab";
    private const string WAIT_HATCH_LOC_KEY = "baby_dragon_guide_wait_hatch";
    private const string PLACE_DRAGON_LOC_KEY = "baby_dragon_guide_place_dragon";
    private const string PLACE_DRAGON_TILE_LOC_KEY = "baby_dragon_guide_place_dragon_tile";
    private const string COMPLETED_LOC_KEY = "baby_dragon_guide_completed";
    private const string MANAGE_HINT_LOC_KEY = "baby_dragon_guide_manage_hint";

    private const float DEFAULT_GUIDE_START_DELAY = 0.5f;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private UI_DragonInventoryWindow _inventoryWindow;
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새끼용을 골라 배치 대기 상태가 됐는지 판정하는 데 쓴다. 골랐으면 딤을 걷어 그리드를 그대로 보여준다.")]
    [SerializeField] private BuildingPlacementController _placementController;

    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("안내가 끝났을 때 남길 문구를 띄운다. 획득 토스트와 같은 오브젝트를 써도 된다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("HUD의 새끼용 인벤토리 버튼. OpenInventory 단계에서 이것만 남기고 화면을 덮는다.")]
    [SerializeField] private RectTransform _inventoryButton;

    [Tooltip("인벤토리 토글 액션. 안내 문구에 실제 키 이름을 넣는 데만 쓴다 - 바인딩을 바꾸면 문구도 따라간다.")]
    [SerializeField] private InputActionReference _inventoryToggleAction;

    [Tooltip("알 획득·부화 토스트가 사라진 뒤 안내를 시작하기까지의 추가 여유(초). " +
             "토스트 자체의 길이는 토스트에서 읽어오므로 여기에 포함하지 않는다.")]
    [SerializeField] private float _guideStartDelay = DEFAULT_GUIDE_START_DELAY;

    /// <summary>
    /// 새 단계에 들어섰다. 건너뛴 중간 단계까지 한 번에 지나갈 수 있으므로 인자는 "도달한 단계"다 -
    /// 특정 지점을 기다리는 쪽은 == 이 아니라 >= 로 판정해야 한다.
    /// </summary>
    public UnityEvent<BabyDragonGuideStep> StepEntered = new();

    // 아직 시작 전이면 값이 없다 - 첫 알을 얻는 순간 OpenInventory로 들어간다.
    private BabyDragonGuideStep? _currentStep;

    private RunData CurrentRun => _gameManager == null ? null : _gameManager.CurrentRun;

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

        // 끄면 떠 있던 딤·말풍선도 같이 걷는다 - 안 그러면 화면에 그대로 남는다.
        // 구독을 먼저 끊어야 Release가 부르는 DisplayReleased가 방금 끈 이 컨트롤러를 다시 그리지 않는다.
        if (_overlay != null)
        {
            _overlay.DisplayReleased -= Render;
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
    }

    private void HandleEggGranted(DragonType _) =>
        AdvanceAfterToastAsync(BabyDragonGuideStep.OpenInventory).Forget();

    private void HandleEggHatched(DragonType _) =>
        AdvanceAfterToastAsync(BabyDragonGuideStep.PlaceDragon).Forget();

    // 사용자가 방금 누른 결과라 즉시 반응해야 한다 - 지연시키면 조작이 먹지 않은 것처럼 보인다.
    private void HandleTabDisplayed(bool _) => Advance(BabyDragonGuideStep.WaitHatch);

    /// <summary>
    /// 알 획득·부화는 토스트가 먼저 뜨는 이벤트다. 둘이 겹치면 어느 쪽을 봐야 할지 알 수 없으므로
    /// 토스트가 사라질 때까지 기다렸다가 안내를 시작한다.
    /// 기다리는 동안 플레이어가 먼저 인벤토리를 열어도 Advance가 단조라 늦게 도착한 호출은 무시된다.
    /// </summary>
    private async UniTaskVoid AdvanceAfterToastAsync(BabyDragonGuideStep step)
    {
        // 토스트 길이를 여기에 베껴 두면 토스트만 고쳤을 때 조용히 어긋나므로 토스트에서 직접 읽는다.
        float toastDuration = _toast == null ? 0f : _toast.TotalDuration;

        // 토스트가 일시정지 중에도 진행되므로(UI_NotificationToast의 SetUpdate(true)) 여기도 실시간으로 센다.
        await UniTask.WaitForSeconds(
            toastDuration + _guideStartDelay,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        Advance(step);
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

    private void AnnounceCompletion()
    {
        if (_toast == null)
        {
            return;
        }

        // 가리킬 대상이 없는 안내라 화살표가 아니라 토스트로 보낸다. 토스트가 큐를 갖고 있어 순차로 뜬다.
        _toast.Show(COMPLETED_LOC_KEY);
        _toast.Show(MANAGE_HINT_LOC_KEY);
    }

    private void Render()
    {
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
                ShowGuide(_inventoryButton, OPEN_INVENTORY_LOC_KEY, blocksInput: true, ResolveToggleKeyLabel());
                break;

            case BabyDragonGuideStep.WaitHatch:
                // 창을 닫은 건 시킨 대로 한 것이다 - 다시 열라고 하면 안내가 제자리를 돈다.
                if (!IsInventoryOpen)
                {
                    _overlay.Release(this);
                    break;
                }

                // 알 슬롯은 눌러도 반응이 없으므로 "창을 닫고 하루를 보내라"까지 같이 알려준다.
                ShowSlotGuide(wantDragonTab: false, WAIT_HATCH_LOC_KEY, ResolveToggleKeyLabel());
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
                    ShowGuide(_inventoryButton, REOPEN_INVENTORY_LOC_KEY, blocksInput: false, ResolveToggleKeyLabel());
                    break;
                }

                // 배치는 슬롯을 누른 뒤 그리드를 눌러야 끝나므로 화면을 막으면 배치 자체가 불가능해진다.
                ShowSlotGuide(wantDragonTab: true, PLACE_DRAGON_LOC_KEY);
                break;

            default:
                _overlay.Release(this);
                break;
        }
    }

    // 표시권을 못 잡으면(1일차 튜토리얼이 화면을 쓰는 중) 그냥 넘어간다 - 단계는 이미 전진해 있고,
    // 튜토리얼이 놓을 때 DisplayReleased로 Render가 다시 돌아 그 시점의 단계부터 유도한다.
    private void ShowGuide(RectTransform target, string locKey, bool blocksInput, params object[] args)
    {
        // 새끼용 안내는 전부 행동형이라 확인 버튼을 쓰지 않는다.
        // 용을 배치할 때만 그리드를 가리지 않게 말풍선을 아래로 내린다.
        GuideBubbleSlot slot = locKey == PLACE_DRAGON_TILE_LOC_KEY
            ? GuideBubbleSlot.Bottom
            : GuideBubbleSlot.Default;

        // 새끼용 안내는 전부 눌러보게 하는 단계라 대상을 막지 않는다.
        _overlay.Show(this, GuidePriority.BABY_DRAGON_GUIDE, target, locKey, blocksInput,
            blocksTargetInteraction: false, showConfirmButton: false, slot, args);
    }

    // 참조가 비어 있어도 문구 자체는 떠야 하므로 키 이름만 빈 문자열로 대체한다.
    private string ResolveToggleKeyLabel()
    {
        return _inventoryToggleAction == null || _inventoryToggleAction.action == null
            ? string.Empty
            : _inventoryToggleAction.action.GetBindingDisplayString();
    }

    private bool IsInventoryOpen => _inventoryWindow != null && _inventoryWindow.IsOpen;

    // 새끼용 고스트가 커서를 따라다니는 중. ESC로 인벤토리를 닫아도 고스트는 살아있으므로
    // (UI_DragonInventoryWindow는 창만 닫고 배치를 취소하지 않는다) 창 열림 여부보다 이 판정이 먼저다.
    private bool IsPlacingBabyDragon =>
        _placementController != null && _placementController.BuildingToPlace is BabyDragonTower;

    private void HandleBuildingToPlaceChanged(Building _) => Render();

    // 창이 열려 있는 동안 그 안의 대상을 강조한다. 창이 닫힌 동안 무엇을 할지는 단계마다 다르므로
    // 여기서 정하지 않고 호출부(Render)가 미리 걸러낸다.
    private void ShowSlotGuide(bool wantDragonTab, string slotLocKey, params object[] slotArgs)
    {
        if (!IsInventoryOpen)
        {
            _overlay.Release(this);
            return;
        }

        // 딤은 켜되 막지는 않는다 - 알 슬롯은 눌러도 반응이 없고(SetupEgg에서 interactable=false),
        // 용 슬롯은 누른 뒤 그리드까지 눌러야 배치가 끝나므로 막으면 진행 자체가 불가능해진다.
        if (_inventoryWindow.IsDragonTabShown != wantDragonTab)
        {
            RectTransform tabRect = wantDragonTab ? _inventoryWindow.DragonTabRect : _inventoryWindow.EggTabRect;
            ShowGuide(tabRect, SWITCH_TAB_LOC_KEY, blocksInput: false);
            return;
        }

        if (_inventoryWindow.TryGetFirstSlotRect(out RectTransform slotRect))
        {
            ShowGuide(slotRect, slotLocKey, blocksInput: false, slotArgs);
            return;
        }

        _overlay.Release(this);
    }
}
