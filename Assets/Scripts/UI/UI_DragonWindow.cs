using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 용 창 - 어미용/새끼용 탭을 전환하고, 좌측 프레임(Left_Panel_frame)에 현재 어미용의
// 아이콘과 속성명을 표시한다. IExclusiveMode(UIManager.OpenExclusive) 조정을 겸한다.
//
// 닫을 때 이 오브젝트가 아니라 자식 _windowRoot만 끈다 - 스크립트 호스트가 계속 활성이어야
// Update가 돌아 성(Castle) 클릭을 감지할 수 있기 때문이다.
// UI_MainCastleWindow / UI_PopulationAllocationWindow / UI_BabyDragonManageWindow와 같은 구조다
// (HUD 버튼으로만 여는 UI_ResearchWindow 계열은 반대로 자기 자신을 끈다).
// 속성 변경 규칙·속성 색·아이콘 원본은 여기서 재구현하지 않는다
// - DragonTreeManager / DragonAttributePalette / BabyDragonDataCatalog에 위임한다.
// Button_change는 UI_DragonChangePopup을 열기만 하고, 실제 변경 로직은 그 팝업이 갖고 있다.
public class UI_DragonWindow : MonoBehaviour, IExclusiveMode
{
    // 탭 - Panel_MotherDragon / Panel_BabyDragon 중 하나만 활성이 된다.
    private enum DragonTab
    {
        Mother,
        Baby,
    }

    // 탭 버튼 하나의 시각 요소 묶음(런타임 전달용 - 직렬화 대상이 아니다).
    // 선택되면 SelectFocus만 켜고 아이콘·라벨을 포커스 색으로, 아니면 SelectDefault만 켜고 노멀 색으로 만든다.
    // UI_BuildModeWindow.FilterTab / UI_DragonInventoryWindow.FilterTab과 같은 패턴이지만,
    // 이 프리팹은 Focus/Normal 컨테이너가 버튼마다 하나뿐이라 GameObject를 복제하지 않고 색만 바꾼다.
    // 인스펙터에는 구조체가 아니라 아래 5개씩의 평면 필드로 노출한다
    // (에디터 툴링이 중첩 구조체 필드를 배선하지 못해 평면화했다).
    private struct DragonTabView
    {
        public Button Button;
        public GameObject SelectFocus;
        public GameObject SelectDefault; // 프리팹 오브젝트 이름 오타(Select_Defalt) 그대로 유지
        public Image Icon;
        public TextMeshProUGUI Label;
    }

    [Header("Dependencies")]
    [Tooltip("현재 어미용 속성의 조회·변경 알림 출처. 없으면 좌측 프레임을 비워둔다.")]
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [Tooltip("배타 UI 조정용. 없으면 다른 창을 닫지 않고 이 창만 연다.")]
    [SerializeField] private UIManager _uiManager;

    [Header("Window")]
    [Tooltip("창 콘텐츠 전체를 담은 자식(Content). 닫을 때 이것만 끈다 - " +
        "이 스크립트가 붙은 오브젝트는 계속 활성이어야 성 클릭 구독이 살아 있어 창을 다시 열 수 있다.")]
    [SerializeField] private GameObject _windowRoot;
    [Tooltip("성(Castle) 클릭 감지용. 없으면 성을 눌러도 창이 열리지 않는다.")]
    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private Button _exitButton;
    [Tooltip("창 바깥 클릭 닫기용 전용 blocker Button. 반드시 창 콘텐츠와 별개의(자손이 아닌) 오브젝트여야 한다 - " +
        "콘텐츠의 조상에 붙은 Button을 넣으면 EventSystem이 자손 클릭을 부모로 버블링시켜 창 안을 클릭할 때마다 닫힌다. " +
        "현재 이 프리팹에는 전용 blocker가 없어 비워 둔다.")]
    [SerializeField] private Button _blockerButton;
    [Tooltip("InputActionReference - 보통 ESC.")]
    [SerializeField] private InputActionReference _closeAction;

    [Header("Tabs")]
    [SerializeField] private GameObject _motherPanel;
    [SerializeField] private GameObject _babyPanel;

    [Header("Tab - Mother (TabMenu/Button_MotherDragon)")]
    [SerializeField] private Button _motherTabButton;
    [SerializeField] private GameObject _motherTabSelectFocus;
    [SerializeField] private GameObject _motherTabSelectDefault;
    [SerializeField] private Image _motherTabIcon;
    [SerializeField] private TextMeshProUGUI _motherTabLabel;

    [Header("Tab - Baby (TabMenu/Button_BabyDragon)")]
    [SerializeField] private Button _babyTabButton;
    [SerializeField] private GameObject _babyTabSelectFocus;
    [SerializeField] private GameObject _babyTabSelectDefault;
    [SerializeField] private Image _babyTabIcon;
    [SerializeField] private TextMeshProUGUI _babyTabLabel;

    [Header("Tab Colors (프리팹 실측값 - 포커스/노멀)")]
    [SerializeField] private Color _tabIconFocusColor = Color.white;
    [SerializeField] private Color _tabIconNormalColor = new Color(0.7264151f, 0.7264151f, 0.7264151f, 1f);
    [SerializeField] private Color _tabLabelFocusColor = new Color(0.9647059f, 0.88235295f, 0.6117647f, 1f);
    [SerializeField] private Color _tabLabelNormalColor = new Color(0.74509805f, 0.70980394f, 0.7137255f, 1f);

    [Header("Mother Dragon (Left_Panel_frame)")]
    [Tooltip("Icon_dragon/Image_mask/Icon_dragon 의 Image.")]
    [SerializeField] private Image _motherIcon;
    [Tooltip("Panel_name/Text (TMP). 어미용에는 고유 이름이 없어 속성명을 표시한다.")]
    [SerializeField] private TextMeshProUGUI _motherNameText;
    [Tooltip("Panel_Info/Text (TMP). 현재 속성에 대한 설명 문구를 표시한다.")]
    [SerializeField] private TextMeshProUGUI _motherInfoText;
    [Tooltip("어미용 속성별 스프라이트 - DragonType 선언 순서(Ice, Fire, Time, Stone, Life). 빈 칸은 새끼용 카탈로그 스프라이트로 대체한다.")]
    [SerializeField] private Sprite[] _motherSprites;
    [Tooltip("_motherSprites 칸이 비었을 때 대신 쓸 새끼용 스프라이트 카탈로그. 새끼용/알 리스트의 데이터 조회에도 쓴다.")]
    [SerializeField] private BabyDragonDataCatalog _babyDragonDataCatalog;

    [Tooltip("Left_Panel_frame/Icon_dragon/Button_change - 속성 변경 팝업을 연다.")]
    [SerializeField] private Button _changeButton;

    [Tooltip("Popup_Dragon_Change - 실제 변경 로직은 이 팝업이 갖고 있다.")]
    [SerializeField] private UI_DragonChangePopup _changePopup;

    [Tooltip("속성 변경이 하루 1회 제한에 걸렸을 때 토스트를 띄운다. 없으면 조용히 무시한다.")]
    [SerializeField] private UI_WarningWindow _warningWindow;

    [Header("Baby Dragon / Egg 리스트 (Panel_BabyDragon)")]
    [Tooltip("Panel_Right/Panel_DragonInfo/Text (TMP). 마우스를 올린 새끼용 슬롯의 속성 설명을 표시하고, " +
        "아무 슬롯에도 올라가 있지 않으면 비운다.")]
    [SerializeField] private TextMeshProUGUI _babyInfoText;

    [Tooltip("보유 새끼용·알 목록의 출처(RunData). 없으면 두 리스트를 비운다.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("미배치 새끼용의 그리드 배치를 시작한다. 없으면 배치 클릭이 무시된다.")]
    [SerializeField] private BabyDragonPlacementCoordinator _placementCoordinator;

    [Tooltip("배치된 새끼용으로 카메라를 옮긴다. 없으면 포커스 클릭이 무시된다.")]
    [SerializeField] private CameraController _cameraController;

    [Tooltip("배치된 새끼용 인스턴스를 찾는 데 쓴다(GridMap.Buildings).")]
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새끼용 슬롯 프리팹(Slot_BabyDragon_List).")]
    [FormerlySerializedAs("_babyDragonSlotTemplate")]
    [SerializeField] private UI_BabyDragonListSlot _babyDragonSlotPrefab;

    [Tooltip("새끼용 슬롯이 들어갈 부모 - Scroll View_BabyDragonList/Viewport/Content.")]
    [SerializeField] private Transform _babyDragonSlotContainer;

    [Tooltip("알 슬롯 프리팹(Slot_Egg_List).")]
    [FormerlySerializedAs("_eggSlotTemplate")]
    [SerializeField] private UI_EggListSlot _eggSlotPrefab;

    [Tooltip("알 슬롯이 들어갈 부모 - Scroll View_EggInventory/Viewport/Content.")]
    [SerializeField] private Transform _eggSlotContainer;

    // --- 안내(튜토리얼·새끼용 가이드)용 관측 지점 ---
    // 이 창은 안내를 모른다. 무엇이 그려졌는지만 알리고, 무엇을 가리킬지는 듣는 쪽이 정한다.
    // 통합 전 UI_DragonInventoryWindow가 갖고 있던 것과 같은 이름·같은 의미다.

    [Tooltip("새끼용 탭이 보이게 됐는지(true) 어미용 탭인지(false). 탭을 바꿀 때마다 발화한다.")]
    public UnityEvent<bool> OnTabDisplayed = new();

    [Tooltip("알·새끼용 슬롯을 다시 그렸다. 슬롯은 런타임 생성이라 안내가 이걸 듣고 다시 조준한다.")]
    public UnityEvent OnSlotViewChanged = new();

    public bool IsBabyTabShown => _isOpen && _currentTab == DragonTab.Baby;

    public bool IsOpen => _isOpen;

    /// <summary>새끼용 탭 버튼. 알·새끼용 목록이 이 탭 안에 함께 있다.</summary>
    public RectTransform BabyTabRect =>
        _babyTabButton == null ? null : (RectTransform)_babyTabButton.transform;

    private DragonTab _currentTab = DragonTab.Mother;
    private bool _isOpen;

    // 마우스가 올라가 있는 새끼용 슬롯의 개체. Panel_DragonInfo에 띄울 설명을 결정한다.
    private BabyDragon _hoveredBabyDragon;

    // 미리 배치된 슬롯을 0번으로 물려받는 풀 - 그래야 디자인 타임 미리보기가 남으면서
    // 런타임에 쓰이지 않는 유령 슬롯이 생기지 않는다.
    private ComponentPool<UI_BabyDragonListSlot> _babyDragonSlotPool;
    private ComponentPool<UI_EggListSlot> _eggSlotPool;

    private void Awake()
    {
        if (_exitButton != null)
        {
            _exitButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        if (_motherTabButton != null)
        {
            _motherTabButton.onClick.AddListener(() => SelectTab(DragonTab.Mother));
        }

        if (_babyTabButton != null)
        {
            _babyTabButton.onClick.AddListener(() => SelectTab(DragonTab.Baby));
        }

        if (_changeButton != null && _changePopup != null)
        {
            _changeButton.onClick.AddListener(ToggleChangePopup);
        }

        CreateSlotPools();

        // 프리팹에는 Select_Focus와 Select_Defalt가 두 버튼 모두 켜져 있다(실측) -
        // 이 호출이 그 상태를 정상화하는 유일한 지점이므로 Awake에서 한 번 확정한다.
        SelectTab(_currentTab);

        // 시작 시 콘텐츠만 끈다. 스크립트 호스트는 계속 활성이라 Update가 돌고,
        // 그래야 창이 닫혀 있는 동안에도 성 클릭을 감지할 수 있다.
        //
        // _isOpen 가드는 방어용이다 - 인스턴스가 비활성으로 저장돼 Awake가 첫 Open()의
        // SetActive(true) 안에서 실행되는 경우, 무조건 닫으면 방금 연 창을 스스로 닫아버린다
        // (CLAUDE.md 이벤트 초기화 규칙 참고).
        if (!_isOpen)
        {
            Close();
        }
    }

    // 성을 클릭할 때마다 열린다. SelectedBuilding 폴링이 아니라 이벤트를 쓰는 이유는, 창을 닫은 뒤
    // 성이 계속 선택된 상태에서 성을 다시 눌러도 열려야 하기 때문이다 - 그 경우 SelectedBuilding은
    // 계속 Castle이라 폴링으로는 변화가 관측되지 않는다.
    private void HandleBuildingSelected(Building building)
    {
        if (building is not Castle)
        {
            return;
        }

        // 점령·건설 모드 등이 클릭을 점유한 동안에는 그쪽 창이 같은 자리를 쓰므로 열지 않는다.
        if (_buildingPlacementController.InputSuppressed)
        {
            return;
        }

        OpenAtMotherTab();
    }

    private void OnEnable()
    {
        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.BuildingSelected.AddListener(HandleBuildingSelected);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleActiveAttributeChanged);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }

        // 알 획득·부화·배치·철거를 반영한다. 창이 닫혀 있는 동안은 구독하지 않아도
        // 다시 열릴 때 아래 RebuildLists()가 현재 상태로 다시 그린다.
        RunData run = CurrentRun;
        if (run != null)
        {
            run.OnInventoryChanged.AddListener(RebuildLists);
        }

        // ActiveAttributeChanged는 런 생성 시 발화되지 않는다(NotifyActiveAttributeChanged /
        // HandleDayStart에서만 발화) - 구독 직후 한 번 수동으로 현재 값을 반영한다.
        RenderMotherDragon();
        RebuildLists();
    }

    private void OnDisable()
    {
        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.BuildingSelected.RemoveListener(HandleBuildingSelected);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleActiveAttributeChanged);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }

        RunData run = CurrentRun;
        if (run != null)
        {
            run.OnInventoryChanged.RemoveListener(RebuildLists);
        }
    }

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        // _isOpen 가드가 필요한 이유: 스크립트 호스트가 항상 활성이라 이 구독은 씬 로드부터 계속 유지된다
        // (창이 열려 있는 동안만 구독하던 예전 구조와 다르다). 가드가 없으면 창이 닫혀 있을 때 누른 ESC도
        // Close()를 호출한다 - 지금은 무해하지만 Close()에 연출이 붙으면 문제가 된다.
        if (!_isOpen)
        {
            return;
        }

        Close();
    }

    private void HandleActiveAttributeChanged(DragonType attribute)
    {
        RenderMotherDragon();
    }

    public void Open()
    {
        _isOpen = true;

        // 스크립트 호스트가 비활성으로 저장돼 있으면 되살린다 - 그래야 Update가 돌아 성 클릭을 받는다
        // (UI_BuildModeWindow.OpenBuildPanel과 같은 방어).
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(true);
        }

        // OnEnable은 이제 씬 로드 때 한 번만 돌므로(호스트가 늘 활성), 열 때마다 여기서 직접 갱신한다.
        SelectTab(_currentTab);
        RenderMotherDragon();
        RebuildLists();
    }

    public void Close()
    {
        _isOpen = false;

        // 팝업은 _windowRoot의 자손이라 창을 끄면 같이 사라지지만, activeSelf는 true로 남는다 -
        // 그대로 두면 창을 다시 열었을 때 팝업이 딸려 나온다. 명시적으로 닫아 다음 열기를 깨끗하게 만든다.
        if (_changePopup != null)
        {
            _changePopup.Close();
        }

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(false);
        }
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    // 인게임 HUD(UI_IngameWindow)의 용 버튼이 호출하는 진입점
    // - UI_ResearchWindow/UI_DragonSkillWindow.ToggleFromEntryPoint와 동일한 패턴.
    public void ToggleFromEntryPoint()
    {
        if (_isOpen)
        {
            Close();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            Open();
        }
    }

    // 성(Castle) 클릭처럼 "어미용 화면을 보여달라"는 진입점.
    // ToggleFromEntryPoint와 달리 토글하지 않는다 - 이미 열려 있는데 닫아버리면
    // 성을 클릭했는데 창이 사라지는 셈이라, 그때는 탭만 어미용으로 맞춘다.
    public void OpenAtMotherTab()
    {
        SelectTab(DragonTab.Mother);

        if (_isOpen)
        {
            return;
        }

        if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            Open();
        }
    }

    // 선택된 탭의 패널만 켜고, 두 탭 버튼의 시각 상태를 갱신한다.
    // 탭 선택은 창을 닫아도 유지된다(다시 열면 마지막 탭이 보인다).
    private void SelectTab(DragonTab tab)
    {
        _currentTab = tab;
        bool isMother = tab == DragonTab.Mother;

        // 속성 변경 팝업은 Content 바로 밑(패널 바깥)이라 탭을 바꿔도 스스로 사라지지 않는다 -
        // 새끼용 탭 위에 어미용 팝업이 남지 않도록 여기서 닫는다.
        if (_changePopup != null)
        {
            _changePopup.Close();
        }

        if (_motherPanel != null)
        {
            _motherPanel.SetActive(isMother);
        }

        if (_babyPanel != null)
        {
            _babyPanel.SetActive(!isMother);
        }

        ApplyTabVisual(MotherTabView, isMother);
        ApplyTabVisual(BabyTabView, !isMother);

        if (isMother)
        {
            RenderMotherDragon();
        }
        else
        {
            // 탭을 막 열었으면 마우스는 아직 어떤 슬롯 위에도 없다 - 이전 호버 흔적을 지운다.
            // (RunData가 없어 RebuildLists가 조기 반환하는 경우까지 덮으려면 여기서 비워야 한다.)
            _hoveredBabyDragon = null;
            RenderBabyInfo();
            RebuildLists();
        }

        // 그리기가 끝난 뒤에 알린다 - 듣는 쪽(안내)이 곧바로 슬롯을 조회하기 때문이다.
        OnTabDisplayed?.Invoke(!isMother);
    }

    /// <summary>
    /// 새끼용 탭에 그려진 첫 알 슬롯. 슬롯은 런타임 생성이라 GuideAnchor로는 가리킬 수 없어
    /// 창이 직접 돌려준다(UI_BuildModeWindow.TryGetSlotRect와 같은 방식).
    /// </summary>
    public bool TryGetFirstEggSlotRect(out RectTransform slotRect) =>
        TryGetFirstActiveChildRect(_eggSlotContainer, out slotRect);

    /// <summary>새끼용 탭에 그려진 첫 새끼용 슬롯.</summary>
    public bool TryGetFirstBabyDragonSlotRect(out RectTransform slotRect) =>
        TryGetFirstActiveChildRect(_babyDragonSlotContainer, out slotRect);

    // 풀은 쓰지 않은 슬롯을 비활성으로 남겨두므로, 활성인 것 중 첫 번째를 골라야 한다.
    // activeSelf가 아니라 activeInHierarchy를 보는 이유: 어미용 탭일 때 Panel_BabyDragon이 꺼져도
    // 그 안의 슬롯은 activeSelf가 켜진 채다. 그대로 돌려주면 안내가 보이지도 않는 자리에 구멍을 뚫는다.
    private static bool TryGetFirstActiveChildRect(Transform container, out RectTransform slotRect)
    {
        if (container != null)
        {
            foreach (Transform child in container)
            {
                if (child.gameObject.activeInHierarchy && child is RectTransform rect)
                {
                    slotRect = rect;
                    return true;
                }
            }
        }

        slotRect = null;
        return false;
    }

    // 평면 필드를 ApplyTabVisual이 다루기 쉬운 묶음으로 모은다(직렬화 대상은 아니다).
    private DragonTabView MotherTabView => new DragonTabView
    {
        Button = _motherTabButton,
        SelectFocus = _motherTabSelectFocus,
        SelectDefault = _motherTabSelectDefault,
        Icon = _motherTabIcon,
        Label = _motherTabLabel,
    };

    private DragonTabView BabyTabView => new DragonTabView
    {
        Button = _babyTabButton,
        SelectFocus = _babyTabSelectFocus,
        SelectDefault = _babyTabSelectDefault,
        Icon = _babyTabIcon,
        Label = _babyTabLabel,
    };

    // Select_Focus / Select_Defalt 중 하나만 켜고, 아이콘 Image와 라벨 TMP 색을 상태에 맞춘다.
    // 오브젝트를 복제하지 않고 색만 바꾸는 이유: 아이콘·텍스트를 프리팹에서 2벌 유지하면
    // 스프라이트·문구 변경 시 한쪽만 고치는 실수가 난다.
    private void ApplyTabVisual(DragonTabView tab, bool isSelected)
    {
        if (tab.SelectFocus != null)
        {
            tab.SelectFocus.SetActive(isSelected);
        }

        if (tab.SelectDefault != null)
        {
            tab.SelectDefault.SetActive(!isSelected);
        }

        if (tab.Icon != null)
        {
            tab.Icon.color = isSelected ? _tabIconFocusColor : _tabIconNormalColor;
        }

        if (tab.Label != null)
        {
            tab.Label.color = isSelected ? _tabLabelFocusColor : _tabLabelNormalColor;
        }
    }

    // Button_change 하나로 속성 변경 팝업을 열고 닫는다.
    // 팝업에는 바깥 클릭 blocker가 없어, 카드를 고르지 않고 무르려면 이 버튼이 유일한 수단이다.
    // (팝업은 버튼을 가리지 않는 위치에 떠서 두 번째 클릭이 버튼에 닿는다.)
    private void ToggleChangePopup()
    {
        if (_changePopup.IsOpen)
        {
            _changePopup.Close();
            return;
        }

        // 오늘 이미 바꿨으면 팝업을 열지 않고 경고만 띄운다 - 눌러도 아무 일이 없는 카드만 늘어놓는 것보다
        // 이유를 바로 알려주는 편이 낫다(변경 규칙 자체는 Dragon.TryChangeType이 단일 출처다).
        Dragon dragon = CurrentRun?.CurrentDragon;

        if (dragon != null && dragon.IsChangedThisDay)
        {
            _warningWindow?.ShowMotherDragonChangeWarning();
            return;
        }

        _changePopup.Open();
    }

    // 새끼용 탭 우측 설명(Panel_DragonInfo) - 마우스를 올린 슬롯의 속성을 표시한다.
    // 아무 슬롯에도 올라가 있지 않으면 비운다(프리팹 자리표시 문구 노출 방지).
    private void RenderBabyInfo()
    {
        if (!WiringGuard.Require(_babyInfoText, nameof(_babyInfoText), this))
        {
            return;
        }

        _babyInfoText.text = _hoveredBabyDragon != null
            ? StringTable.GetString(DragonLocKeys.BabyInfoLocKey(_hoveredBabyDragon.DragonType))
            : string.Empty;
    }

    private void HandleBabyDragonHoverChanged(BabyDragon dragon, bool isHovered)
    {
        if (isHovered)
        {
            _hoveredBabyDragon = dragon;
        }
        else if (_hoveredBabyDragon != dragon)
        {
            // 이미 다른 슬롯으로 옮겨간 뒤 뒤늦게 도착한 exit - 새 슬롯의 설명을 지우면 안 된다.
            return;
        }
        else
        {
            _hoveredBabyDragon = null;
        }

        RenderBabyInfo();
    }

    // 좌측 프레임에 현재 어미용을 반영한다.
    // 어미용에는 고유 이름 데이터가 없으므로(RunData.Dragon은 CurrentType만 들고 있다)
    // 표시 이름 = 로컬라이즈된 속성명이다(UI_MainCastleWindow.RenderPortrait와 동일 규칙).
    private void RenderMotherDragon()
    {
        DragonType? attribute = _dragonTreeManager != null ? _dragonTreeManager.ActiveAttribute : null;

        // RunData/CurrentDragon이 아직 없거나 매니저가 주입되지 않은 시점 -
        // 프리팹의 자리표시 문구("Dragon Name")가 그대로 보이지 않도록 비워둔다.
        if (!attribute.HasValue)
        {
            if (_motherNameText != null)
            {
                _motherNameText.text = string.Empty;
            }

            if (_motherInfoText != null)
            {
                _motherInfoText.text = string.Empty;
            }

            if (_motherIcon != null)
            {
                _motherIcon.enabled = false;
            }

            return;
        }

        DragonType type = attribute.Value;

        if (_motherNameText != null)
        {
            _motherNameText.text = StringTable.GetString(DragonLocKeys.AttributeLocKey(type));
        }

        if (_motherInfoText != null)
        {
            _motherInfoText.text = StringTable.GetString(DragonLocKeys.MotherInfoLocKey(type));
        }

        if (!WiringGuard.Require(_motherIcon, nameof(_motherIcon), this))
        {
            return;
        }

        _motherIcon.enabled = true;
        Sprite sprite = ResolveDragonSprite(type);

        if (sprite != null)
        {
            _motherIcon.sprite = sprite;
            _motherIcon.color = Color.white;
        }
        else
        {
            // 어미용 전용 스프라이트가 아직 없으면 최소한 속성 색으로 구분한다
            // (UI_MainCastleWindow.RenderPortrait와 같은 폴백).
            _motherIcon.color = DragonAttributePalette.ColorOf(type);
        }
    }

    private RunData CurrentRun => _gameManager != null ? _gameManager.CurrentRun : null;

    // 슬롯은 전부 프리팹에서 새로 만들어 컨테이너(Content) 아래에 넣는다.
    // 컨테이너를 프리팹의 부모에서 파생시키면 안 된다 - 프리팹 에셋은 부모가 없어(null)
    // 슬롯이 Content 밖에 생성되고 화면에 나타나지 않는다.
    private void CreateSlotPools()
    {
        if (_babyDragonSlotPrefab != null && _babyDragonSlotContainer != null)
        {
            _babyDragonSlotPool = new ComponentPool<UI_BabyDragonListSlot>(
                _babyDragonSlotPrefab, _babyDragonSlotContainer);
        }

        if (_eggSlotPrefab != null && _eggSlotContainer != null)
        {
            _eggSlotPool = new ComponentPool<UI_EggListSlot>(
                _eggSlotPrefab, _eggSlotContainer);
        }
    }

    // 보유 새끼용·알 목록을 현재 RunData 기준으로 다시 그린다.
    // 풀을 쓰므로 매번 Destroy/Instantiate하지 않고, 이번에 쓰지 않은 슬롯만 비활성화한다.
    private void RebuildLists()
    {
        RunData run = CurrentRun;
        if (run == null)
        {
            _babyDragonSlotPool?.DeactivateAll();
            _eggSlotPool?.DeactivateAll();
            OnSlotViewChanged?.Invoke();
            return;
        }

        BuildBabyDragonSlots(run);
        BuildEggSlots(run);

        OnSlotViewChanged?.Invoke();
    }

    private void BuildBabyDragonSlots(RunData run)
    {
        if (_babyDragonSlotPool == null)
        {
            return;
        }

        int used = 0;
        bool isHoveredStillListed = false;

        // 설치 중인 용도 함께 표시한다 - 슬롯의 Icon_Focus가 미배치(배치 시작)/배치(카메라 이동)로
        // 갈리므로 목록에서 빼면 배치된 개체를 찾아갈 방법이 없어진다.
        // (기존 BabyDragon_window는 슬롯이 배치 버튼이라 IsInTower를 걸러냈다.)
        foreach (BabyDragon dragon in run.BabyDragons)
        {
            if (!TryResolveBabyDragonData(dragon.DragonType, out BabyDragonData data))
            {
                continue;
            }

            _babyDragonSlotPool
                .Get(used)
                .Setup(dragon, data, HandleBabyDragonFocusClicked, HandleBabyDragonHoverChanged);
            used++;

            isHoveredStillListed |= dragon == _hoveredBabyDragon;
        }

        _babyDragonSlotPool.DeactivateFrom(used);

        // 슬롯이 풀에서 재사용되거나 비활성화될 때는 OnPointerExit가 오지 않는다.
        // 호버 중이던 용이 목록에서 사라졌을 때만 설명을 비우고, 남아 있으면 그대로 유지한다
        // (마우스를 올려둔 채 알이 부화하는 등으로 목록이 갱신돼도 설명이 깜빡이지 않게).
        if (!isHoveredStillListed)
        {
            _hoveredBabyDragon = null;
        }

        RenderBabyInfo();
    }

    // Icon_Focus 클릭 - 어느 쪽이든 창을 먼저 닫는다(그리드를 봐야 하는 동작이라 창이 방해된다).
    private void HandleBabyDragonFocusClicked(BabyDragon dragon)
    {
        Close();

        if (dragon.IsInTower)
        {
            FocusCameraOn(dragon);
            return;
        }

        if (_placementCoordinator != null)
        {
            _placementCoordinator.BeginPlacement(dragon);
        }
    }

    // 레코드에 결속된 인스턴스를 찾아 카메라를 옮긴다.
    // 속성으로 찾으면 같은 속성 두 마리 중 엉뚱한 쪽이 잡히므로 Record 동일성으로 찾는다
    // (BabyDragonPlacementCoordinator.HandleBuildingRemoving과 같은 이유).
    private void FocusCameraOn(BabyDragon dragon)
    {
        if (_cameraController == null || _gridMap == null)
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is BabyDragonTower tower && tower.Record == dragon)
            {
                _cameraController.MoveTo(tower.transform.position);
                return;
            }
        }
    }

    private void BuildEggSlots(RunData run)
    {
        if (_eggSlotPool == null)
        {
            return;
        }

        int used = 0;
        foreach (DragonEgg egg in run.DragonEggs)
        {
            if (!TryResolveBabyDragonData(egg.DragonType, out BabyDragonData data))
            {
                continue;
            }

            _eggSlotPool.Get(used).Setup(egg, data);
            used++;
        }

        _eggSlotPool.DeactivateFrom(used);
    }

    private bool TryResolveBabyDragonData(DragonType type, out BabyDragonData data)
    {
        if (_babyDragonDataCatalog != null)
        {
            return _babyDragonDataCatalog.TryResolve(type, out data);
        }

        data = null;
        return false;
    }

    // UI_MainCastleWindow.ResolveDragonSprite와 같은 규칙(전용 스프라이트 → 새끼용 카탈로그 폴백).
    private Sprite ResolveDragonSprite(DragonType type)
    {
        int index = (int)type;
        if (_motherSprites != null && index < _motherSprites.Length && _motherSprites[index] != null)
        {
            return _motherSprites[index];
        }

        if (_babyDragonDataCatalog != null &&
            _babyDragonDataCatalog.TryResolve(type, out BabyDragonData data))
        {
            return data.Sprite;
        }

        return null;
    }
}
