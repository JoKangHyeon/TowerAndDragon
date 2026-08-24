using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 선택한 건물 하나의 산출량·인구 현황을 보여주고 인구를 배치/회수하는 창(이슈 #110).
// 산출 행은 건물 종류마다 의미가 달라 타입별로 채운다 - 생산시설은 자원 생산량,
// 타워는 가동 여부·충원율에 이어 실효 전투 스탯, 연구소는 정산 시 받는 연구 포인트.
// 건물마다 행 개수가 달라(슬라임 농장은 ProducedResourceType이 다중 비트, 타워는 스탯이 여럿)
// 산출 행을 풀링한다. 언제나 하나인 인구 행만 프리팹에 고정 배치해 값을 갱신한다
// (점령 창 UI_ConquestWindow와 같은 2단 방식).
//
// 전역 값(가용 인구)은 여기에 두지 않는다 - 상단 HUD가 상시 보여주고, 배치 버튼의 활성 여부로도
// 드러난다. 이 창은 선택한 건물 하나만 말한다.
//
// 배타 모드에는 한 방향으로만 참여한다 - 진입에 "어떤 건물인가"라는 인자가 필요해 인자 없는
// Open()을 쓸 수 없기 때문이다(SkillTargetingController와 같은 형태). 자세한 것은 파일 끝의
// IExclusiveMode 구현을 볼 것.
public class UI_PopulationAllocationWindow : MonoBehaviour, IExclusiveMode
{
    private const int POPULATION_STEP = 1;
    private const float PERCENT_MULTIPLIER = 100f;
    private const string VALUE_FORMAT = "{0} / {1}";

    // 소수 첫 자리까지만 보여준다(BabyDragonTooltipBuilder.DISTANCE_FORMAT와 같은 표기).
    private const string NUMBER_FORMAT = "{0:0.#}";

    // 체력·오라·용 속성 전환은 선택 변경도 인구 변경도 아니어서 이벤트가 오지 않는다 - 창이 열려 있는 동안
    // 이 간격으로 다시 그린다. 매 프레임 다시 그리면 행마다 문자열을 새로 만들어 버리는 값이 커진다.
    private const float REFRESH_INTERVAL_SECONDS = 0.5f;

    private const string ASSIGN_ALL_LOC_KEY =
        "population_allocation_assign_all";
    private const string UNASSIGN_ALL_LOC_KEY =
        "population_allocation_unassign_all";
    private const string ASSIGN_ONE_LOC_KEY =
        "building_window_assign_one";
    private const string UNASSIGN_ONE_LOC_KEY =
        "building_window_unassign_one";
    private const string CLOSE_LOC_KEY =
        "building_window_close";
    private const string POPULATION_LABEL_LOC_KEY =
        "building_window_population_label";
    private const string OPERATION_LABEL_LOC_KEY =
        "building_window_operation_label";
    private const string OPERATION_ON_FORMAT_LOC_KEY =
        "building_window_operation_on_format";
    private const string OPERATION_OFF_LOC_KEY =
        "building_window_operation_off";
    private const string RESEARCH_LABEL_LOC_KEY =
        "building_window_research_label";
    private const string NIGHT_LOCKED_LOC_KEY =
        "building_window_night_locked";

    private const string HEALTH_LABEL_LOC_KEY =
        "building_window_health_label";
    private const string ATTACK_LABEL_LOC_KEY =
        "building_window_attack_label";
    private const string ATTACK_INTERVAL_LABEL_LOC_KEY =
        "building_window_attack_interval_label";
    private const string DPS_LABEL_LOC_KEY =
        "building_window_dps_label";
    private const string RANGE_LABEL_LOC_KEY =
        "building_window_range_label";

    // 시간 단위는 언어마다 붙는 자리가 달라 서식을 스트링테이블에 둔다("1.6초" / "1.6s").
    private const string INTERVAL_VALUE_LOC_KEY =
        "building_window_interval_value";

    [SerializeField] private GameObject _windowRoot;
    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private ResearchManager _researchManager;

    [Tooltip("Esc로 닫을 때 튜토리얼 관문(CanCloseExclusive)을 묻는 데 쓴다.")]
    [SerializeField] private UIManager _uiManager;

    [Tooltip("성에서 캐릭터가 걸어 나오는 연출. 비워두면 연출만 생략되고 배치 자체는 그대로 동작한다.")]
    [SerializeField] private VillagerDispatchSystem _villagerDispatch;

    [Header("Keys")]
    [Tooltip("창을 닫는 키 - 보통 Esc.")]
    [SerializeField] private InputActionReference _closeAction;

    [SerializeField] private TMP_Text _buildingNameText;
    [SerializeField] private TMP_Text _nightLockedText;

    [Header("산출 행 - 건물 종류에 따라 개수가 달라 풀링한다")]
    [Tooltip("산출 행 프리팹(Slot__EnemyConquest).")]
    [SerializeField] private UI_ConquestInfoSlot _outputRowPrefab;
    [SerializeField] private Transform _outputRowContainer;

    [Tooltip("프리팹에 미리 배치해 둔 첫 산출 행 - 풀의 0번으로 재사용한다.")]
    [SerializeField] private UI_ConquestInfoSlot _outputRowSeed;

    [Header("항상 존재하는 행 - 값만 갱신한다")]
    [SerializeField] private UI_ConquestInfoSlot _populationRow;

    [Tooltip("인구 행 아이콘. 인구는 자원이 아니라 ResourceData가 없어 별도 지정한다.")]
    [SerializeField] private Sprite _populationIcon;

    [Tooltip("타워 가동 행 아이콘.")]
    [SerializeField] private Sprite _operationIcon;

    [Tooltip("연구소 연구 포인트 행 아이콘.")]
    [SerializeField] private Sprite _researchPointIcon;

    // 산출 행은 건물을 바꿔 가며 재사용하는 풀이고 UI_ConquestInfoSlot.Setup은 null 아이콘을 무시하므로,
    // 비워 두면 직전에 선택했던 생산시설의 자원 아이콘이 그대로 남는다. 다섯 개 모두 배선해야 한다.
    [Header("타워 스탯 행 아이콘 - 비워 두면 이전 건물의 아이콘이 남는다")]
    [SerializeField] private Sprite _healthIcon;
    [SerializeField] private Sprite _attackIcon;
    [SerializeField] private Sprite _attackIntervalIcon;
    [SerializeField] private Sprite _dpsIcon;
    [SerializeField] private Sprite _rangeIcon;

    [Header("버튼")]
    [SerializeField] private Button _assignButton;
    [SerializeField] private Button _unassignButton;
    [SerializeField] private Button _assignAllButton;
    [SerializeField] private Button _unassignAllButton;
    [SerializeField] private Button _closeButton;

    [SerializeField] private TMP_Text _assignButtonText;
    [SerializeField] private TMP_Text _unassignButtonText;
    [SerializeField] private TMP_Text _assignAllButtonText;
    [SerializeField] private TMP_Text _unassignAllButtonText;
    [SerializeField] private TMP_Text _closeButtonText;

    private Building _selectedBuilding;
    private IPopulationAllocationTarget _selectedTarget;

    // 선택한 건물의 체력. 타워 체력 행에만 쓰며, 체력이 없는 건물에서는 null로 남는다.
    private Health _selectedHealth;
    private ComponentPool<UI_ConquestInfoSlot> _outputRowPool;
    private bool _wasInputSuppressed;
    private float _nextRefreshTime;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private void Awake()
    {
        _outputRowPool = new ComponentPool<UI_ConquestInfoSlot>(
            _outputRowPrefab,
            _outputRowContainer,
            _outputRowSeed);

        AddButtonListeners();
        ApplyLocalizedLabels();

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(
                HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(
                HandleCycleChanged);
        }

        // 이 오브젝트는 늘 활성이고 _windowRoot만 켜고 끄므로 구독은 상시 유지된다
        // (액션을 켜는 것은 GlobalInputBootstrap의 몫이다 - UI_ResearchWindow와 같은 판단).
        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(
                HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(
                HandleCycleChanged);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    // BuildingPlacementController에는 선택 변경 이벤트가 없어(SelectedBuilding은 파생 getter)
    // 매 프레임 확인한다 - UI_BuildModeWindow 등 기존 소비자들과 같은 방식이다.
    private void Update()
    {
        if (!WiringGuard.Require(_buildingPlacementController, nameof(_buildingPlacementController), this))
        {
            return;
        }

        Building selectedBuilding =
            _buildingPlacementController.SelectedBuilding;
        bool inputSuppressed =
            _buildingPlacementController.InputSuppressed;

        if (_selectedBuilding != selectedBuilding ||
            _wasInputSuppressed != inputSuppressed)
        {
            _wasInputSuppressed = inputSuppressed;
            Bind(selectedBuilding);
            return;
        }

        // 타워 스탯·체력은 이벤트 없이 바뀌므로 창이 열려 있는 동안 주기적으로 다시 그린다.
        // timeScale이 게임 속도 설정에 따라 달라지므로 unscaledTime으로 잰다.
        if (IsWindowOpen && Time.unscaledTime >= _nextRefreshTime)
        {
            Refresh();
        }
    }

    private void Bind(Building building)
    {
        _selectedBuilding = building;
        _selectedTarget = building != null
            ? building.GetComponent<IPopulationAllocationTarget>()
            : null;
        _selectedHealth = building != null
            ? building.GetComponent<Health>()
            : null;

        // 점령 모드 등 다른 모드가 클릭을 점유한 동안에는 그쪽 창(Claim_window)이 같은 자리를
        // 쓰므로 이 창을 숨긴다.
        bool hasTarget =
            _selectedTarget != null &&
            _selectedTarget.IsInitialized &&
            !_wasInputSuppressed;

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(hasTarget);
        }

        if (hasTarget)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_selectedBuilding == null ||
            _selectedTarget == null ||
            _populationManager == null)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + REFRESH_INTERVAL_SECONDS;

        if (_buildingNameText != null)
        {
            _buildingNameText.text = ResolveBuildingName(_selectedBuilding);
        }

        RefreshOutputRows();

        if (_populationRow != null)
        {
            _populationRow.Setup(
                _populationIcon,
                Color.white,
                StringTable.GetString(POPULATION_LABEL_LOC_KEY),
                string.Format(
                    VALUE_FORMAT,
                    _selectedTarget.AssignedPopulation,
                    _selectedTarget.Capacity));
        }

        RefreshButtonState();
    }

    // 건물 종류별로 "산출"의 의미가 달라 여기서 갈라 채운다
    // (UI_BuildingSlot.ResolveName / ResolveBuildingName과 같은 타입 스위치 관례).
    // BabyDragonTower도 Tower 파생이므로 Factory·ResearchLab을 먼저 확인한다.
    private void RefreshOutputRows()
    {
        int usedCount = 0;

        if (_selectedBuilding is Factory factory)
        {
            foreach (ResourceType resourceType in
                factory.EnumerateProducedResourceTypes())
            {
                // 슬라임 농장처럼 산출 종류가 여러 개면 이 자리에서 실제로 나오지 않는 종류가 섞인다
                // (풋프린트 셀이 그 자원을 안 가진 경우). 최대 생산량이 0인 종류는 표시하지 않는다.
                int maxYield = factory.GetMaxYield(resourceType);
                if (maxYield == 0)
                {
                    continue;
                }

                SetOutputRow(
                    usedCount++,
                    ResolveResourceIcon(resourceType),
                    Color.white,
                    ResolveResourceName(resourceType),
                    string.Format(
                        VALUE_FORMAT,
                        factory.GetCurrentYield(resourceType),
                        maxYield));
            }
        }
        else if (_selectedBuilding is ResearchLab researchLab)
        {
            SetOutputRow(
                usedCount++,
                _researchPointIcon,
                Color.white,
                StringTable.GetString(RESEARCH_LABEL_LOC_KEY),
                string.Format(
                    VALUE_FORMAT,
                    ResolveResearchPointsPerDay(
                        researchLab, _selectedTarget.AssignedPopulation),
                    ResolveResearchPointsPerDay(
                        researchLab, _selectedTarget.Capacity)));
        }
        else if (_selectedBuilding is Tower tower)
        {
            SetOutputRow(
                usedCount++,
                _operationIcon,
                Color.white,
                StringTable.GetString(OPERATION_LABEL_LOC_KEY),
                ResolveTowerOperationText());

            usedCount = AppendTowerStatRows(tower, usedCount);
        }

        _outputRowPool.DeactivateFrom(usedCount);
    }

    // 지금 이 타워에 실제로 적용되는 수치를 낸다 - 데이터 원본이 아니라 인구 충원율·연구·어미용
    // 스킬트리·오라·지형 페널티가 모두 곱해진 값이다(BabyDragonTooltipBuilder.AppendAttackEffects와
    // 같은 원칙). 값이 성립하지 않는 행은 아예 내지 않는다 - 0을 보여주면 "공격력 0인 타워"로 읽힌다
    // (UI_TowerInfoPopup.RenderAttack과 같은 판단).
    private int AppendTowerStatRows(Tower tower, int usedCount)
    {
        if (_selectedHealth != null)
        {
            SetOutputRow(
                usedCount++,
                _healthIcon,
                Color.white,
                StringTable.GetString(HEALTH_LABEL_LOC_KEY),
                string.Format(
                    VALUE_FORMAT,
                    Mathf.RoundToInt(_selectedHealth.CurrentHealth),
                    Mathf.RoundToInt(_selectedHealth.MaxHealth)));
        }

        TowerAttack attack = tower.Attack;
        if (attack == null)
        {
            return usedCount;
        }

        bool hasDamage = attack.TryGetEffectiveDamage(out float damage);
        bool hasInterval = attack.TryGetEffectiveAttackInterval(out float interval);

        if (hasDamage)
        {
            SetOutputRow(
                usedCount++,
                _attackIcon,
                Color.white,
                StringTable.GetString(ATTACK_LABEL_LOC_KEY),
                string.Format(NUMBER_FORMAT, damage));
        }

        if (hasInterval)
        {
            SetOutputRow(
                usedCount++,
                _attackIntervalIcon,
                Color.white,
                StringTable.GetString(ATTACK_INTERVAL_LABEL_LOC_KEY),
                string.Format(
                    StringTable.GetString(INTERVAL_VALUE_LOC_KEY),
                    interval));
        }

        // 초당 피해는 적용되는 값이 아니라 위 둘에서 파생한 표시용 수치라 여기서 나눈다.
        // 간격이 0인 데이터는 나눗셈이 무한대가 되므로 행을 내지 않는다.
        if (hasDamage && hasInterval && interval > 0f)
        {
            SetOutputRow(
                usedCount++,
                _dpsIcon,
                Color.white,
                StringTable.GetString(DPS_LABEL_LOC_KEY),
                string.Format(NUMBER_FORMAT, damage / interval));
        }

        if (tower.Data != null && tower.Data.CanAttack)
        {
            SetOutputRow(
                usedCount++,
                _rangeIcon,
                Color.white,
                StringTable.GetString(RANGE_LABEL_LOC_KEY),
                string.Format(NUMBER_FORMAT, attack.EffectiveRange));
        }

        return usedCount;
    }

    private void SetOutputRow(int index, Sprite icon, Color iconColor, string label, string value)
    {
        _outputRowPool.Get(index).Setup(icon, iconColor, label, value);
    }

    private int ResolveResearchPointsPerDay(
        ResearchLab researchLab, int population)
    {
        return _researchManager != null
            ? _researchManager.PreviewResearchPointsPerDay(
                researchLab, population)
            : 0;
    }

    private string ResolveTowerOperationText()
    {
        if (_selectedTarget is not ITowerStaffing staffing ||
            !staffing.CanOperate)
        {
            return StringTable.GetString(OPERATION_OFF_LOC_KEY);
        }

        int staffingPercent = Mathf.RoundToInt(
            staffing.StaffingRatio * PERCENT_MULTIPLIER);

        return string.Format(
            StringTable.GetString(OPERATION_ON_FORMAT_LOC_KEY),
            staffingPercent);
    }

    // 자원 아이콘·이름은 데이터 에셋(ResourceData)이 단일 출처 - 카탈로그에서 종류로 조회한다
    // (UI_ConquestWindow.ResolveResourceIcon과 같은 경로).
    private bool TryGetResourceData(ResourceType type, out ResourceData data)
    {
        data = null;
        return _resourceManager != null &&
            _resourceManager.Catalog != null &&
            _resourceManager.Catalog.TryGet(type, out data);
    }

    private Sprite ResolveResourceIcon(ResourceType type)
    {
        return TryGetResourceData(type, out ResourceData data)
            ? data.Icon
            : null;
    }

    // 카탈로그에 없는 종류는 종류 이름을 그대로 쓴다
    // (점령 보상 슬롯 UI_ConquestWindow.SpawnRewardSlot과 동일한 대체 방식).
    private string ResolveResourceName(ResourceType type)
    {
        return TryGetResourceData(type, out ResourceData data)
            ? StringTable.GetString(data.NameLocKey)
            : type.ToString();
    }

    private void RefreshButtonState()
    {
        bool hasValidTarget =
            _selectedBuilding != null &&
            _selectedTarget != null &&
            _selectedTarget.IsInitialized;

        bool canEdit = hasValidTarget && IsDay;
        bool canAssign =
            canEdit &&
            _populationManager != null &&
            _populationManager.AvailablePopulation >= POPULATION_STEP &&
            _selectedTarget.AvailableCapacity >= POPULATION_STEP;
        bool canUnassign =
            canEdit &&
            (_selectedTarget is TowerPopulation tp
                ? tp.RealAssignedPopulation >= POPULATION_STEP
                : _selectedTarget.AssignedPopulation >= POPULATION_STEP);

        SetInteractable(_assignButton, canAssign);
        SetInteractable(_assignAllButton, canAssign);
        SetInteractable(_unassignButton, canUnassign);
        SetInteractable(_unassignAllButton, canUnassign);

        // 버튼이 회색인 이유(밤)를 알려준다 - 정원/가용 한도는 숫자 행에서 이미 드러난다.
        if (_nightLockedText != null)
        {
            _nightLockedText.gameObject.SetActive(hasValidTarget && !IsDay);
        }
    }

    // 배치/회수 요청량 클램프는 PopulationAssignmentRules가 단일 출처다
    // (인구 배치 모드 WorkerModeController의 클릭 처리와 같은 규칙을 쓴다).
    private void AssignOne()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (CanEditTarget())
        {
            RunAssign(POPULATION_STEP);
        }
    }

    private void UnassignOne()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (CanEditTarget())
        {
            RunUnassign(POPULATION_STEP);
        }
    }

    private void AssignAll()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (CanEditTarget())
        {
            RunAssign(_selectedTarget.AvailableCapacity);
        }
    }

    private void UnassignAll()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (CanEditTarget())
        {
            int amount = _selectedTarget is TowerPopulation tp
                ? tp.RealAssignedPopulation
                : _selectedTarget.AssignedPopulation;
            RunUnassign(amount);
        }
    }

    // 네 버튼이 같은 뒤처리(연출 통지)를 하므로 실제 호출을 여기 둘로 모았다.
    // 연출은 "실제로 몇 명이 움직였는지"를 조작 전후 값의 차이로 관측한다 - 클램프 규칙
    // (PopulationAssignmentRules)을 여기서 다시 계산하면 규칙이 두 곳으로 갈라지기 때문이다.
    private void RunAssign(int requested)
    {
        int assignedBefore = _selectedTarget.AssignedPopulation;

        if (PopulationAssignmentRules.TryAssignClamped(_selectedTarget, _populationManager, requested) &&
            _villagerDispatch != null)
        {
            _villagerDispatch.NotifyAllocationChanged(_selectedTarget, assignedBefore);
        }
    }

    private void RunUnassign(int requested)
    {
        int assignedBefore = _selectedTarget.AssignedPopulation;

        if (PopulationAssignmentRules.TryUnassignClamped(_selectedTarget, requested) &&
            _villagerDispatch != null)
        {
            _villagerDispatch.NotifyAllocationChanged(_selectedTarget, assignedBefore);
        }
    }

    // 선택을 해제하면 Update가 창을 자동으로 닫는다(ResearchLabDebugGUI의 ESC 처리와 동일).
    private void CloseWindow()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.Deselect();
        }
    }

    private bool CanEditTarget()
    {
        return _selectedBuilding != null &&
            _selectedTarget != null &&
            _selectedTarget.IsInitialized &&
            IsDay;
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        Refresh();
    }

    private void HandleCycleChanged(CycleManager.CycleState state)
    {
        // 밤이 되면 생산량 표시는 그대로 두고 조작만 잠근다.
        RefreshButtonState();
    }

    private void AddButtonListeners()
    {
        _assignButton?.onClick.AddListener(AssignOne);
        _unassignButton?.onClick.AddListener(UnassignOne);
        _assignAllButton?.onClick.AddListener(AssignAll);
        _unassignAllButton?.onClick.AddListener(UnassignAll);
        _closeButton?.onClick.AddListener(CloseWindow);
    }

    private void RemoveButtonListeners()
    {
        _assignButton?.onClick.RemoveListener(AssignOne);
        _unassignButton?.onClick.RemoveListener(UnassignOne);
        _assignAllButton?.onClick.RemoveListener(AssignAll);
        _unassignAllButton?.onClick.RemoveListener(UnassignAll);
        _closeButton?.onClick.RemoveListener(CloseWindow);
    }

    private void ApplyLocalizedLabels()
    {
        SetLabel(_assignButtonText, ASSIGN_ONE_LOC_KEY);
        SetLabel(_unassignButtonText, UNASSIGN_ONE_LOC_KEY);
        SetLabel(_assignAllButtonText, ASSIGN_ALL_LOC_KEY);
        SetLabel(_unassignAllButtonText, UNASSIGN_ALL_LOC_KEY);
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

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static string ResolveBuildingName(Building building)
    {
        if (building is Tower tower && tower.Data != null)
        {
            return StringTable.GetString(tower.Data.NameLocKey);
        }

        if (building is Factory factory && factory.Data != null)
        {
            return StringTable.GetString(factory.Data.NameLocKey);
        }

        if (building is ResearchLab researchLab && researchLab.Data != null)
        {
            return StringTable.GetString(researchLab.Data.NameLocKey);
        }

        return string.Empty;
    }

    private bool IsWindowOpen => _windowRoot != null && _windowRoot.activeSelf;

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        // 선택한 건물이 없으면 이 Esc는 이 창의 몫이 아니다 - 그때의 Esc는 설정 창을 연다.
        if (!IsWindowOpen)
        {
            return;
        }

        // 안내가 이 창 안을 가리키는 중이면 Esc로 닫지 못하게 막는다.
        if (_uiManager != null && !_uiManager.CanCloseExclusive(this))
        {
            return;
        }

        CloseAndDeselect();
    }

    // CloseWindow는 선택만 해제하고 표시는 다음 Update의 Bind가 끈다. 배타 조정은 닫은 직후
    // IsOpen을 읽으므로(UIManager.CloseAllExcept), 여기서 Bind(null)까지 불러 같은 프레임에 맞춘다.
    private void CloseAndDeselect()
    {
        CloseWindow();
        Bind(null);
    }

    bool IExclusiveMode.IsOpen => IsWindowOpen;

    // 진입에 "어떤 건물인가"라는 인자가 필요해 인자 없는 Open()으로 표현할 수 없다.
    // 이 창은 "다른 모드가 열리면 닫힌다"는 한 방향으로만 레지스트리에 참여하고,
    // 여는 쪽은 건물 선택(BuildingPlacementController)이 담당한다.
    // SkillTargetingController가 같은 이유로 같은 형태를 쓴다.
    void IExclusiveMode.Open() { }

    void IExclusiveMode.Close() => CloseAndDeselect();
}
