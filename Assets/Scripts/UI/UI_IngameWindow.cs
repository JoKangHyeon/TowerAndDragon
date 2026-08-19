using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 인게임 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소를 한곳에서 관리한다.
/// - 낮/밤 심볼(Symbol_Day) 전환과 하단 컨트롤 표시 (CycleManager.OnCycleChanged 구독)
/// - Panel_Label/Day 날짜 텍스트 표시 (CycleManager.OnDayReady 구독)
/// - Panel_TopLeft 자원 보유량 표시 (ResourceManager 이벤트 구독)
/// - People_amount 인구 표시: 가용/총 (PopulationManager 이벤트 구독)
/// 활성화 시점의 현재 상태도 즉시 반영한다.
/// </summary>
public class UI_IngameWindow : MonoBehaviour
{
    // 인구 표기 형식: 가용 인구 / 총(최대) 인구.
    private const string POPULATION_FORMAT = "{0}/{1}";
    private const string POPULATION_WITH_LOSS_FORMAT =
        "{0}/{1}<color=#{2}>(-{3})</color>";

    // 날짜 표기 형식은 스트링테이블에서 가져온다(언어별 문구·{0} 위치가 다름).
    // 값 예) en_us: "DAY {0}" / ko_kr: "{0} 일"
    private const string DAY_LOC_KEY = "main_day";
    private static string DayFormat => StringTable.GetString(DAY_LOC_KEY);

    // 밤 진입 확인 문구. {0} = 모자란 식량, {1} = 굶어 죽을 시민 수.
    // 서식은 확인창(UI_ConfirmPopup)이 채우므로 여기서는 key만 지목한다.
    private const string STARVATION_CONFIRM_LOC_KEY = "night_confirm_starvation_message";

    // 자원 표기(보유량 + 순증감)의 서식과 색 판정은 ResourceAmountFormatter가 갖는다 -
    // 용 창의 슬라임 칸(ResourceAmountView)과 같은 규칙으로 보이게 하기 위함.
    private static readonly Color PRODUCTION_COLOR_DEFAULT = ResourceAmountFormatter.GAIN_COLOR_DEFAULT;
    private static readonly Color LOSS_COLOR_DEFAULT = ResourceAmountFormatter.LOSS_COLOR_DEFAULT;

    // 웨이브 진행 바가 가득 찰 때까지의 일수. 이 값째 클리어에 슬라이더가 가득 찬다.
    private const int WAVE_FILL_LENGTH = 6;

    // 자원 표시 1칸: 자원 종류 ↔ 수량 텍스트(+ 선택적으로 아이콘).
    [System.Serializable]
    private struct ResourceSlot
    {
        public ResourceType Type;
        public TMP_Text AmountText;
        [Tooltip("비워두면 프리팹에 배치된 아이콘을 그대로 쓴다. 지정하면 ResourceData의 아이콘으로 덮어쓴다.")]
        [WiringOptional]
        public Image IconImage;
    }

    [SerializeField] private CycleManager _cycleManager;

    [Header("낮/밤 심볼")]
    [Tooltip("낮에 켜질 아이콘.")]
    [SerializeField] private GameObject _imageDay;
    [Tooltip("밤에 켜질 아이콘.")]
    [SerializeField] private GameObject _imageLight;

    [Header("날짜 표시")]
    [Tooltip("날짜 텍스트(Day)")]
    [SerializeField] private TMP_Text _dayText;

    [Header("낮/밤 하단 컨트롤")]
    [Tooltip("낮에 켜질 버튼(다음 밤으로 진행).")]
    [SerializeField] private GameObject _buttonNextNight;
    [Tooltip("밤에 켜질 속도 조절 UI.")]
    [SerializeField] private GameObject _speedSetting;
    [Tooltip("굶주림처럼 되돌릴 수 없는 결과가 예상될 때 띄우는 확인창. " +
        "비우면 확인 없이 곧장 밤으로 넘어간다(기존 동작).")]
    [WiringOptional]
    [SerializeField] private UI_ConfirmPopup _confirmPopup;

    [Header("자원 표시 (Panel_TopLeft)")]
    [SerializeField] private ResourceManager _resourceManager;
    [Tooltip("자원 종류별 수량 텍스트. 기본 3종(식량·통나무·돌) + 특화 4종만 배선한다 - " +
        "슬라임 5종은 칸을 따로 두지 않고 _slimeSummary가 기호 하나로 요약한다.")]
    [SerializeField] private ResourceSlot[] _resourceSlots;
    [Tooltip("하루 예상 증감 표기용. 각 자원 보유량 옆에 (+증가) 또는 (-감소)로 노출한다.")]
    [FormerlySerializedAs("_productionForecast")]
    [SerializeField] private ResourceForecast _resourceForecast;
    [Tooltip("인구 1명당 식량 소모량의 출처. PopulationUpkeepSystem·ResourceForecast와 같은 에셋을 " +
        "연결해야 표시값과 실제 차감액이 어긋나지 않는다.")]
    [SerializeField] private EconomyBalanceData _economyBalance;
    [Tooltip("하루 순증가 글씨 색(연두색).")]
    [SerializeField] private Color _productionColor = PRODUCTION_COLOR_DEFAULT;
    [Tooltip("자원 칸 툴팁을 그릴 표시기. 각 자원 행의 UI_TooltipTrigger에 주입한다.")]
    [SerializeField] private UI_TooltipPresenter _tooltipPresenter;
    [Tooltip("특화자원 행 오른쪽 끝의 슬라임 요약 칸. 슬라임 5종은 _resourceSlots에 넣지 않고 " +
        "이 칸이 기호 하나로 요약하고, 자세한 값은 호버 패널에서 보여준다.")]
    [SerializeField] private UI_SlimeSummaryIndicator _slimeSummary;

    // _resourceSlots와 인덱스가 대응하는 툴팁 트리거 캐시.
    private UI_TooltipTrigger[] _resourceTooltipTriggers;

    // 툴팁 내역을 받아오는 재사용 버퍼(ResourceForecast.CollectBreakdown이 매번 덮어쓴다).
    private readonly List<ResourceForecastEntry> _forecastBreakdownBuffer = new();

    [Header("인구 표시 (Panel_peopleAmount)")]
    [SerializeField] private PopulationManager _populationManager;
    [Tooltip("인구 수량 텍스트(People_amount). 가용/총으로 표시된다.")]
    [SerializeField] private TMP_Text _populationText;

    [SerializeField] private Button _buttonBuildMode;
    [SerializeField] private UI_BuildModeWindow _buildModeWindow;

    [Header("인구 배치 (Panel_BottomCenter/Buttons)")]
    [Tooltip("인구 배치 모드(Worker Mode) 토글 버튼.")]
    [SerializeField] private Button _buttonWorkerMode;
    [Tooltip("인구 배치 모드 컨트롤러. 버튼 클릭 시 모드를 토글한다.")]
    [SerializeField] private WorkerModeController _workerModeController;

    [Header("점령 (Panel_BottomRight)")]
    [Tooltip("점령 모드 토글 버튼.")]
    [SerializeField] private Button _buttonConquest;
    [Tooltip("점령 정보 창. 버튼 클릭 시 점령 모드를 토글한다.")]
    [SerializeField] private UI_ConquestWindow _conquestWindow;

    [Header("용 (Panel_BottomRight)")]
    [Tooltip("용 창 토글 버튼(Button_Dragon).")]
    [FormerlySerializedAs("_buttonDragonSkill")]
    [SerializeField] private Button _buttonDragon;
    [Tooltip("용 창. 어미용(스킬트리 포함)/새끼용 탭을 토글한다 - 스킬트리는 " +
        "UI_DragonSkillWindow 독립 창에서 이 창의 어미용 탭으로 옮겨갔다.")]
    [SerializeField] private UI_DragonWindow _dragonWindow;

    [Header("연구 (Panel_BottomRight)")]
    [Tooltip("연구 창 토글 버튼.")]
    [SerializeField] private Button _buttonResearch;
    [Tooltip("연구 창. 버튼 클릭 시 연구 창을 토글한다.")]
    [SerializeField] private UI_ResearchWindow _researchWindow;

    [Header("설정 (Button_Setting)")]
    [Tooltip("설정 창 토글 버튼.")]
    [SerializeField] private Button _buttonSetting;
    [Tooltip("설정 창(Config_window). 버튼 클릭 시 토글한다.")]
    [SerializeField] private UI_ConfigWindow _configWindow;

    [Header("도움말 (Button_Help)")]
    [Tooltip("도감형 도움말 창 토글 버튼.")]
    [SerializeField] private Button _buttonHelp;
    [Tooltip("도움말 창(Help_window_Blocker). 버튼 클릭 시 토글한다.")]
    [SerializeField] private UI_HelpWindow _helpWindow;

    [Header("웨이브 진행 바 (Panel_TopCenter/BossWave)")]
    [Tooltip("웨이브 진행 슬라이더(Slider_wave).")]
    [SerializeField] private Slider _waveSlider;
    [Tooltip("현재 진행 위치를 가리키는 점(Icon_Point).")]
    [SerializeField] private RectTransform _wavePoint;
    [Tooltip("일수별 Point 위치. 순서대로 [0]=Day1(line_01, 시작/리셋 위치) … [6]=Day7(Icon_Boss). 총 7개.")]
    [SerializeField] private RectTransform[] _wavePointStops;
    [Tooltip("슬라이더/Point 이동 트윈 시간(초).")]
    [SerializeField] private float _waveTweenDuration = 0.4f;

    private Vector2 _wavePointStartPos;

    // 가득 찬 다음 날(보스 격파 다음 날)에는 진행 바를 시작점으로 되돌린다. 즉 실제 한 주기는
    // '가득 찬 뒤 되돌아가는 하루'까지 포함해 WAVE_FILL_LENGTH + 1일이다.
    private int _resetInterval;

    // 진행 바가 마지막으로 반영한 클리어 수. 낮 진입(OnDayReady)마다 일차로부터 유도한 값과 비교해,
    // 이미 같은 값이면 아무것도 하지 않는다 - 밤 종료 트윈이 도는 중에 이어서 오는 낮 진입이
    // 트윈을 끊고 순간이동시키지 않게 하기 위한 캐시다.
    private int _shownClearedWaves;

    private void Awake()
    {
        // 시작/리셋 위치는 눈금 배열의 첫 칸(Day1, line_01)으로 잡는다.
        if (_wavePointStops != null && _wavePointStops.Length > 0 && _wavePointStops[0] != null)
        {
            _wavePointStartPos = _wavePointStops[0].anchoredPosition;
        }
        else if (_wavePoint != null)
        {
            _wavePointStartPos = _wavePoint.anchoredPosition;
        }

        _resetInterval = WAVE_FILL_LENGTH + 1;

        // '다음 밤으로' 버튼: 누르면 낮을 종료하고 밤을 시작한다.
        if (_buttonNextNight != null)
        {
            Button button = _buttonNextNight.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(GoToNight);
            }
        }

        // 건설 버튼: 누를 때마다 건설 모드를 켜고 끈다(토글). 실제 모드 처리는 건설 창이 담당.
        if (_buttonBuildMode != null && _buildModeWindow != null)
        {
            _buttonBuildMode.onClick.AddListener(_buildModeWindow.ToggleFromEntryPoint);
        }

        // 인구 배치 버튼: 누를 때마다 인구 배치 모드를 켜고 끈다(토글). 실제 모드 처리는 컨트롤러가 담당.
        if (_buttonWorkerMode != null && _workerModeController != null)
        {
            _buttonWorkerMode.onClick.AddListener(ToggleWorkerMode);
        }

        // 점령 버튼: 누를 때마다 점령 모드를 켜고 끈다(토글). 실제 모드 처리는 점령 창이 담당.
        if (_buttonConquest != null && _conquestWindow != null)
        {
            _buttonConquest.onClick.AddListener(_conquestWindow.ToggleConquestMode);
        }

        if (_buttonDragon != null && _dragonWindow != null)
        {
            _buttonDragon.onClick.AddListener(_dragonWindow.ToggleFromEntryPoint);
        }

        if (_buttonResearch != null && _researchWindow != null)
        {
            _buttonResearch.onClick.AddListener(_researchWindow.ToggleFromEntryPoint);
        }

        if (_buttonSetting != null && _configWindow != null)
        {
            _buttonSetting.onClick.AddListener(_configWindow.ToggleFromEntryPoint);
        }

        // 클릭음은 창의 Open/Close가 내므로 여기서 SoundManager를 중복으로 부르지 않는다.
        if (_buttonHelp != null && _helpWindow != null)
        {
            _buttonHelp.onClick.AddListener(_helpWindow.ToggleFromEntryPoint);
        }
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(ApplyCycle);
            ApplyCycle(_cycleManager.CurrentCycle);
        }

        ResolveResourceTooltipTriggers();

        // 슬라임 요약 칸은 자원 출처를 스스로 배선하지 않는다(프리팹 안쪽이라 씬 참조를 넣을 수 없다).
        if (_slimeSummary != null)
        {
            _slimeSummary.Construct(_resourceManager, _resourceForecast);
        }

        // 예측이 바뀌면(건물/인구/버프/지형 변경) 보유량 옆 증감 표기와 툴팁을 다시 그린다.
        // 자원 보유량 참조와 무관하게 구독해야, ResourceManager 미연결 씬에서도 표기가 갱신된다.
        if (_resourceForecast != null)
        {
            _resourceForecast.ForecastChanged.AddListener(HandleForecastChanged);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
            ApplyResourceIcons();
            RenderAllResources();
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
            RenderPopulation(_populationManager.CurrentState);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.AddListener(HandleWaveCleared);
            ResetWaveBar();

            _cycleManager.OnDayReady.AddListener(HandleDayReady);
        }

        // 언어가 바뀌면 이 창의 로컬라이즈된 텍스트를 다시 그린다.
        StringTable.OnLanguageChanged += RefreshLocalizedTexts;
    }

    // 클릭음을 붙이기 위한 래퍼. WorkerModeController는 단축키 경로에서도 호출되므로
    // 컨트롤러가 아니라 버튼을 물리는 이쪽에서 소리를 낸다(창 토글 버튼들은 창이 직접 낸다).
    private void ToggleWorkerMode()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        _workerModeController.ToggleWorkerMode();
    }

    /// <summary>
    /// 밤 진입. 다음 아침 정산에서 굶어 죽을 시민이 있으면 곧장 넘기지 않고 한 번 확인받는다 -
    /// 밤은 되돌릴 수 없는데, 인구 표기 옆의 (-N)만으로는 놓치기 쉽다.
    ///
    /// 기아 자체는 밤이 아니라 <b>다음 낮 시작</b>(OnDayStartUpkeep)에 일어나므로, 여기서 보는 값은
    /// 예측이다. HUD 인구 표기와 같은 GetPopulationUpkeepPreview를 쓰므로 화면과 어긋나지 않는다.
    /// </summary>
    private void GoToNight()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_cycleManager == null)
        {
            return;
        }

        // 관문(튜토리얼 안내·새끼용 가이드)이 이미 막고 있으면 경고보다 막힌 이유를 먼저 알려야 한다.
        // 그대로 넘기면 CycleManager가 DayEndBlocked로 사유를 띄운다.
        if (_cycleManager.IsDayEndBlocked)
        {
            _cycleManager.EndDay();
            return;
        }

        PopulationUpkeepPreview preview = _populationManager != null
            ? GetPopulationUpkeepPreview(_populationManager.CurrentState)
            : default;

        if (preview.PopulationLost > 0 && _confirmPopup != null)
        {
            _confirmPopup.Open(
                STARVATION_CONFIRM_LOC_KEY,
                _cycleManager.EndDay,
                preview.FoodShortage,
                preview.PopulationLost);
            return;
        }

        _cycleManager.EndDay();
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(ApplyCycle);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.RemoveListener(HandleResourceChanged);
        }

        if (_resourceForecast != null)
        {
            _resourceForecast.ForecastChanged.RemoveListener(HandleForecastChanged);
        }

        if (_tooltipPresenter != null)
        {
            _tooltipPresenter.ForceHide();
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(HandleWaveCleared);
            _cycleManager.OnDayReady.RemoveListener(HandleDayReady);
        }

        StringTable.OnLanguageChanged -= RefreshLocalizedTexts;

        if (_waveSlider != null)
        {
            _waveSlider.DOKill();
        }

        if (_wavePoint != null)
        {
            _wavePoint.DOKill();
        }
    }

    // 언어 변경 시 현재 일수로 다시 그릴 수 있도록 마지막 표시 일수를 저장한다.
    private int _currentDay;

    // 낮 진입(CycleManager.OnDayReady) 시 1회. 새 낮(StartDay)과 이어하기 복원(ResumeDay)이 함께 타는
    // 단계이므로, 밤 종료 이벤트를 재생하지 않는 이어하기에서도 이 경로로 진행 바가 일차에 맞춰진다.
    private void HandleDayReady(int day)
    {
        RenderDay(day);
        SyncWaveBar(day);
    }

    // HandleDayReady(day) / 언어 변경으로 갱신된다.
    private void RenderDay(int day)
    {
        _currentDay = day;
        if (_dayText != null)
        {
            _dayText.text = string.Format(DayFormat, day);
        }
    }

    // 언어가 바뀌면(StringTable.OnLanguageChanged) 이 창의 로컬라이즈된 텍스트를 현재 값으로 다시 그린다.
    private void RefreshLocalizedTexts()
    {
        RenderDay(_currentDay);

        // 자원 툴팁 문구는 SetContent로 '밀어 넣는' 방식이라, 다시 그리지 않으면 언어를 바꿔도
        // 이전 언어로 만들어 둔 문자열이 그대로 남는다(수량 표기와 달리 값이 안 바뀌면 갱신될 일이 없다).
        if (_resourceManager != null)
        {
            RenderAllResources();
        }
    }

    // 가용 인구 / 총(최대) 인구로 표시한다.
    private void RenderPopulation(PopulationState state)
    {
        if (!WiringGuard.Require(_populationText, nameof(_populationText), this))
        {
            return;
        }

        PopulationUpkeepPreview preview = GetPopulationUpkeepPreview(state);
        _populationText.text = preview.PopulationLost > 0
            ? string.Format(
                POPULATION_WITH_LOSS_FORMAT,
                state.AvailablePopulation,
                state.MaxPopulation,
                ColorUtility.ToHtmlStringRGB(LOSS_COLOR_DEFAULT),
                preview.PopulationLost)
            : string.Format(
                POPULATION_FORMAT,
                state.AvailablePopulation,
                state.MaxPopulation);
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        RenderPopulation(state);
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        RenderResource(type, amount);

        // 슬라임은 _resourceSlots에 없으므로 RenderResource가 아무것도 하지 않는다 - 요약 칸이 받는다.
        if (_slimeSummary != null && DragonSlimeTable.TryGetAttribute(type, out _))
        {
            _slimeSummary.Refresh();
        }

        if (type == ResourceType.Food && _populationManager != null)
        {
            RenderPopulation(_populationManager.CurrentState);
        }
    }

    private void HandleForecastChanged()
    {
        RenderAllResources();

        if (_populationManager != null)
        {
            RenderPopulation(_populationManager.CurrentState);
        }
    }

    private PopulationUpkeepPreview GetPopulationUpkeepPreview(
        PopulationState state)
    {
        if (_resourceManager == null)
        {
            return default;
        }

        if (!WiringGuard.Require(_economyBalance, nameof(_economyBalance), this))
        {
            return default;
        }

        // 순증감이 아니라 '생산량'을 넘긴다 - Calculate가 요구량을 다시 빼므로,
        // 순증감을 넘기면 인구 유지비가 두 번 차감된다.
        int projectedFoodProduction = _resourceForecast != null
            ? _resourceForecast.GetDailyProduction(ResourceType.Food)
            : 0;

        return PopulationUpkeepRules.Calculate(
            state.MaxPopulation,
            _resourceManager.GetAmount(ResourceType.Food),
            projectedFoodProduction,
            _economyBalance.FoodUpkeepPerPopulation
        );
    }

    // 아이콘은 자원 종류마다 고정이라 보유량과 달리 활성화 시 1회만 채우면 된다.
    // 자원 아이콘은 데이터 에셋(ResourceData)이 단일 출처 - 카탈로그에서 종류로 조회한다.
    // 슬라임 5종은 공용 흰 스프라이트 하나를 쓰므로 속성 색으로 틴트해 구분한다.
    private void ApplyResourceIcons()
    {
        if (_resourceManager.Catalog == null)
        {
            return;
        }

        foreach (ResourceSlot slot in _resourceSlots)
        {
            if (slot.IconImage == null || !_resourceManager.Catalog.TryGet(slot.Type, out ResourceData data))
            {
                continue;
            }

            // 틴트를 씌우지 않는다 - 슬라임 5종도 ResourceData에 전용 스프라이트가 들어가 있어
            // 속성 색을 덧입히면 색이 이중으로 먹는다(예전엔 공용 흰 스프라이트라 TintFor가 필요했다).
            slot.IconImage.sprite = data.Icon;
            slot.IconImage.color = Color.white;
        }
    }

    // 활성화 시점의 보유량을 전 슬롯에 즉시 반영한다(이벤트를 놓친 초기 지급분 포함).
    private void RenderAllResources()
    {
        foreach (ResourceSlot slot in _resourceSlots)
        {
            RenderResource(slot.Type, _resourceManager.GetAmount(slot.Type));
        }

        if (_slimeSummary != null)
        {
            _slimeSummary.Refresh();
        }
    }

    private void RenderResource(ResourceType type, int amount)
    {
        for (int i = 0; i < _resourceSlots.Length; i += 1)
        {
            ResourceSlot slot = _resourceSlots[i];

            if (slot.Type != type)
            {
                continue;
            }

            if (slot.AmountText != null)
            {
                slot.AmountText.text = FormatResourceAmount(type, amount);
            }

            RenderResourceTooltip(i, type, amount);
        }
    }

    // 하루 순증감(생산 - 소모)을 합산해 한 값으로만 표시한다.
    // 증가면 연두색 (+N), 감소면 적색 (-N), 다음 정산 후 바닥나면 보유량까지 적색으로 물들인다.
    private string FormatResourceAmount(ResourceType type, int amount) =>
        ResourceAmountFormatter.Format(
            type,
            amount,
            _resourceForecast,
            _productionColor,
            LOSS_COLOR_DEFAULT);

    // 자원 행의 툴팁 트리거는 _resourceSlots와 같은 인덱스로 캐시해 둔다(OnEnable에서 1회 해석).
    private void RenderResourceTooltip(int slotIndex, ResourceType type, int amount)
    {
        if (_resourceTooltipTriggers == null || _resourceTooltipTriggers[slotIndex] == null)
        {
            return;
        }

        if (_resourceForecast == null)
        {
            _resourceTooltipTriggers[slotIndex].ClearContent();
            return;
        }

        _resourceTooltipTriggers[slotIndex].SetContent(
            ResourceForecastTooltipBuilder.Build(
                type,
                amount,
                _resourceForecast,
                _resourceManager != null ? _resourceManager.Catalog : null,
                _forecastBreakdownBuffer,
                _productionColor,
                LOSS_COLOR_DEFAULT));
    }

    // 자원 행의 툴팁 트리거를 찾아 표시기를 주입한다. 트리거는 수량 텍스트의 부모 행(배경 Image가
    // 레이캐스트를 받는 패널)에 붙어 있다 - ResourceSlot 배열에 필드를 더하지 않고 부모에서 찾는다.
    private void ResolveResourceTooltipTriggers()
    {
        if (_resourceTooltipTriggers != null && _resourceTooltipTriggers.Length == _resourceSlots.Length)
        {
            return;
        }

        _resourceTooltipTriggers = new UI_TooltipTrigger[_resourceSlots.Length];

        for (int i = 0; i < _resourceSlots.Length; i += 1)
        {
            TMP_Text amountText = _resourceSlots[i].AmountText;

            if (amountText == null)
            {
                continue;
            }

            UI_TooltipTrigger trigger = amountText.GetComponentInParent<UI_TooltipTrigger>(true);

            if (trigger == null)
            {
                continue;
            }

            trigger.SetPresenter(_tooltipPresenter);
            _resourceTooltipTriggers[i] = trigger;
        }
    }

    // 웨이브 바를 빈 상태(0칸, Point 시작 위치)로 되돌린다. 진행 중이던 트윈이 있다면 먼저 멈춘다.
    private void ResetWaveBar()
    {
        if (_waveSlider != null)
        {
            _waveSlider.DOKill();
            _waveSlider.value = 0f;
        }

        if (_wavePoint != null)
        {
            _wavePoint.DOKill();
            _wavePoint.anchoredPosition = _wavePointStartPos;
        }

        _shownClearedWaves = 0;
    }

    // 웨이브 클리어(CycleManager.OnNightEnd) 시마다 호출된다. wave는 방금 끝난 웨이브(누적 클리어) 번호.
    // Day1(0클리어)=시작점, Day2(1클리어)=1/6 … Day7(6클리어)=6/6(가득 참), Day8(7클리어)=시작점으로 리셋, 이후 반복.
    // 리셋도 DOTween으로 부드럽게 시작점(line_01)/0으로 되돌린다.
    private void HandleWaveCleared(int wave)
    {
        // cleared==0은 Day8(리셋) → 진행도 0, Point는 시작 칸(인덱스 0).
        ApplyWaveBar(wave % _resetInterval, animate: true);
    }

    // 이어하기(세이브 로드)는 OnNightEnd를 재생하지 않으므로 밤마다 전진하는 것만으로는 바가 복원되지 않는다.
    // 낮 진입 시 일차에서 클리어 수를 되돌려 계산해 바를 맞춘다(연출 없이 즉시).
    private void SyncWaveBar(int day)
    {
        int cleared = ClearedWavesForDay(day);
        if (cleared == _shownClearedWaves)
        {
            // 정상 진행(밤 종료 → 곧바로 다음 낮)에서는 이미 같은 칸이므로, 진행 중인 트윈을 끊지 않는다.
            return;
        }

        ApplyWaveBar(cleared, animate: false);
    }

    // day일차의 낮에는 그 전날 밤까지 day-1회를 클리어한 상태다. HandleWaveCleared가 쓰는
    // "방금 끝난 밤의 일차 % _resetInterval"과 같은 값이 되도록 같은 주기로 접는다.
    private int ClearedWavesForDay(int day)
    {
        if (day <= 0)
        {
            return 0;
        }

        return (day - 1) % _resetInterval;
    }

    // 진행 바(슬라이더 + Point)를 클리어 수에 맞춘다. animate면 트윈, 아니면 즉시 반영한다.
    private void ApplyWaveBar(int cleared, bool animate)
    {
        if (_wavePointStops == null || _wavePointStops.Length == 0)
        {
            return;
        }

        float progress = (float)cleared / WAVE_FILL_LENGTH;

        if (_waveSlider != null)
        {
            _waveSlider.DOKill();

            if (animate)
            {
                // 순수 UI 연출이라 일시정지(Time.timeScale == 0) 중에도 정상 재생되어야 한다.
                _waveSlider.DOValue(progress, _waveTweenDuration).SetUpdate(true);
            }
            else
            {
                _waveSlider.value = progress;
            }
        }

        // 배열은 일수별 위치([0]=Day1 … [6]=Day7). cleared만큼 진행했으니 그 인덱스로 이동한다.
        int stopIndex = Mathf.Min(cleared, _wavePointStops.Length - 1);
        RectTransform stop = _wavePointStops[stopIndex];

        if (_wavePoint != null && stop != null)
        {
            _wavePoint.DOKill();

            if (animate)
            {
                _wavePoint.DOAnchorPos(stop.anchoredPosition, _waveTweenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(_wavePoint.gameObject);
            }
            else
            {
                _wavePoint.anchoredPosition = stop.anchoredPosition;
            }
        }

        _shownClearedWaves = cleared;
    }

    private void ApplyCycle(CycleManager.CycleState state)
    {
        bool isDay = state == CycleManager.CycleState.Day;

        if (_imageDay != null)
        {
            _imageDay.SetActive(isDay);
        }

        if (_imageLight != null)
        {
            _imageLight.SetActive(!isDay);
        }

        // 낮에는 '다음 밤으로' 버튼, 밤에는 속도 조절 UI를 켠다.
        if (_buttonNextNight != null)
        {
            _buttonNextNight.SetActive(isDay);
        }

        if (_speedSetting != null)
        {
            _speedSetting.SetActive(!isDay);
        }
    }
}
