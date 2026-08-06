using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 자원 한 칸의 하루 증감 내역을 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·UI_TooltipPresenter)는
/// 도메인을 모르므로, 자원 전용 표현은 여기에만 둔다.
///
/// 출력 예)
///   식량
///   생산          +20
///   인구 유지비    -35
///   합계          -15      (합계 줄은 색으로 방향을 표시)
///   5 부족합니다. 이대로 날을 넘기면 인구 5명이 굶어 죽습니다.
/// </summary>
public static class ResourceForecastTooltipBuilder
{
    private const string SIGNED_GAIN_FORMAT = "+{0}";
    private const string SIGNED_LOSS_FORMAT = "-{0}";
    private const string COLORED_FORMAT = "<color=#{0}>{1}</color>";

    // 툴팁 본문은 한 줄에 한 항목씩 쌓는다. 매 프레임이 아니라 값이 바뀔 때만 만들므로 StringBuilder로 충분하다.
    private static readonly StringBuilder BODY_BUILDER = new();

    /// <summary>
    /// 자원 하나의 툴팁 내용을 만든다. breakdownBuffer는 호출자가 소유하는 재사용 버퍼다
    /// (ResourceForecast.CollectBreakdown이 내용을 덮어쓴다).
    /// </summary>
    public static TooltipContent Build(
        ResourceType type,
        int currentAmount,
        ResourceForecast forecast,
        ResourceCatalog catalog,
        List<ResourceForecastEntry> breakdownBuffer,
        Color gainColor,
        Color lossColor)
    {
        if (forecast == null || breakdownBuffer == null)
        {
            return default;
        }

        forecast.CollectBreakdown(type, breakdownBuffer);

        int netChange = forecast.GetDailyNetChange(type);

        BODY_BUILDER.Clear();

        foreach (ResourceForecastEntry entry in breakdownBuffer)
        {
            AppendRow(
                StringTable.GetString(ResourceLocKeys.SourceLocKey(entry.Source)),
                FormatSigned(entry.Amount, gainColor, lossColor));
        }

        // 항목이 둘 이상일 때만 합계 줄이 정보를 더한다(한 줄뿐이면 같은 값이 두 번 나온다).
        if (breakdownBuffer.Count > 1)
        {
            AppendRow(
                StringTable.GetString(ResourceLocKeys.TOOLTIP_TOTAL),
                FormatSigned(netChange, gainColor, lossColor));
        }

        AppendWarning(type, currentAmount, netChange, lossColor);

        return new TooltipContent(ResolveTitle(type, catalog), BODY_BUILDER.ToString());
    }

    private static void AppendRow(string label, string value)
    {
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
        }

        BODY_BUILDER.AppendFormat(StringTable.GetString(ResourceLocKeys.TOOLTIP_ROW), label, value);
    }

    // 이대로 날을 넘겼을 때 벌어질 일을 알린다. 부족분이 0이면 "모자라지는 않지만 바닥난다"는 뜻이라
    // 결과 문구 대신 고갈 안내를 쓴다.
    private static void AppendWarning(
        ResourceType type,
        int currentAmount,
        int netChange,
        Color lossColor)
    {
        if (!ResourceForecastRules.WillRunOut(currentAmount, netChange))
        {
            return;
        }

        int shortage = ResourceForecastRules.Shortage(currentAmount, netChange);

        string warning = shortage > 0
            ? string.Format(StringTable.GetString(ResourceLocKeys.WarningLocKey(type)), shortage)
            : StringTable.GetString(ResourceLocKeys.WARNING_DEPLETED);

        BODY_BUILDER.AppendLine();
        BODY_BUILDER.AppendLine();
        BODY_BUILDER.AppendFormat(COLORED_FORMAT, ColorUtility.ToHtmlStringRGB(lossColor), warning);
    }

    private static string FormatSigned(int amount, Color gainColor, Color lossColor)
    {
        bool isGain = amount >= 0;
        string text = string.Format(
            isGain ? SIGNED_GAIN_FORMAT : SIGNED_LOSS_FORMAT,
            Mathf.Abs(amount));

        return string.Format(
            COLORED_FORMAT,
            ColorUtility.ToHtmlStringRGB(isGain ? gainColor : lossColor),
            text);
    }

    private static string ResolveTitle(ResourceType type, ResourceCatalog catalog)
    {
        if (catalog != null && catalog.TryGet(type, out ResourceData data))
        {
            return StringTable.GetString(data.NameLocKey);
        }

        return string.Empty;
    }
}
