using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
    private const int RECT_CORNER_COUNT = 4;

    // 두 끝값의 가운데를 잡는 비율. 피벗을 사각형 한가운데에 두는 데도, 안내 사각형의 중심을
    // 위·아래 행의 중점으로 잡는 데도 같은 뜻으로 쓴다.
    private const float CENTER_RATIO = 0.5f;

    // 안내가 공격속도~초당피해를 함께 가리킬 때 쓰는 런타임 전용 빈 오브젝트의 이름(디버깅용).
    private const string ATTACK_ROWS_GUIDE_NAME = "AttackRowsGuideRect";

    private static readonly Vector2 CENTER_PIVOT = new Vector2(CENTER_RATIO, CENTER_RATIO);
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

    [Tooltip("가동 상태 행(StatusRow). 타워에만 있는 값이라 타워가 아닌 건물에서는 통째로 숨긴다.")]
    [SerializeField] private UI_ConquestInfoSlot _statusRow;

    [Tooltip("인구 행 아이콘. 인구는 자원이 아니라 ResourceData가 없어 별도 지정한다.")]
    [SerializeField] private Sprite _populationIcon;

    // 가동 행 아이콘은 여기서 지정하지 않는다 - StatusRow는 타워 전용 고정 행이라 아이콘이 바뀌지 않고,
    // 프리팹의 Icon 오브젝트가 스프라이트를 들고 있다(_statusRow의 _iconImage도 비워 둔다).

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

    // 같은 자리에 뜨는 새끼용 관리창(UI_BabyDragonManageWindow)과 같은 연출을 쓴다 -
    // 두 창이 건물 종류에 따라 번갈아 뜨는데 한쪽만 슬라이드하면 서로 다른 창처럼 읽힌다.
    [Header("패널 슬라이드 연출")]
    [SerializeField] private float _slideDuration = 0.4f;

    [Tooltip("열릴 때 시작 오프셋(홈 기준). 여기서 홈으로 슬라이드 인.")]
    [SerializeField] private Vector2 _openFromOffset = new Vector2(100f, 0f);

    [Tooltip("닫힐 때 도착 오프셋(홈 기준). 홈에서 여기로 슬라이드 아웃 후 비활성화.")]
    [SerializeField] private Vector2 _closeToOffset = new Vector2(500f, 0f);

    private Building _selectedBuilding;
    private IPopulationAllocationTarget _selectedTarget;

    // 선택한 건물의 체력. 타워 체력 행에만 쓰며, 체력이 없는 건물에서는 null로 남는다.
    private Health _selectedHealth;
    private readonly Vector3[] _guideCornerBuffer = new Vector3[RECT_CORNER_COUNT];

    private ComponentPool<UI_ConquestInfoSlot> _outputRowPool;

    // 이번 갱신에서 공격속도·초당피해 행이 실제로 놓인 자리. 행 번호는 건물마다 달라지고
    // 체력·공격력이 빠지는 타워도 있어 고정할 수 없으므로, 채우면서 그때그때 붙잡아 둔다.
    private RectTransform _attackSpeedRowRect;
    private RectTransform _dpsRowRect;

    // 위 두 행을 함께 덮는 안내용 사각형. 안내는 RectTransform 하나만 가리킬 수 있는데
    // 두 행을 아우르는 오브젝트가 계층에 없어 여기서 만들어 쓴다
    // (UI_GuideOverlay가 구멍 차단막을 만드는 것과 같은 방식).
    private RectTransform _attackRowsGuideRect;
    private bool _wasInputSuppressed;

    // 창이 열려 있는 동안에만 도는 갱신 루프의 수명. 창이 닫히면 다음 간격을 기다리지 않고
    // 그 자리에서 끊는다 - 닫는 프레임에 마지막 Refresh가 한 번 더 도는 일이 없도록.
    private CancellationTokenSource _refreshLoopCts;

    private RectTransform _windowRect;
    private Vector2 _homePos;
    private Tween _windowTween;

    // 닫히는 애니메이션이 도는 동안에도 _windowRoot는 아직 활성이므로 activeSelf로는 열림을 판정할 수 없다.
    // 상태는 이 플래그가 들고 있는다(UI_BabyDragonManageWindow._isOpen과 같은 이유).
    private bool _isOpen;

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

        if (_windowRoot != null)
        {
            // 홈 위치는 비활성화 전에 잡아 둔다 - 슬라이드는 이 자리를 기준으로 오간다.
            _windowRect = _windowRoot.GetComponent<RectTransform>();
            _homePos = _windowRect.anchoredPosition;
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

        if (WiringGuard.Require(_buildingPlacementController, nameof(_buildingPlacementController), this))
        {
            _buildingPlacementController.SelectedBuildingChanged.AddListener(HandleSelectedBuildingChanged);
            _buildingPlacementController.InteractionStateChanged.AddListener(HandleInteractionStateChanged);

            // 구독 직후 현재 값 1회 반영. ClosePanel에 _isOpen 가드가 있어 이미 닫힌 창에
            // 닫힘 연출이 돌지 않는다.
            _wasInputSuppressed = _buildingPlacementController.InputSuppressed;
            Bind(_buildingPlacementController.SelectedBuilding);
        }

        // 구독 직후 현재 언어로 한 번 반영한다 - 창이 닫혀 있는 동안 바뀐 언어를 놓치지 않는다
        // (LocalizedText와 같은 방식).
        StringTable.OnLanguageChanged += HandleLanguageChanged;
        HandleLanguageChanged();
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

        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.SelectedBuildingChanged.RemoveListener(HandleSelectedBuildingChanged);
            _buildingPlacementController.InteractionStateChanged.RemoveListener(HandleInteractionStateChanged);
        }

        StringTable.OnLanguageChanged -= HandleLanguageChanged;

        // 이 오브젝트는 늘 활성이지만, 씬 전환이나 계층 비활성화로 여기 닿았을 때 루프가 살아남지
        // 않게 한다. 파괴 취소(GetCancellationTokenOnDestroy)는 파괴 시점에만 오므로 그것만으로는 부족하다.
        StopRefreshLoop();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        StopRefreshLoop();
        _windowTween?.Kill();
    }

    // 그리드에서 고른 건물이 바뀌었다.
    private void HandleSelectedBuildingChanged(Building building)
    {
        _wasInputSuppressed = _buildingPlacementController.InputSuppressed;
        Bind(building);
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
        Bind(_selectedBuilding);
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

        if (_windowRoot == null)
        {
            return;
        }

        _windowTween?.Kill();

        _windowRoot.SetActive(true);
        _windowRect.anchoredPosition = _homePos + _openFromOffset;
        _windowTween = _windowRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_windowRoot);

        StartRefreshLoop();
    }

    private void ClosePanel()
    {
        // 이미 닫혀 있으면 닫힘 연출을 다시 돌리지 않는다 - 비활성 상태의 창을 홈 밖으로 밀어 두고
        // 끝나 다음 열기가 엉뚱한 자리에서 시작한다.
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        StopRefreshLoop();

        if (_windowRoot == null)
        {
            return;
        }

        _windowTween?.Kill();
        _windowTween = _windowRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_windowRoot)
            .OnComplete(() => _windowRoot.SetActive(false));
    }

    // 체력·오라·용 속성 전환은 선택 변경도 인구 변경도 아니어서 이벤트가 오지 않는다 -
    // 창이 열려 있는 동안에만 이 간격으로 다시 그린다.
    private void StartRefreshLoop()
    {
        // 열기가 겹쳐 들어와도 루프가 두 벌 돌지 않게 한다(0.5초가 0.25초가 되는 식).
        StopRefreshLoop();

        _refreshLoopCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        RunRefreshLoopAsync(_refreshLoopCts.Token).Forget();
    }

    private void StopRefreshLoop()
    {
        if (_refreshLoopCts == null)
        {
            return;
        }

        _refreshLoopCts.Cancel();
        _refreshLoopCts.Dispose();
        _refreshLoopCts = null;
    }

    private async UniTaskVoid RunRefreshLoopAsync(CancellationToken token)
    {
        // 연 직후의 첫 그리기는 Bind가 이미 했으므로 기다렸다가 다음 것부터 그린다.
        while (!token.IsCancellationRequested)
        {
            // timeScale이 게임 속도 설정(GameSpeedManager)에 따라 0이 되므로 unscaled로 잰다 -
            // scaled로 재면 일시정지 중에 창이 영영 갱신되지 않는다(옛 Time.unscaledTime과 같은 이유).
            await UniTask.Delay(
                TimeSpan.FromSeconds(REFRESH_INTERVAL_SECONDS),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);

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
        // 지난 건물의 자리를 물려주지 않는다 - 타워가 아니면 이 행들은 아예 그려지지 않는다.
        _attackSpeedRowRect = null;
        _dpsRowRect = null;

        int usedCount = 0;

        RefreshStatusRow();

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
            usedCount = AppendTowerStatRows(tower, usedCount);
        }

        _outputRowPool.DeactivateFrom(usedCount);
    }

    // 가동 여부는 InfoZone의 산출 행이 아니라 창 위쪽 고정 행에 낸다 - 건물마다 개수가 달라지는
    // 풀링 행과 달리 자리가 고정이라, 어떤 타워를 골라도 늘 같은 위치에서 읽힌다.
    // 타워에만 있는 값이므로 생산시설·연구소에서는 행 자체를 숨긴다.
    private void RefreshStatusRow()
    {
        if (_statusRow == null)
        {
            return;
        }

        bool isTower = _selectedBuilding is Tower;
        _statusRow.gameObject.SetActive(isTower);

        if (!isTower)
        {
            return;
        }

        // 아이콘은 null로 넘긴다 - Setup은 null 아이콘을 무시하므로 프리팹에 박아 둔 고정 아이콘이 그대로 남는다.
        _statusRow.Setup(
            null,
            Color.white,
            StringTable.GetString(OPERATION_LABEL_LOC_KEY),
            ResolveTowerOperationText());
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
            _attackSpeedRowRect = SetOutputRow(
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
            _dpsRowRect = SetOutputRow(
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

    /// <summary>
    /// 튜토리얼이 "인구를 채우면 공격이 빨라진다"를 가리킬 자리 - 공격속도 행과 초당피해 행을 함께 덮는다.
    /// 두 행은 언제나 붙어 있어 사각형 하나로 덮인다.
    ///
    /// 지금 고른 건물이 타워가 아니거나 그 행들이 그려지지 않았으면 false를 준다 -
    /// 그때 빈 사각형을 돌려주면 안내가 엉뚱한 자리에 구멍을 뚫는다.
    /// </summary>
    public bool TryGetAttackSpeedRowsRect(out RectTransform rowsRect)
    {
        rowsRect = null;

        RectTransform first = _attackSpeedRowRect;
        RectTransform last = _dpsRowRect != null ? _dpsRowRect : _attackSpeedRowRect;

        if (first == null || last == null || !first.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform parent = EnsureAttackRowsGuideRect();
        if (parent == null)
        {
            return false;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        Encapsulate(first, ref min, ref max);
        Encapsulate(last, ref min, ref max);

        _attackRowsGuideRect.anchoredPosition = Vector2.Lerp(min, max, CENTER_RATIO);
        _attackRowsGuideRect.sizeDelta = max - min;

        rowsRect = _attackRowsGuideRect;
        return true;
    }

    private void Encapsulate(RectTransform target, ref Vector2 min, ref Vector2 max)
    {
        target.GetWorldCorners(_guideCornerBuffer);

        foreach (Vector3 corner in _guideCornerBuffer)
        {
            Vector2 local = _attackRowsGuideRect.parent.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
    }

    /// <summary>
    /// 두 행을 아우르는 빈 사각형을 만들어 둔다. 계층에 그런 오브젝트가 없어 런타임에 만든다.
    /// <see cref="LayoutElement.ignoreLayout"/>을 켜는 이유: 산출 행 컨테이너는 레이아웃 그룹이 배치하므로,
    /// 그냥 자식으로 넣으면 이 빈 칸이 한 줄을 차지해 행들이 밀린다.
    /// </summary>
    private RectTransform EnsureAttackRowsGuideRect()
    {
        if (_attackRowsGuideRect != null)
        {
            return _attackRowsGuideRect;
        }

        if (_outputRowContainer is not RectTransform container)
        {
            return null;
        }

        var holder = new GameObject(ATTACK_ROWS_GUIDE_NAME, typeof(RectTransform), typeof(LayoutElement));
        holder.GetComponent<LayoutElement>().ignoreLayout = true;

        _attackRowsGuideRect = (RectTransform)holder.transform;
        _attackRowsGuideRect.SetParent(container, false);
        _attackRowsGuideRect.anchorMin = container.pivot;
        _attackRowsGuideRect.anchorMax = container.pivot;
        _attackRowsGuideRect.pivot = CENTER_PIVOT;

        return _attackRowsGuideRect;
    }

    // 만든 행의 자리를 돌려준다 - 안내가 특정 행을 가리켜야 해서 붙잡아 둘 곳이 필요하다.
    // 대부분의 호출부는 반환값을 쓰지 않는다.
    //
    // as가 아니라 캐스트인 이유: 행 프리팹의 루트가 RectTransform이 아니게 되면 조용히 null이 되어
    // 안내만 말없이 빠지는 대신, 그 자리에서 예외로 드러나야 한다.
    private RectTransform SetOutputRow(
        int index, Sprite icon, Color iconColor, string label, string value)
    {
        UI_ConquestInfoSlot slot = _outputRowPool.Get(index);
        slot.Setup(icon, iconColor, label, value);

        return (RectTransform)slot.transform;
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

    // 선택을 해제하면 Deselect의 알림(SelectedBuildingChanged)이 창을 닫는다(ResearchLabDebugGUI의 ESC 처리와 동일).
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

    // 버튼 라벨·밤 잠금 안내는 한 번 써 놓고 마는 글자라 언어가 바뀌면 여기서 다시 칠한다.
    // 값 행은 Refresh가 주기적으로 다시 그리지만, 언어 전환은 즉시 보이는 편이 낫다.
    private void HandleLanguageChanged()
    {
        ApplyLocalizedLabels();
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

    private bool IsWindowOpen => _isOpen;

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

    // CloseWindow의 Deselect()가 SelectedBuildingChanged를 동기 발화해 Bind(null)이 이미 돌았겠지만,
    // 배타 조정은 닫은 직후 IsOpen을 읽으므로(UIManager.CloseAllExcept) 여기서도 한 번 더 불러
    // 확실히 한다(멱등이라 중복 호출이 무해하다).
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
