using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// HUD 특화자원 행 오른쪽 끝에서 슬라임 5종의 수급을 기호 한 글자로 요약한다.
/// 5종을 모두 늘어놓으면 행이 너무 길어지므로, 자세한 값은 마우스를 올렸을 때 뜨는 세로 패널에서 본다.
///
/// 판정은 SlimeBalanceRules가, 상세 패널의 표기는 UI_ResourceAmountPanel(=ResourceAmountView)이 갖는다.
/// 상세 패널 각 행의 예측 내역 툴팁도 ResourceAmountView가 붙인다(HUD 자원 7칸과 같은 문구).
/// 이 컴포넌트는 값을 모아 넘기고 기호·색을 칠하는 일만 한다.
///
/// 자원 출처(ResourceManager·ResourceForecast)와 툴팁 표시기는 씬 오브젝트라 프리팹에 박을 수 없어
/// UI_IngameWindow가 Construct로 주입한다.
/// </summary>
public class UI_SlimeSummaryIndicator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 상태 기호. 언어와 무관한 단일 문자라 스트링테이블을 거치지 않는다.
    private const string BALANCED_GLYPH = "-";
    private const string SURPLUS_GLYPH = "▲";
    private const string DEFICIT_GLYPH = "▼";
    private const string STARVING_GLYPH = "!";

    // 굶는 상태의 기본 색(흑적색). 순감소 적색보다 어둡게 해 한 단계 더 위험함을 드러낸다.
    private static readonly Color STARVING_COLOR_DEFAULT = new Color(0.55f, 0.06f, 0.06f);

    [Tooltip("상태 기호를 그릴 텍스트.")]
    [SerializeField] private TMP_Text _stateText;

    [Tooltip("마우스를 올렸을 때 켜지는 슬라임 상세 패널. 비활성 상태로 저장해 둔다.")]
    [SerializeField] private GameObject _detailPanel;

    [Tooltip("상세 패널의 자원 표시. Construct로 받은 자원 출처를 그대로 넘겨준다.")]
    [SerializeField] private UI_ResourceAmountPanel _detailAmounts;

    [Tooltip("전 종의 하루 순증감이 0일 때의 기호 색.")]
    [SerializeField] private Color _balancedColor = Color.black;

    [Tooltip("줄어드는 종이 없을 때의 기호 색(연두색).")]
    [SerializeField] private Color _surplusColor = ResourceAmountFormatter.GAIN_COLOR_DEFAULT;

    [Tooltip("줄어드는 종이 있을 때의 기호 색(적색).")]
    [SerializeField] private Color _deficitColor = ResourceAmountFormatter.LOSS_COLOR_DEFAULT;

    [Tooltip("다음 정산에서 새끼용이 먹이를 못 받는 상태의 기호 색(흑적색).")]
    [SerializeField] private Color _starvingColor = STARVING_COLOR_DEFAULT;

    private ResourceManager _resourceManager;
    private ResourceForecast _resourceForecast;

    // 판정에 넘길 재사용 버퍼. 슬라임 5종뿐이라 매번 새로 담아도 할당이 생기지 않는다.
    private readonly List<SlimeBalanceEntry> _entryBuffer = new();

    /// <summary>자원 출처와 툴팁 표시기를 주입한다. 상세 패널에도 같은 것을 넘긴다.</summary>
    public void Construct(
        ResourceManager resourceManager,
        ResourceForecast resourceForecast,
        UI_TooltipPresenter tooltipPresenter)
    {
        _resourceManager = resourceManager;
        _resourceForecast = resourceForecast;

        if (_detailAmounts != null)
        {
            _detailAmounts.Construct(resourceManager, resourceForecast, tooltipPresenter);
        }

        Refresh();
    }

    /// <summary>보유량·예측을 다시 읽어 기호와 색을 갱신한다.</summary>
    public void Refresh()
    {
        if (_stateText == null || _resourceManager == null)
        {
            return;
        }

        _entryBuffer.Clear();

        foreach (ResourceType slimeType in DragonSlimeTable.AllSlimes)
        {
            int netChange = _resourceForecast != null
                ? _resourceForecast.GetDailyNetChange(slimeType)
                : 0;

            _entryBuffer.Add(new SlimeBalanceEntry(_resourceManager.GetAmount(slimeType), netChange));
        }

        SlimeBalanceState state = SlimeBalanceRules.Evaluate(_entryBuffer);

        _stateText.text = GlyphFor(state);
        _stateText.color = ColorFor(state);
    }

    private void Awake()
    {
        HideDetail();
    }

    // 호버 중에 HUD가 꺼지면 상세 패널만 남는다(OnPointerExit이 오지 않는다).
    private void OnDisable()
    {
        HideDetail();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_detailPanel != null)
        {
            _detailPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideDetail();
    }

    private void HideDetail()
    {
        if (_detailPanel != null)
        {
            _detailPanel.SetActive(false);
        }
    }

    private string GlyphFor(SlimeBalanceState state) => state switch
    {
        SlimeBalanceState.Surplus => SURPLUS_GLYPH,
        SlimeBalanceState.Deficit => DEFICIT_GLYPH,
        SlimeBalanceState.Starving => STARVING_GLYPH,
        _ => BALANCED_GLYPH,
    };

    private Color ColorFor(SlimeBalanceState state) => state switch
    {
        SlimeBalanceState.Surplus => _surplusColor,
        SlimeBalanceState.Deficit => _deficitColor,
        SlimeBalanceState.Starving => _starvingColor,
        _ => _balancedColor,
    };
}
