using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그리드에 설치된 새끼용을 선택했을 때 뜨는 관리창. UI_PopulationAllocationWindow와 같은 방식으로
/// BuildingPlacementController.SelectedBuilding을 매 프레임 폴링해 바인딩하고(선택 변경 이벤트가 없으므로),
/// 열림/닫힘 연출은 UI_BuildModeWindow의 DOTween 슬라이드 패턴을 따른다.
/// IExclusiveMode는 구현하지 않는다 - 사용자가 열고 닫는 모드형 창이 아니라 그리드 선택에 종속된 정보창이라
/// UI_PopulationAllocationWindow와 같은 부류다. 단, 스크립트가 붙은 오브젝트 자체는 항상 활성 상태를 유지해
/// Update 폴링이 멈추지 않게 하고, 실제 여닫히는 것은 자식 _panel뿐이다(BuildMode_window와 다른 점).
/// </summary>
public class UI_BabyDragonManageWindow : MonoBehaviour
{
    private const string BUFF_RADIUS_FORMAT = "{0:0.#}";
    private const string BUFF_MULTIPLIER_FORMAT = "×{0:0.##}";

    private const string TITLE_FORMAT_LOC_KEY = "baby_dragon_manage_title_format";
    private const string STATUS_LABEL_LOC_KEY = "baby_dragon_manage_status_label";
    private const string STATUS_ACTIVE_LOC_KEY = "baby_dragon_manage_status_active";
    private const string STATUS_STARVING_LOC_KEY = "baby_dragon_manage_status_starving";
    private const string FEED_LABEL_LOC_KEY = "baby_dragon_manage_feed_label";
    private const string FEED_INFO_LOC_KEY = "baby_dragon_feed_info"; // UI_DragonInventorySlot과 공유하는 기존 키
    private const string BUFF_RADIUS_LABEL_LOC_KEY = "baby_dragon_manage_buff_radius_label";
    private const string BUFF_MULTIPLIER_LABEL_LOC_KEY = "baby_dragon_manage_buff_multiplier_label";
    private const string MODE_ATTACK_LOC_KEY = "baby_dragon_manage_mode_attack";
    private const string MODE_BUFF_LOC_KEY = "baby_dragon_manage_mode_buff";
    private const string RELOCATE_LOC_KEY = "baby_dragon_manage_relocate";
    private const string SKILL_TREE_LOC_KEY = "baby_dragon_manage_skill_tree";
    private const string REMOVE_LOC_KEY = "baby_dragon_manage_remove";
    private const string CLOSE_LOC_KEY = "baby_dragon_manage_close";
    private const string NIGHT_LOCKED_LOC_KEY = "baby_dragon_manage_night_locked";

    [Header("Dependencies")]
    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private ResourceCatalog _resourceCatalog;
    [Tooltip("용 창. 강화 트리는 이 창의 어미용 탭 안에 있다(과거 독립 스킬트리 창은 은퇴).")]
    [SerializeField] private UI_DragonWindow _dragonWindow;

    [Header("패널")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Image _titleIcon;

    [Header("정보 행 - 개수가 고정이라 풀링 없이 슬롯을 직접 지정한다")]
    [SerializeField] private UI_ConquestInfoSlot _statusRow;
    [SerializeField] private UI_ConquestInfoSlot _feedRow;
    [SerializeField] private UI_ConquestInfoSlot _buffRadiusRow;
    [SerializeField] private UI_ConquestInfoSlot _buffMultiplierRow;
    [SerializeField] private Sprite _statusIcon;
    [SerializeField] private Sprite _buffIcon;

    // 모드 버튼은 Focus/Default 오브젝트 없이, "지금 그 모드인 버튼은 비활성화(회색)"로 현재 모드를
    // 나타낸다 - 데이터상 못 쓰는 모드도 같은 이유(interactable=false)로 비활성화되므로 조건을 합친다.
    [Header("모드 버튼 - 공격/버프")]
    [SerializeField] private Button _attackModeButton;
    [SerializeField] private Button _buffModeButton;
    [SerializeField] private TMP_Text _attackModeButtonText;
    [SerializeField] private TMP_Text _buffModeButtonText;

    [Header("행동 버튼")]
    [SerializeField] private Button _skillTreeButton;
    [SerializeField] private Button _relocateButton;
    [SerializeField] private Button _removeButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _skillTreeButtonText;
    [SerializeField] private TMP_Text _relocateButtonText;
    [SerializeField] private TMP_Text _removeButtonText;
    [SerializeField] private TMP_Text _closeButtonText;
    [Tooltip("밤에는 재배치가 잠기는 이유를 알려준다 - 정원/가용 한도와 같은 방식(UI_PopulationAllocationWindow._nightLockedText).")]
    [SerializeField] private TMP_Text _nightLockedText;

    [Header("패널 슬라이드 연출")]
    [SerializeField] private float _slideDuration = 0.4f;
    [Tooltip("열릴 때 시작 오프셋(홈 기준). 여기서 홈으로 슬라이드 인.")]
    [SerializeField] private Vector2 _openFromOffset = new Vector2(-100f, 0f);
    [Tooltip("닫힐 때 도착 오프셋(홈 기준). 홈에서 여기로 슬라이드 아웃 후 비활성화.")]
    [SerializeField] private Vector2 _closeToOffset = new Vector2(-500f, 0f);

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private bool _isOpen;
    private Tween _panelTween;

    private BabyDragonTower _boundTower;
    private bool _wasSuppressed;

    private bool IsDay =>
        _cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private void Awake()
    {
        _panelRect = _panel.GetComponent<RectTransform>();
        _homePos = _panelRect.anchoredPosition;
        _panel.SetActive(false);

        AddButtonListeners();
        ApplyStaticLabels();

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
            _cycleManager.OnDayStartUpkeep.AddListener(HandleDayStart);
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
            _cycleManager.OnDayStartUpkeep.RemoveListener(HandleDayStart);
        }

        if (_boundTower != null)
        {
            _boundTower.ModeChanged.RemoveListener(HandleModeChanged);
        }

        _panelTween?.Kill();
    }

    // BuildingPlacementController에는 선택 변경 이벤트가 없어(SelectedBuilding은 파생 getter)
    // 매 프레임 확인한다 - UI_PopulationAllocationWindow와 동일한 방식.
    private void Update()
    {
        if (_buildingPlacementController == null)
        {
            return;
        }

        Building selected = _buildingPlacementController.SelectedBuilding;
        bool suppressed = _buildingPlacementController.InputSuppressed;
        BabyDragonTower tower = selected as BabyDragonTower;

        if (_boundTower == tower && _wasSuppressed == suppressed)
        {
            if (_isOpen)
            {
                RefreshButtons();
            }
            return;
        }

        _wasSuppressed = suppressed;
        Bind(tower);
    }

    private void Bind(BabyDragonTower tower)
    {
        if (_boundTower != null)
        {
            _boundTower.ModeChanged.RemoveListener(HandleModeChanged);
        }

        _boundTower = tower;

        if (_boundTower != null)
        {
            _boundTower.ModeChanged.AddListener(HandleModeChanged);
        }

        // 점령 모드 등 다른 모드가 클릭을 점유한 동안에는 이 창을 숨긴다
        // (UI_PopulationAllocationWindow.Bind와 동일한 이유).
        bool hasTarget = _boundTower != null && !_wasSuppressed;

        if (hasTarget)
        {
            OpenPanel();
            Refresh();
        }
        else
        {
            ClosePanel();
        }
    }

    private void OpenPanel()
    {
        _isOpen = true;
        _panelTween?.Kill();

        _panel.SetActive(true);
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_panel);
    }

    private void ClosePanel()
    {
        _isOpen = false;
        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_panel)
            .OnComplete(() => _panel.SetActive(false));
    }

    // 밤이 시작되면 열려 있던 패널을 닫는다(UI_BuildModeWindow.HandleNightStart와 동일).
    // _boundTower는 그대로 두므로, 같은 새끼용이 계속 선택되어 있는 한 낮이 와도 자동으로 다시
    // 열리지는 않는다 - 다른 건물을 선택했다가 되돌아오면 재바인딩되어 열린다(기존 창들과 같은 한계).
    private void HandleNightStart(int _)
    {
        if (_isOpen)
        {
            ClosePanel();
        }
    }

    // 먹이 지급은 아침(OnDayStartUpkeep)에 갱신되므로, 열려 있는 동안 새 급여 결과를 반영한다.
    private void HandleDayStart(int _)
    {
        if (_isOpen)
        {
            Refresh();
        }
    }

    private void HandleModeChanged()
    {
        if (_isOpen)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_boundTower == null || _boundTower.DragonData == null)
        {
            return;
        }

        BabyDragonData data = _boundTower.DragonData;
        DragonType type = data.DragonType;
        Color attributeColor = DragonAttributePalette.ColorOf(type);
        string attributeName = StringTable.GetString(DragonLocKeys.AttributeLocKey(type));

        if (_titleText != null)
        {
            _titleText.text = string.Format(StringTable.GetString(TITLE_FORMAT_LOC_KEY), attributeName);
        }

        if (_titleIcon != null)
        {
            _titleIcon.sprite = data.Sprite;
            _titleIcon.color = Color.white;
        }

        bool isActive = _boundTower.CanOperate;
        _statusRow?.Setup(
            _statusIcon,
            Color.white,
            StringTable.GetString(STATUS_LABEL_LOC_KEY),
            StringTable.GetString(isActive ? STATUS_ACTIVE_LOC_KEY : STATUS_STARVING_LOC_KEY));

        RefreshFeedRow(data);
        RefreshBuffRows(data, attributeColor);
        RefreshButtons();
    }

    // 속성 색을 받지 않는다 - 먹이 슬라임이 속성별 전용 스프라이트를 갖게 되어 틴트가 필요 없다.
    private void RefreshFeedRow(BabyDragonData data)
    {
        int dailyFeed = ComputeDailyFeed(data);

        Sprite feedIcon = null;
        Color feedColor = Color.white;
        if (DragonSlimeTable.TryGetFeedSlime(data.DragonType, out ResourceType slimeType) &&
            _resourceCatalog != null &&
            _resourceCatalog.TryGet(slimeType, out ResourceData resourceData))
        {
            // 슬라임은 속성별 전용 스프라이트를 갖고 있으므로 틴트하지 않는다(feedColor는 흰색 그대로).
            feedIcon = resourceData.Icon;
        }

        _feedRow?.Setup(
            feedIcon,
            feedColor,
            StringTable.GetString(FEED_LABEL_LOC_KEY),
            string.Format(StringTable.GetString(FEED_INFO_LOC_KEY), dailyFeed));
    }

    // 현재 설치된(IsInTower) 개체 집계 기준 - BabyDragonFeedingSystem이 매일 아침 계산하는 것과 같은 공식.
    private int ComputeDailyFeed(BabyDragonData data)
    {
        if (_gameManager == null || _gameManager.CurrentRun == null)
        {
            return data.BaseFeed;
        }

        int sameTypeCount = 0;
        int totalCount = 0;

        foreach (BabyDragon dragon in _gameManager.CurrentRun.BabyDragons)
        {
            if (!dragon.IsInTower)
            {
                continue;
            }

            totalCount++;
            if (dragon.DragonType == data.DragonType)
            {
                sameTypeCount++;
            }
        }

        return BabyDragonFeedFormula.ResolveDailyFeed(data, sameTypeCount, totalCount);
    }

    // 버프 반경이 없는 속성(순수 공격형)은 버프 관련 행을 아예 숨긴다 - 값이 0인 채로 보여주면
    // "버프가 있는데 반경만 0"으로 오해할 수 있다.
    private void RefreshBuffRows(BabyDragonData data, Color attributeColor)
    {
        bool hasBuff = _boundTower.CanUseBuffMode;

        if (_buffRadiusRow != null)
        {
            _buffRadiusRow.gameObject.SetActive(hasBuff);
            if (hasBuff)
            {
                _buffRadiusRow.Setup(
                    _buffIcon,
                    attributeColor,
                    StringTable.GetString(BUFF_RADIUS_LABEL_LOC_KEY),
                    string.Format(BUFF_RADIUS_FORMAT, data.BuffRadius));
            }
        }

        if (_buffMultiplierRow != null)
        {
            _buffMultiplierRow.gameObject.SetActive(hasBuff);
            if (hasBuff)
            {
                _buffMultiplierRow.Setup(
                    _buffIcon,
                    attributeColor,
                    StringTable.GetString(BUFF_MULTIPLIER_LABEL_LOC_KEY),
                    string.Format(BUFF_MULTIPLIER_FORMAT, data.BuffYieldMultiplier));
            }
        }
    }

    // 매 프레임 바뀔 수 있는 것만 가볍게 동기화한다(UI_BuildModeWindow.Update와 동일한 관례) -
    // 이동 중 여부, 밤 여부, 모드 가용성은 이 창을 열어 둔 채로도 실시간으로 바뀔 수 있다.
    private void RefreshButtons()
    {
        if (_boundTower == null)
        {
            return;
        }

        if (_relocateButton != null)
        {
            _relocateButton.gameObject.SetActive(
                _buildingPlacementController == null || !_buildingPlacementController.IsMoving);
            _relocateButton.interactable = _buildingPlacementController != null &&
                _buildingPlacementController.CanMoveNow(_boundTower);
        }

        if (_removeButton != null)
        {
            _removeButton.interactable = _buildingPlacementController != null &&
                _buildingPlacementController.CanRemoveNow(_boundTower);
        }

        SetModeButtonState(_attackModeButton, _boundTower.Mode == BabyDragonMode.Attack, _boundTower.CanUseAttackMode);
        SetModeButtonState(_buffModeButton, _boundTower.Mode == BabyDragonMode.Buff, _boundTower.CanUseBuffMode);

        // 버튼이 회색인 이유(밤)를 알려준다(UI_PopulationAllocationWindow._nightLockedText와 동일한 이유).
        if (_nightLockedText != null)
        {
            _nightLockedText.gameObject.SetActive(!IsDay);
        }
    }

    // 지금 그 모드인 버튼은 눌러도 의미가 없어 비활성화한다 - 데이터상 쓸 수 없는 모드(isAvailable == false)도
    // 같은 방식으로 비활성화되므로, 결과적으로 "회색 버튼 = 지금 이 모드이거나 애초에 못 씀"이 된다.
    private static void SetModeButtonState(Button modeButton, bool isSelected, bool isAvailable)
    {
        if (modeButton != null)
        {
            modeButton.interactable = isAvailable && !isSelected;
        }
    }

    private void HandleAttackModeClicked() => _boundTower?.SetMode(BabyDragonMode.Attack);
    private void HandleBuffModeClicked() => _boundTower?.SetMode(BabyDragonMode.Buff);
    private void HandleSkillTreeClicked() => _dragonWindow?.ToggleFromEntryPoint();
    private void HandleRelocateClicked() => _buildingPlacementController?.EnterMoveMode();
    private void HandleRemoveClicked() => _buildingPlacementController?.RemoveSelectedBuilding();

    // 선택을 해제하면 Update가 창을 자동으로 닫는다(UI_PopulationAllocationWindow.CloseWindow와 동일).
    private void HandleCloseClicked() => _buildingPlacementController?.Deselect();

    private void AddButtonListeners()
    {
        _attackModeButton?.onClick.AddListener(HandleAttackModeClicked);
        _buffModeButton?.onClick.AddListener(HandleBuffModeClicked);
        _skillTreeButton?.onClick.AddListener(HandleSkillTreeClicked);
        _relocateButton?.onClick.AddListener(HandleRelocateClicked);
        _removeButton?.onClick.AddListener(HandleRemoveClicked);
        _closeButton?.onClick.AddListener(HandleCloseClicked);
    }

    private void RemoveButtonListeners()
    {
        _attackModeButton?.onClick.RemoveListener(HandleAttackModeClicked);
        _buffModeButton?.onClick.RemoveListener(HandleBuffModeClicked);
        _skillTreeButton?.onClick.RemoveListener(HandleSkillTreeClicked);
        _relocateButton?.onClick.RemoveListener(HandleRelocateClicked);
        _removeButton?.onClick.RemoveListener(HandleRemoveClicked);
        _closeButton?.onClick.RemoveListener(HandleCloseClicked);
    }

    private void ApplyStaticLabels()
    {
        SetLabel(_attackModeButtonText, MODE_ATTACK_LOC_KEY);
        SetLabel(_buffModeButtonText, MODE_BUFF_LOC_KEY);
        SetLabel(_skillTreeButtonText, SKILL_TREE_LOC_KEY);
        SetLabel(_relocateButtonText, RELOCATE_LOC_KEY);
        SetLabel(_removeButtonText, REMOVE_LOC_KEY);
        SetLabel(_closeButtonText, CLOSE_LOC_KEY);
        SetLabel(_nightLockedText, NIGHT_LOCKED_LOC_KEY);
    }

    private static void SetLabel(TMP_Text text, string locKey)
    {
        if (text != null)
        {
            text.text = StringTable.GetString(locKey);
        }
    }
}
