using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 용 창 - 어미용/새끼용 탭을 전환하고, 좌측 프레임(Left_Panel_frame)에 현재 어미용의
// 아이콘과 속성명을 표시한다. blocker 루트에 부착되어 바깥 클릭 닫기와
// IExclusiveMode(UIManager.OpenExclusive) 조정을 겸한다 - UI_ResearchWindow / UI_DragonSkillWindow와
// 동일한 구조(Assets/Scripts/UI/Research/UI_ResearchWindow.cs, Assets/Scripts/UI/Dragon/UI_DragonSkillWindow.cs).
// 속성 변경 규칙·속성 색·아이콘 원본은 여기서 재구현하지 않는다
// - DragonTreeManager / DragonAttributePalette / BabyDragonDataCatalog에 위임한다.
// Button_change와 Panel_Info는 이번 범위가 아니다(배선하지 않는다).
public class UI_DragonWindow : MonoBehaviour, IExclusiveMode
{
    // 탭 - Panel_MomDragon / Panel_BabyDragon 중 하나만 활성이 된다.
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

    [Header("Tab - Mother (TabMenu/Button_MomDragon)")]
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
    [Tooltip("어미용 속성별 스프라이트 - DragonType 선언 순서(Ice, Fire, Time, Stone, Life). 빈 칸은 새끼용 카탈로그 스프라이트로 대체한다.")]
    [SerializeField] private Sprite[] _motherSprites;
    [Tooltip("_motherSprites 칸이 비었을 때 대신 쓸 새끼용 스프라이트 카탈로그. 새끼용/알 리스트의 데이터 조회에도 쓴다.")]
    [SerializeField] private BabyDragonDataCatalog _babyDragonDataCatalog;

    [Header("Baby Dragon / Egg 리스트 (Panel_BabyDragon)")]
    [Tooltip("보유 새끼용·알 목록의 출처(RunData). 없으면 두 리스트를 비운다.")]
    [SerializeField] private GameManager _gameManager;

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

    private DragonTab _currentTab = DragonTab.Mother;
    private bool _isOpen;

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

        CreateSlotPools();

        // 프리팹에는 Select_Focus와 Select_Defalt가 두 버튼 모두 켜져 있다(실측) -
        // 이 호출이 그 상태를 정상화하는 유일한 지점이므로 Awake에서 한 번 확정한다.
        SelectTab(_currentTab);

        // UI_Canvas 인스턴스가 활성(m_IsActive:1)으로 저장돼 있어도 시작 시 닫힌 상태를 보장한다
        // (UI_DragonInventoryWindow.Awake의 _panel.SetActive(false)와 같은 방어).
        //
        // _isOpen 가드가 필요한 이유: 인스턴스가 비활성으로 저장된 경우 Unity는 씬 로드 때 Awake를
        // 호출하지 않고, 첫 Open()의 SetActive(true) 안에서 비로소 이 Awake가 동기 실행된다.
        // 그때 무조건 Close()하면 방금 연 창을 스스로 닫아 첫 클릭이 먹지 않는다.
        // Open()은 SetActive(true) 전에 _isOpen을 세우므로, 이 값으로 두 경우를 구분한다.
        if (!_isOpen)
        {
            Close();
        }
    }

    private void OnEnable()
    {
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
        Close();
    }

    private void HandleActiveAttributeChanged(DragonType attribute)
    {
        RenderMotherDragon();
    }

    public void Open()
    {
        _isOpen = true;
        gameObject.SetActive(true); // 비활성이었다면 이 안에서 OnEnable(구독 + 1회 반영)이 돈다

        // 이미 활성인 상태로 다시 열린 경우(OnEnable 미발화)를 위해 시각 상태를 한 번 더 확정한다.
        SelectTab(_currentTab);
        RenderMotherDragon();
    }

    public void Close()
    {
        _isOpen = false;
        gameObject.SetActive(false);
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

    // 선택된 탭의 패널만 켜고, 두 탭 버튼의 시각 상태를 갱신한다.
    // 탭 선택은 창을 닫아도 유지된다(다시 열면 마지막 탭이 보인다).
    private void SelectTab(DragonTab tab)
    {
        _currentTab = tab;
        bool isMother = tab == DragonTab.Mother;

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
            RebuildLists();
        }
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

        if (_motherIcon == null)
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
            return;
        }

        BuildBabyDragonSlots(run);
        BuildEggSlots(run);
    }

    private void BuildBabyDragonSlots(RunData run)
    {
        if (_babyDragonSlotPool == null)
        {
            return;
        }

        int used = 0;
        foreach (BabyDragon dragon in run.BabyDragons)
        {
            // 설치 중인 용은 목록에 표시하지 않는다 - 기존 BabyDragon_window(UI_DragonInventoryWindow)와 같은 규칙.
            if (dragon.IsInTower)
            {
                continue;
            }

            if (!TryResolveBabyDragonData(dragon.DragonType, out BabyDragonData data))
            {
                continue;
            }

            _babyDragonSlotPool.Get(used).Setup(dragon, data);
            used++;
        }

        _babyDragonSlotPool.DeactivateFrom(used);
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
