using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소를 한곳에서 관리한다.
/// - 낮/밤 심볼(Symbol_Day) 전환과 하단 컨트롤 표시 (CycleManager.OnCycleChanged 구독)
/// - Panel_Label/Day 날짜 텍스트 표시 (CycleManager.OnDayStart 구독)
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

    // 자원 표기: 보유량과 다음 정산의 생산량을 TMP 리치텍스트 색으로 구분한다.
    private const string RESOURCE_WITH_PRODUCTION_FORMAT =
        "{0}<color=#{1}>(+{2})</color>";

    // 하루 생산량 글씨 기본 색(연두색).
    private static readonly Color PRODUCTION_COLOR_DEFAULT = new Color(0.62f, 1f, 0.42f);
    private static readonly Color LOSS_COLOR_DEFAULT = new Color(1f, 0.35f, 0.35f);

    // 웨이브 진행 바가 가득 찰 때까지의 일수. 이 값째 클리어에 슬라이더가 가득 찬다.
    private const int WAVE_FILL_LENGTH = 6;

    // 자원 표시 1칸: 자원 종류 ↔ 수량 텍스트(+ 선택적으로 아이콘).
    [System.Serializable]
    private struct ResourceSlot
    {
        public ResourceType Type;
        public TMP_Text AmountText;
        [Tooltip("비워두면 프리팹에 배치된 아이콘을 그대로 쓴다. 지정하면 ResourceData의 아이콘으로 덮어쓴다.")]
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

    [Header("자원 표시 (Panel_TopLeft)")]
    [SerializeField] private ResourceManager _resourceManager;
    [Tooltip("자원 종류별 수량 텍스트. 기본 3종 + 특화 4종 + 슬라임 5종.")]
    [SerializeField] private ResourceSlot[] _resourceSlots;
    [Tooltip("하루 예상 생산량 표기용. 각 자원 보유량 옆에 (+생산량)으로 노출한다.")]
    [SerializeField] private ProductionForecast _productionForecast;
    [Tooltip("하루 생산량 글씨 색(연두색).")]
    [SerializeField] private Color _productionColor = PRODUCTION_COLOR_DEFAULT;

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

    [Header("용 스킬트리 (Panel_BottomRight)")]
    [Tooltip("용 스킬 모드 토글 버튼.")]
    [SerializeField] private Button _buttonDragonSkill;
    [Tooltip("용 스킬 정보 창. 버튼 클릭 시 용 스킬 모드를 토글한다.")]
    [SerializeField] private UI_DragonSkillWindow _dragonSkillWindow;

    [Header("연구 (Panel_BottomRight)")]
    [Tooltip("연구 창 토글 버튼.")]
    [SerializeField] private Button _buttonResearch;
    [Tooltip("연구 창. 버튼 클릭 시 연구 창을 토글한다.")]
    [SerializeField] private UI_ResearchWindow _researchWindow;
    [Tooltip("연구 버튼 라벨. 스트링테이블에서 채운다.")]
    [SerializeField] private TMP_Text _buttonResearchLabel;

    [Header("새끼용 인벤토리 (Panel_BottomRight)")]
    [Tooltip("새끼용 인벤토리 창 토글 버튼.")]
    [SerializeField] private Button _buttonBabyDragonInventory;
    [Tooltip("새끼용 인벤토리 창. 버튼 클릭 시 토글한다.")]
    [SerializeField] private UI_DragonInventoryWindow _babyDragonInventoryWindow;

    [Header("설정 (Button_Setting)")]
    [Tooltip("누르면 사용 가능한 언어를 순환 전환한다(en_us ↔ ko_kr).")]
    [SerializeField] private Button _buttonSetting;

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
            _buttonWorkerMode.onClick.AddListener(_workerModeController.ToggleWorkerMode);
        }

        // 점령 버튼: 누를 때마다 점령 모드를 켜고 끈다(토글). 실제 모드 처리는 점령 창이 담당.
        if (_buttonConquest != null && _conquestWindow != null)
        {
            _buttonConquest.onClick.AddListener(_conquestWindow.ToggleConquestMode);
        }

        if (_buttonDragonSkill != null && _dragonSkillWindow != null)
        {
            _buttonDragonSkill.onClick.AddListener(_dragonSkillWindow.ToggleFromEntryPoint);
        }

        if (_buttonResearch != null && _researchWindow != null)
        {
            _buttonResearch.onClick.AddListener(_researchWindow.ToggleFromEntryPoint);
        }

        if (_buttonResearchLabel != null)
        {
            _buttonResearchLabel.text = StringTable.GetString(ResearchLocKeys.WINDOW_HEADER);
        }

        if (_buttonBabyDragonInventory != null && _babyDragonInventoryWindow != null)
        {
            _buttonBabyDragonInventory.onClick.AddListener(_babyDragonInventoryWindow.ToggleFromEntryPoint);
        }

        // 설정 버튼: 누를 때마다 다음 언어로 순환 전환한다(테스트용 언어 토글).
        if (_buttonSetting != null)
        {
            _buttonSetting.onClick.AddListener(StringTable.CycleLanguage);
        }
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(ApplyCycle);
            ApplyCycle(_cycleManager.CurrentCycle);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
            ApplyResourceIcons();

            // 생산량 예측이 바뀌면(건물/인구 변경) 보유량 옆 (+생산량) 표기를 다시 그린다.
            if (_productionForecast != null)
            {
                _productionForecast.ForecastChanged.AddListener(HandleForecastChanged);
            }

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

            _cycleManager.OnDayStart.AddListener(RenderDay);
        }

        // 언어가 바뀌면 이 창의 로컬라이즈된 텍스트를 다시 그린다.
        StringTable.OnLanguageChanged += RefreshLocalizedTexts;

        // 자원 패널(ContentSizeFitter) 폭이 확정된 뒤 인구 패널이 겹치지 않도록,
        // 다음 프레임에 '자식 자원 패널 → 부모 층' 순서로 레이아웃을 한 번만 갱신한다.
        RefreshResourceLayoutNextFrame().Forget();
    }

    // 활성화 다음 프레임(자원 패널·텍스트 크기 확정 시점)에 딱 한 번 실행.
    // 순서 보장: (1) 각 자원 패널을 먼저 갱신해 폭을 확정 → (2) 그 부모 층을 갱신해 확정된 폭으로 재배치.
    private async UniTaskVoid RefreshResourceLayoutNextFrame()
    {
        await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());

        foreach (ResourceSlot slot in _resourceSlots)
        {
            if (slot.AmountText == null)
            {
                continue;
            }

            ContentSizeFitter panelFitter = slot.AmountText.GetComponentInParent<ContentSizeFitter>();
            if (panelFitter == null)
            {
                continue;
            }

            RectTransform panelRect = (RectTransform)panelFitter.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            if (panelRect.parent is RectTransform floorRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(floorRect);
            }
        }
    }

    private void GoToNight()
    {
        if (_cycleManager != null)
        {
            _cycleManager.EndDay();
        }
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

            if (_productionForecast != null)
            {
                _productionForecast.ForecastChanged.RemoveListener(HandleForecastChanged);
            }
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(HandleWaveCleared);
            _cycleManager.OnDayStart.RemoveListener(RenderDay);
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

    // CycleManager.OnDayStart(day)로 갱신된다.
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

        if (_buttonResearchLabel != null)
        {
            _buttonResearchLabel.text = StringTable.GetString(ResearchLocKeys.WINDOW_HEADER);
        }
    }

    // 가용 인구 / 총(최대) 인구로 표시한다.
    private void RenderPopulation(PopulationState state)
    {
        if (_populationText == null)
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

        int projectedFoodProduction = _productionForecast != null
            ? _productionForecast.GetDailyProduction(ResourceType.Food)
            : 0;

        return PopulationUpkeepRules.Calculate(
            state.MaxPopulation,
            _resourceManager.GetAmount(ResourceType.Food),
            projectedFoodProduction
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

            slot.IconImage.sprite = data.Icon;
            slot.IconImage.color = DragonAttributePalette.TintFor(slot.Type);
        }
    }

    // 활성화 시점의 보유량을 전 슬롯에 즉시 반영한다(이벤트를 놓친 초기 지급분 포함).
    private void RenderAllResources()
    {
        foreach (ResourceSlot slot in _resourceSlots)
        {
            RenderResource(slot.Type, _resourceManager.GetAmount(slot.Type));
        }
    }

    private void RenderResource(ResourceType type, int amount)
    {
        foreach (ResourceSlot slot in _resourceSlots)
        {
            if (slot.Type == type && slot.AmountText != null)
            {
                slot.AmountText.text = FormatResourceAmount(type, amount);
            }
        }
    }

    // 하루 예상 생산량이 있으면 "보유량(+생산량)"으로 표시한다.
    private string FormatResourceAmount(ResourceType type, int amount)
    {
        int production = _productionForecast != null ? _productionForecast.GetDailyProduction(type) : 0;

        if (production > 0)
        {
            return string.Format(
                RESOURCE_WITH_PRODUCTION_FORMAT,
                amount,
                ColorUtility.ToHtmlStringRGB(_productionColor),
                production);
        }

        return amount.ToString();
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
    }

    // 웨이브 클리어(CycleManager.OnNightEnd) 시마다 호출된다. wave는 방금 끝난 웨이브(누적 클리어) 번호.
    // Day1(0클리어)=시작점, Day2(1클리어)=1/6 … Day7(6클리어)=6/6(가득 참), Day8(7클리어)=시작점으로 리셋, 이후 반복.
    // 리셋도 DOTween으로 부드럽게 시작점(line_01)/0으로 되돌린다.
    private void HandleWaveCleared(int wave)
    {
        if (_wavePointStops == null || _wavePointStops.Length == 0)
        {
            return;
        }

        // cleared==0은 Day8(리셋) → 진행도 0, Point는 시작 칸(인덱스 0).
        int cleared = wave % _resetInterval;
        float progress = (float)cleared / WAVE_FILL_LENGTH;

        if (_waveSlider != null)
        {
            _waveSlider.DOKill();
            // 순수 UI 연출이라 일시정지(Time.timeScale == 0) 중에도 정상 재생되어야 한다.
            _waveSlider.DOValue(progress, _waveTweenDuration).SetUpdate(true);
        }

        // 배열은 일수별 위치([0]=Day1 … [6]=Day7). cleared만큼 진행했으니 그 인덱스로 이동한다.
        int stopIndex = Mathf.Min(cleared, _wavePointStops.Length - 1);
        RectTransform stop = _wavePointStops[stopIndex];

        if (_wavePoint != null && stop != null)
        {
            _wavePoint.DOKill();
            _wavePoint.DOAnchorPos(stop.anchoredPosition, _waveTweenDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(_wavePoint.gameObject);
        }
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
