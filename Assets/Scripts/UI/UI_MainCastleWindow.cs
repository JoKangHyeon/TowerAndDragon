using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 성(Castle)을 선택했을 때 화면 우측에 뜨는 어미용 관리창.
// BuildingPlacementController.SelectedBuilding이 Castle일 때 열리도록
// SelectedBuildingChanged/InteractionStateChanged를 구독해 바인딩한다(새 클릭 배선 불필요).
// 주의: 현재 Castle.IsClickSelectable이 false라(성 클릭 아웃라인 제거 커밋 이후) 그리드 클릭으로는
// SelectedBuilding이 Castle이 되지 않고, 이 창은 어떤 씬에도 배치돼 있지 않다 - 여는 경로가
// 되살아나면(다른 진입점으로) 이 이벤트 배선만으로 동작한다.
// 속성 변경(선택→확정)·5속성 슬라임 현황 표시·용 스킬트리 진입을 담당하고,
// 연구 진행도/인구(스텁 필드)는 이번 범위에서 다루지 않는다.
public class UI_MainCastleWindow : MonoBehaviour
{
    private const string ATTRIBUTE_NAME_FORMAT = "{0}";
    private const string SLIME_LABEL_LOC_KEY = "main_castle_window_slime_label";
    private const string CHANGE_BUTTON_LOC_KEY = "main_castle_window_change_button";
    private const string CHANGE_AVAILABLE_LOC_KEY = "main_castle_window_change_available";
    private const string SKILL_TREE_BUTTON_LOC_KEY = "main_castle_window_skill_tree_button";
    private const string HEADER_LOC_KEY = "main_castle_window_header";
    private const string NIGHT_LOCKED_LOC_KEY = "building_window_night_locked";
    private const string CLOSE_LOC_KEY = "building_window_close";

    [SerializeField]
    private GameManager _gameManager;

    [SerializeField]
    private DragonTreeManager _dragonTreeManager;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [SerializeField]
    private CycleManager _cycleManager;

    [SerializeField]
    private ResourceManager _resourceManager;

    [Tooltip("용 창. 스킬트리는 이 창의 어미용 탭 안에 있다(과거 독립 스킬트리 창은 은퇴).")]
    [SerializeField]
    private UI_DragonWindow _dragonWindow;

    [SerializeField]
    private UIManager _uiManager;

    [Header("UiElements")]
    [SerializeField]
    private GameObject _windowRoot;

    [SerializeField]
    private TMP_Text _headerText;

    [SerializeField]
    private TMP_Text _nightLockedText;

    [SerializeField]
    private Button _closeButton;

    [SerializeField]
    private TMP_Text _closeButtonText;

    [Header("Dragon")]
    [SerializeField] private Image _dragonImage;
    [SerializeField] private TMP_Text _attributeNameText;
    [SerializeField] private TMP_Text _changeStatusText;
    [SerializeField] private Button _dragonTypeChangeButton;
    [SerializeField] private TMP_Text _dragonTypeChangeButtonText;
    [SerializeField] private Button _dragonSkillTreeButton;
    [SerializeField] private TMP_Text _dragonSkillTreeButtonText;

    [Tooltip("속성 선택 버튼 5개 - DragonType 선언 순서(Ice, Fire, Time, Stone, Life)와 일치해야 한다.")]
    [SerializeField] private Button[] _attributeButtons;

    [Tooltip("각 속성 버튼의 아이콘 - 인덱스는 위 버튼 배열과 동일.")]
    [SerializeField] private Image[] _attributeIcons;

    [Tooltip("각 속성 버튼의 선택 표시 - 인덱스는 위 버튼 배열과 동일.")]
    [SerializeField] private GameObject[] _attributeSelectionRings;

    [Tooltip("어미용 속성별 스프라이트 - DragonType 선언 순서. 비어 있는 칸은 새끼용 카탈로그 스프라이트로 대체한다.")]
    [SerializeField] private Sprite[] _dragonSprites;

    [Tooltip("_dragonSprites 칸이 비었을 때 대신 쓸 새끼용 스프라이트 카탈로그.")]
    [SerializeField] private BabyDragonDataCatalog _babyDragonDataCatalog;

    [Header("Slime - 5속성 고정 배치, 값만 갱신한다")]
    [SerializeField] private TMP_Text _slimeLabelText;

    [Tooltip("슬라임 현황 행 5개 - DragonType 선언 순서.")]
    [SerializeField] private UI_ConquestInfoSlot[] _slimeRows;

    [Header("Reseach")]
    [SerializeField] private Image _currentResearch;
    [SerializeField] private Slider _reserachStatusSlider;
    [SerializeField] private Button _researchSelectButton;
    [SerializeField] private Button _reserachAddPopButton;
    [SerializeField] private Button _reserachRemovePopButton;
    [SerializeField] private TMP_Text _researchPopText;
    [SerializeField] private TMP_Text _researchTimeLeftText;

    [Header("Pop")]
    [SerializeField] private TMP_Text _currentPopText;
    [SerializeField] private TMP_Text _currentPopFoodConsumeText;

    [Header("패널 열림/닫힘 연출")]
    [SerializeField] private float _slideDuration = 0.5f;
    [SerializeField] private Vector2 _openFromOffset = new Vector2(100f, 0f);
    [SerializeField] private Vector2 _closeToOffset = new Vector2(500f, 0f);

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    private DragonType _currentSelectedType = (DragonType)(-1);

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private Tween _panelTween;

    // 창이 열려 있는지(또는 열리는 중인지). _windowRoot.activeSelf로 판정하면 닫힘 트윈이
    // OnComplete에서야 SetActive(false)를 하므로 닫히는 중에도 "열려 있다"로 읽혀,
    // 그 사이의 재선택이 OpenPanel()(= 닫힘 트윈 Kill)을 건너뛰고 창이 꺼진 채 고착된다.
    // UI_BabyDragonManageWindow와 같은 방식으로 상태를 직접 들고 있는다.
    private bool _isOpen;

    private Castle _selectedCastle;
    private bool _wasInputSuppressed;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private Dragon CurrentDragon =>
        _gameManager != null ? _gameManager.CurrentRun?.CurrentDragon : null;

    // 굳은 맹세(sworn_element)의 주기당 변경 제한. 0이면 제한 없음이라 뮤테이터 이전과 같다.
    private int TypeChangeLimitPerCycle =>
        DragonTypeChangeRules.ResolveLimitPerCycle(
            RunModifiers.SnapshotOf(_gameManager != null ? _gameManager.RunModifierService : null));

    private int CurrentTypeChangeCycleNumber =>
        DragonTypeChangeRules.ResolveCycleNumber(_cycleManager);

    // 이번 주기의 변경권을 이미 다 썼는가. 버튼 비활성화와 상태 줄이 같은 판정을 쓴다.
    private bool IsTypeChangeCycleLimitReached
    {
        get
        {
            Dragon dragon = CurrentDragon;

            return dragon != null &&
                   dragon.IsCycleLimitReached(CurrentTypeChangeCycleNumber, TypeChangeLimitPerCycle);
        }
    }

    private void Awake()
    {
        _panelRect = _windowRoot.GetComponent<RectTransform>();
        _homePos = _panelRect.anchoredPosition;

        AddButtonListeners();
        ApplyLocalizedLabels();

        _windowRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleDayStart);
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
        }

        // Update 폴링이 하던 일을 이벤트로 옮긴다. 폴링이 없어지면 배선 누락을 매 프레임 확인할
        // 자리도 없어지므로, 구독을 거는 이 자리에서 한 번 검사한다.
        if (WiringGuard.Require(_buildingPlacementController, nameof(_buildingPlacementController), this))
        {
            _buildingPlacementController.SelectedBuildingChanged.AddListener(HandleSelectedBuildingChanged);
            _buildingPlacementController.InteractionStateChanged.AddListener(HandleInteractionStateChanged);

            // 구독 직후 현재 값 1회 반영. Bind는 shouldOpen이 false면 Close()를 부르지 않으므로
            // 이미 닫혀 있는 창에 닫힘 연출이 돌지 않는다.
            _wasInputSuppressed = _buildingPlacementController.InputSuppressed;
            Bind(_buildingPlacementController.SelectedBuilding as Castle);
        }
    }

    private void OnDisable()
    {
        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.RemoveListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleDayStart);
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
        }

        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.SelectedBuildingChanged.RemoveListener(HandleSelectedBuildingChanged);
            _buildingPlacementController.InteractionStateChanged.RemoveListener(HandleInteractionStateChanged);
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    // 그리드에서 고른 건물이 바뀌었다. 성이 아니면 null로 바인딩해 창을 닫는다.
    private void HandleSelectedBuildingChanged(Building building)
    {
        // 억제 상태도 함께 최신화한다 - Bind가 _wasInputSuppressed를 읽으므로, 두 이벤트가 오는
        // 순서에 결과가 좌우되지 않게 한다.
        _wasInputSuppressed = _buildingPlacementController.InputSuppressed;
        Bind(building as Castle);
    }

    // 억제 여부만 본다. 이동 모드·이동 예산은 이 창의 표시에 관여하지 않는다.
    private void HandleInteractionStateChanged()
    {
        bool inputSuppressed = _buildingPlacementController.InputSuppressed;
        if (_wasInputSuppressed == inputSuppressed)
        {
            return;
        }

        _wasInputSuppressed = inputSuppressed;
        Bind(_selectedCastle);
    }

    private void Bind(Castle castle)
    {
        _selectedCastle = castle;

        // 점령 모드 등 다른 모드가 클릭을 점유한 동안에는 그쪽 창(Claim_window)이 같은 자리를
        // 쓰므로 이 창을 숨긴다.
        bool shouldOpen =
            castle != null &&
            CurrentDragon != null &&
            !_wasInputSuppressed;

        bool isOpen = _isOpen;

        if (shouldOpen && !isOpen)
        {
            _currentSelectedType = (DragonType)(-1);
            OpenPanel();
            Render();
        }
        else if (!shouldOpen && isOpen)
        {
            Close();
        }
        else if (shouldOpen)
        {
            Render();
        }
    }

    // 씬에 미리 세팅해 둔 위치(_homePos)에서 슬라이드 인 시킨다(UI_ConquestWindow.OpenPanel과 동일 패턴).
    private void OpenPanel()
    {
        _isOpen = true;
        _panelTween?.Kill();

        _windowRoot.SetActive(true);
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_windowRoot);
    }

    private void Close()
    {
        _isOpen = false;
        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_windowRoot)
            .OnComplete(() => _windowRoot.SetActive(false));
    }

    // 닫기 버튼 - 선택을 해제하면 Deselect의 알림(SelectedBuildingChanged)이 창을 닫는다
    // (UI_PopulationAllocationWindow.CloseWindow와 동일).
    private void CloseWindow()
    {
        SoundManager.Play(SoundId.UiWindowClose);
        _buildingPlacementController?.Deselect();
    }

    public void SelectDragonType(DragonType dragonType)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        _currentSelectedType = dragonType;
        Render();
    }

    public void ApplyDragonType()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        Dragon dragon = CurrentDragon;
        if (dragon == null)
        {
            return;
        }

        // 안내가 아직 속성 변경을 가르치지 않았다. 용 창의 변경 버튼과 같은 판정을 써야
        // 같은 조작이 경로에 따라 갈리지 않는다.
        if (_dragonTreeManager != null && !_dragonTreeManager.CanChangeAttributeNow)
        {
            _dragonTreeManager.NotifyProgressionBlocked();
            return;
        }

        bool changed = dragon.TryChangeType(
            _currentSelectedType,
            CurrentTypeChangeCycleNumber,
            TypeChangeLimitPerCycle,
            out DragonTypeChangeBlock block);

        // 제한에 걸렸으면 조용히 넘기지 않는다 - Render가 상태 줄에 사유를 쓰고 버튼을 잠근다.
        if (block == DragonTypeChangeBlock.CycleLimitReached)
        {
            Render();
            return;
        }

        // DragonTreeManager는 이 알림 없이는 속성 변경을 감지할 수 없다(Dragon.OnDragonTypeChanged가
        // 어디서도 invoke되지 않음 - DragonTreeManager.cs 주석 참고). 변경이 실제로 적용됐을 때만
        // 알려야 같은 속성을 다시 고른 경우까지 HUD를 불필요하게 재바인딩하지 않는다.
        if (changed && _dragonTreeManager != null)
        {
            _dragonTreeManager.NotifyActiveAttributeChanged();
        }

        if (changed)
        {
            _currentSelectedType = (DragonType)(-1);
        }

        Render();
    }

    // 스킬트리가 용 창의 어미용 탭으로 옮겨갔으므로 용 창을 연다.
    // 클릭음을 내지 않는다 - 창을 여닫는 제스처라 대상 창(UI_DragonWindow)이 열림/닫힘음을 낸다.
    public void OpenDragonSkillTree()
    {
        if (!WiringGuard.Require(_dragonWindow, nameof(_dragonWindow), this))
        {
            return;
        }

        if (_uiManager != null)
        {
            _uiManager.OpenExclusive(_dragonWindow);
        }
        else
        {
            _dragonWindow.ToggleFromEntryPoint();
        }
    }

    public void Render()
    {
        Dragon dragon = CurrentDragon;
        if (dragon == null)
        {
            return;
        }

        DragonType displayedType = _currentSelectedType == (DragonType)(-1)
            ? dragon.CurrentType
            : _currentSelectedType;

        RenderPortrait(displayedType);
        RenderAttributeButtons(dragon.CurrentType, displayedType);
        RenderSlimeRows();
        RenderChangeState(dragon);

        if (_nightLockedText != null)
        {
            _nightLockedText.gameObject.SetActive(!IsDay);
        }
    }

    private void RenderPortrait(DragonType type)
    {
        if (_attributeNameText != null)
        {
            _attributeNameText.text = string.Format(
                ATTRIBUTE_NAME_FORMAT,
                StringTable.GetString(DragonLocKeys.AttributeLocKey(type)));
            _attributeNameText.color = DragonAttributePalette.ColorOf(type);
        }

        if (!WiringGuard.Require(_dragonImage, nameof(_dragonImage), this))
        {
            return;
        }

        Sprite sprite = ResolveDragonSprite(type);
        if (sprite != null)
        {
            _dragonImage.sprite = sprite;
            _dragonImage.color = Color.white;
        }
        else
        {
            // 어미용 전용 스프라이트가 아직 없으면 최소한 속성 색으로 구분한다.
            _dragonImage.color = DragonAttributePalette.ColorOf(type);
        }
    }

    private Sprite ResolveDragonSprite(DragonType type)
    {
        int index = (int)type;
        if (_dragonSprites != null && index < _dragonSprites.Length && _dragonSprites[index] != null)
        {
            return _dragonSprites[index];
        }

        if (_babyDragonDataCatalog != null &&
            _babyDragonDataCatalog.TryResolve(type, out BabyDragonData data))
        {
            return data.Sprite;
        }

        return null;
    }

    private void RenderAttributeButtons(DragonType activeType, DragonType selectedType)
    {
        if (!WiringGuard.RequireNotEmpty(_attributeButtons, nameof(_attributeButtons), this))
        {
            return;
        }

        for (int i = 0; i < _attributeButtons.Length && i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            DragonType attribute = ATTRIBUTES_IN_ORDER[i];

            if (_attributeIcons != null && i < _attributeIcons.Length && _attributeIcons[i] != null)
            {
                _attributeIcons[i].color = DragonAttributePalette.ColorOf(attribute);
            }

            if (_attributeSelectionRings != null && i < _attributeSelectionRings.Length &&
                _attributeSelectionRings[i] != null)
            {
                _attributeSelectionRings[i].SetActive(attribute == selectedType);
            }
        }
    }

    // 자원 아이콘·이름은 데이터 에셋(ResourceData)이 단일 출처 - 카탈로그에서 종류로 조회한다
    // (UI_IngameWindow.ApplyResourceIcons와 같은 경로).
    private void RenderSlimeRows()
    {
        if (_slimeRows == null || _resourceManager == null)
        {
            return;
        }

        for (int i = 0; i < _slimeRows.Length && i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            UI_ConquestInfoSlot row = _slimeRows[i];
            if (row == null)
            {
                continue;
            }

            DragonType attribute = ATTRIBUTES_IN_ORDER[i];
            if (!DragonSlimeTable.TryGetFeedSlime(attribute, out ResourceType slimeType))
            {
                continue;
            }

            Sprite icon = null;
            string name = slimeType.ToString();
            if (_resourceManager.Catalog != null &&
                _resourceManager.Catalog.TryGet(slimeType, out ResourceData data))
            {
                icon = data.Icon;
                name = StringTable.GetString(data.NameLocKey);
            }

            row.Setup(
                icon,
                Color.white,
                name,
                _resourceManager.GetAmount(slimeType).ToString());
        }
    }

    private void RenderChangeState(Dragon dragon)
    {
        bool hasSelection = _currentSelectedType != (DragonType)(-1);
        bool isDifferentFromCurrent = !hasSelection || _currentSelectedType != dragon.CurrentType;

        // 낮인지, 현재와 다른 속성을 골랐는지, 그리고 이번 주기의 변경권이 남았는지를 본다.
        // 마지막 조건은 굳은 맹세(sworn_element)가 꺼져 있으면 항상 참이다.
        bool isCycleLimitReached = IsTypeChangeCycleLimitReached;
        bool canChange =
            IsDay &&
            hasSelection &&
            isDifferentFromCurrent &&
            !isCycleLimitReached;

        if (_dragonTypeChangeButton != null)
        {
            _dragonTypeChangeButton.interactable = canChange;
        }

        if (_changeStatusText != null)
        {
            _changeStatusText.text = StringTable.GetString(
                isCycleLimitReached
                    ? Defines.DRAGON_TYPE_CHANGE_CYCLE_LIMIT_LOC_KEY
                    : CHANGE_AVAILABLE_LOC_KEY);
        }
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (DragonSlimeTable.TryGetAttribute(type, out _))
        {
            RenderSlimeRows();
        }
    }

    private void HandleDayStart(int day)
    {
        _currentSelectedType = (DragonType)(-1);
        Render();
    }

    private void HandleCycleChanged(CycleManager.CycleState state)
    {
        Render();
    }

    private void AddButtonListeners()
    {
        if (_attributeButtons != null)
        {
            for (int i = 0; i < _attributeButtons.Length && i < ATTRIBUTES_IN_ORDER.Length; i++)
            {
                DragonType attribute = ATTRIBUTES_IN_ORDER[i];
                _attributeButtons[i]?.onClick.AddListener(() => SelectDragonType(attribute));
            }
        }

        _dragonTypeChangeButton?.onClick.AddListener(ApplyDragonType);
        _dragonSkillTreeButton?.onClick.AddListener(OpenDragonSkillTree);
        _closeButton?.onClick.AddListener(CloseWindow);
    }

    private void RemoveButtonListeners()
    {
        _dragonTypeChangeButton?.onClick.RemoveListener(ApplyDragonType);
        _dragonSkillTreeButton?.onClick.RemoveListener(OpenDragonSkillTree);
        _closeButton?.onClick.RemoveListener(CloseWindow);
    }

    private void ApplyLocalizedLabels()
    {
        SetLabel(_headerText, HEADER_LOC_KEY);
        SetLabel(_slimeLabelText, SLIME_LABEL_LOC_KEY);
        SetLabel(_dragonTypeChangeButtonText, CHANGE_BUTTON_LOC_KEY);
        SetLabel(_dragonSkillTreeButtonText, SKILL_TREE_BUTTON_LOC_KEY);
        SetLabel(_nightLockedText, NIGHT_LOCKED_LOC_KEY);
        SetLabel(_closeButtonText, CLOSE_LOC_KEY);
    }

    private static void SetLabel(TMP_Text text, string locKey)
    {
        if (text != null)
        {
            text.text = StringTable.GetString(locKey);
        }
    }
}
