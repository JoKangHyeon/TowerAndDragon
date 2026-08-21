using System.Text;
using UnityEngine;

/// <summary>
/// 배치 미리보기의 "이 자리에 지으면" 결과를 툴팁 문구로 만든다. 툴팁 프레임워크
/// (TooltipContent · UI_TooltipPresenter)는 도메인을 모르므로 배치 전용 표현은 여기에만 둔다
/// (ResourceForecastTooltipBuilder · BuildingEffectTooltipBuilder와 같은 역할).
///
/// 출력 예)
///   논
///   식량            +14
///   생산량 감소     -30%
///   목재 유지비       10
///
///   인구 10명 만충 기준
/// </summary>
public static class PlacementYieldTooltipBuilder
{
    private const string SIGNED_GAIN_FORMAT = "+{0}";
    private const string COLORED_FORMAT = "<color=#{0}>{1}</color>";

    // 커서를 옮긴 칸에서만 만들고 매 프레임 만들지 않으므로 StringBuilder 하나로 충분하다.
    private static readonly StringBuilder BODY_BUILDER = new();

    /// <summary>
    /// estimate는 호출자가 소유하는 재사용 객체다(TryEstimate가 내용을 덮어쓴다).
    /// 담을 내용이 없으면 빈 TooltipContent를 돌려준다.
    /// </summary>
    public static TooltipContent Build(
        PlacementYieldEstimator estimate,
        ResourceCatalog catalog,
        BuildingEffectIconData effectIconData,
        Color gainColor,
        Color lossColor,
        Color noteColor)
    {
        if (estimate == null)
        {
            return default;
        }

        BODY_BUILDER.Clear();

        AppendYields(estimate, catalog, gainColor);
        AppendEffects(estimate, effectIconData, lossColor);
        AppendFullStaffNote(estimate, noteColor);

        return new TooltipContent(ResolveTitle(estimate), BODY_BUILDER.ToString());
    }

    private static void AppendYields(PlacementYieldEstimator estimate, ResourceCatalog catalog, Color gainColor)
    {
        // 지을 수는 있는데 나오는 양이 0인 자리다. 빈 줄만 두면 툴팁이 고장난 것처럼 보이므로 이유를 밝힌다.
        if (estimate.Yields.Count == 0)
        {
            BODY_BUILDER.Append(StringTable.GetString(PlacementLocKeys.NO_YIELD));
            return;
        }

        foreach (ResourceAmount yield in estimate.Yields)
        {
            AppendRow(
                ResolveResourceName(yield.Type, catalog),
                Colored(string.Format(SIGNED_GAIN_FORMAT, yield.Amount), gainColor));
        }
    }

    // 지형 감소율·유지비. 라벨과 수치 표기는 배치된 건물의 표식(UI_BuildingEffectIconSlot)과 같은
    // 출처를 쓴다 - 지은 뒤에 뜨는 표식과 짓기 전 미리보기가 다른 말을 하면 안 된다.
    private static void AppendEffects(
        PlacementYieldEstimator estimate,
        BuildingEffectIconData effectIconData,
        Color lossColor)
    {
        if (effectIconData == null)
        {
            return;
        }

        foreach (BuildingEffectDescriptor effect in estimate.Effects)
        {
            if (!effectIconData.TryResolve(effect.Kind, out BuildingEffectIconEntry entry))
            {
                continue;
            }

            string label = BuildingEffectFormatter.FormatBadgeLabel(effect);

            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            AppendRow(StringTable.GetString(entry.NameLocKey), Colored(label, lossColor));
        }
    }

    // 이 숫자가 "정원을 다 채웠다면"의 값임을 밝힌다. 배치 직후 인구는 0이라, 각주가 없으면
    // 지어놓고 생산이 0인 것을 버그로 읽게 된다.
    private static void AppendFullStaffNote(PlacementYieldEstimator estimate, Color noteColor)
    {
        if (estimate.PopulationCapacity <= 0)
        {
            return;
        }

        BODY_BUILDER.AppendLine();
        BODY_BUILDER.AppendLine();
        BODY_BUILDER.Append(Colored(
            string.Format(
                StringTable.GetString(PlacementLocKeys.FULL_STAFF_NOTE),
                estimate.PopulationCapacity),
            noteColor));
    }

    private static void AppendRow(string label, string value)
    {
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
        }

        BODY_BUILDER.AppendFormat(StringTable.GetString(ResourceLocKeys.TOOLTIP_ROW), label, value);
    }

    private static string Colored(string text, Color color) =>
        string.Format(COLORED_FORMAT, ColorUtility.ToHtmlStringRGB(color), text);

    private static string ResolveResourceName(ResourceType type, ResourceCatalog catalog) =>
        catalog != null && catalog.TryGet(type, out ResourceData data)
            ? StringTable.GetString(data.NameLocKey)
            : string.Empty;

    private static string ResolveTitle(PlacementYieldEstimator estimate) =>
        string.IsNullOrEmpty(estimate.NameLocKey)
            ? string.Empty
            : StringTable.GetString(estimate.NameLocKey);
}
