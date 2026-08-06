using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 메인 성(Castle) 클릭 시 화면 우측에 뜨는 어미용 관리창.
// Castle은 3x3 풋프린트로 그리드에 등록돼 있어 클릭은 이미 BuildingPlacementController.
// SelectExistingBuildingAt()을 타고 SelectedBuilding으로 돌아온다(새 클릭 배선 불필요) -
// 선택 변경 이벤트가 없으므로(SelectedBuilding은 파생 getter) UI_PopulationAllocationWindow와
// 같은 방식으로 매 프레임 폴링한다.
// 속성 변경(선택→확정)·5속성 슬라임 현황 표시·용 스킬트리 진입을 담당하고,
// 연구 진행도/인구(스텁 필드)는 이번 범위에서 다루지 않는다.
public class UI_MainCastleWindow : MonoBehaviour
{
    private const string ATTRIBUTE_NAME_FORMAT = "{0}";
    private const string SLIME_LABEL_LOC_KEY = "main_castle_window_slime_label";
    private const string CHANGE_BUTTON_LOC_KEY = "main_castle_window_change_button";
    private const string CHANGE_AVAILABLE_LOC_KEY = "main_castle_window_change_available";
    private const string CHANGE_USED_LOC_KEY = "main_castle_window_change_used";
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
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    // BuildingPlacementController에는 선택 변경 이벤트가 없어(SelectedBuilding은 파생 getter)
    // 매 프레임 확인한다 - UI_PopulationAllocationWindow와 같은 방식이다.
    private void Update()
    {
        if (!WiringGuard.Require(_buildingPlacementController, nameof(_buildingPlacementController), this))
        {
            return;
        }

        Castle selectedCastle =
            _buildingPlacementController.SelectedBuilding as Castle;
        bool inputSuppressed = _buildingPlacementController.InputSuppressed;

        if (_selectedCastle == selectedCastle && _wasInputSuppressed == inputSuppressed)
        {
            return;
        }

        _wasInputSuppressed = inputSuppressed;
        Bind(selectedCastle);
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

    // 닫기 버튼 - 선택을 해제하면 Update가 창을 자동으로 닫는다
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

        bool changed = dragon.TryChangeType(_currentSelectedType);

        // DragonTreeManager는 이 알림 없이는 속성 변경을 감지할 수 없다(Dragon.OnDragonTypeChanged가
        // 어디서도 invoke되지 않음 - DragonTreeManager.cs 주석 참고). 변경이 실제로 적용됐을 때만
        // 알려야 낮 1회 제한(IsChangedThisDay)에 막힌 시도까지 HUD를 불필요하게 재바인딩하지 않는다.
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

        bool canChange =
            IsDay &&
            !dragon.IsChangedThisDay &&
            hasSelection &&
            isDifferentFromCurrent;

        if (_dragonTypeChangeButton != null)
        {
            _dragonTypeChangeButton.interactable = canChange;
        }

        if (_changeStatusText != null)
        {
            _changeStatusText.text = StringTable.GetString(
                dragon.IsChangedThisDay ? CHANGE_USED_LOC_KEY : CHANGE_AVAILABLE_LOC_KEY);
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
