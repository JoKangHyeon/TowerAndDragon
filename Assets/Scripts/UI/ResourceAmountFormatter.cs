using UnityEngine;

/// <summary>
/// 자원 보유량 옆에 붙는 하루 예상 증감 표기를 만든다. HUD(UI_IngameWindow)와 독립 패널
/// (ResourceAmountView)이 같은 서식·같은 색 판정을 쓰도록 여기 한 곳에만 둔다 -
/// 한쪽만 생산량을 보고 다른 쪽은 순증감을 보면 같은 자원이 창마다 다른 값으로 보인다.
/// </summary>
public static class ResourceAmountFormatter
{
    // 보유량 옆에 다음 정산의 순증감(생산 - 소모)을 한 값으로만 붙인다.
    // "(+10)(-20)"처럼 나누어 쓰지 않고 합산해 "(-10)"으로 보여준다.
    private const string RESOURCE_WITH_GAIN_FORMAT = "{0}<color=#{1}>(+{2})</color>";
    private const string RESOURCE_WITH_LOSS_FORMAT = "{0}<color=#{1}>(-{2})</color>";

    // 다음 정산 후 보유량이 0 이하가 되는 자원은 보유량 숫자까지 적색으로 물들여 경고한다.
    private const string RESOURCE_DEPLETING_FORMAT = "<color=#{1}>{0}(-{2})</color>";

    /// <summary>하루 순증가 글씨 기본 색(연두색).</summary>
    public static readonly Color GAIN_COLOR_DEFAULT = new Color(0.62f, 1f, 0.42f);

    /// <summary>하루 순감소·고갈 경고 글씨 기본 색(적색).</summary>
    public static readonly Color LOSS_COLOR_DEFAULT = new Color(1f, 0.35f, 0.35f);

    /// <summary>
    /// "보유량", "보유량(+증가)", "보유량(-감소)" 중 하나를 만든다.
    /// 예측이 없으면(forecast == null) 보유량만 낸다.
    /// </summary>
    public static string Format(
        ResourceType type,
        int amount,
        ResourceForecast forecast,
        Color gainColor,
        Color lossColor)
    {
        int netChange = forecast != null ? forecast.GetDailyNetChange(type) : 0;

        if (netChange == 0)
        {
            return amount.ToString();
        }

        if (netChange > 0)
        {
            return string.Format(
                RESOURCE_WITH_GAIN_FORMAT,
                amount,
                ColorUtility.ToHtmlStringRGB(gainColor),
                netChange);
        }

        string lossFormat = ResourceForecastRules.WillRunOut(amount, netChange)
            ? RESOURCE_DEPLETING_FORMAT
            : RESOURCE_WITH_LOSS_FORMAT;

        return string.Format(
            lossFormat,
            amount,
            ColorUtility.ToHtmlStringRGB(lossColor),
            Mathf.Abs(netChange));
    }
}
