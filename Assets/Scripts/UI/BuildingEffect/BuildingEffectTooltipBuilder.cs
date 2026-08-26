using System.Collections.Generic;
using System.Text;

/// <summary>
/// 건물 하나에 걸린 효과 전체를 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·
/// UI_TooltipPresenter)는 도메인을 모르므로 건물 전용 표현은 여기에만 둔다
/// (ResourceForecastTooltipBuilder와 같은 역할).
///
/// 출력 예)
///   벌목장
///   목재 유지비
///   배치 인구 1명당 매일 나무 1을 소모합니다. (현재 2)
///
/// 설명에 들어가는 수치는 전부 인자로 받은 TerrainPenaltyModifiers에서 읽는다 - 밸런스 값이
/// 아직 조정 중이라(TerrainPenaltyData 주석 참고) 문구에 상수로 박으면 테이블과 어긋난다.
/// </summary>
public static class BuildingEffectTooltipBuilder
{
    // 리치텍스트 서식은 언어에 따라 달라지지 않으므로 상수로 둔다
    // (ResourceForecastTooltipBuilder.COLORED_FORMAT과 같은 관례).
    private const string NAME_FORMAT = "<b>{0}</b>";

    private static readonly StringBuilder BODY_BUILDER = new();

    /// <summary>
    /// 호버 대상이 바뀔 때만 호출한다(매 프레임 문자열을 새로 만들지 않는다).
    /// effects는 BuildingEffectResolver.Collect가 채운 버퍼를 그대로 넘기면 된다.
    /// </summary>
    public static TooltipContent Build(
        Building building,
        IReadOnlyList<BuildingEffectDescriptor> effects,
        in TerrainPenaltyModifiers modifiers,
        BuildingEffectIconData iconData)
    {
        if (effects == null || effects.Count == 0 || iconData == null)
        {
            return default;
        }

        BODY_BUILDER.Clear();

        for (int i = 0; i < effects.Count; i++)
        {
            BuildingEffectDescriptor effect = effects[i];

            if (!iconData.TryResolve(effect.Kind, out BuildingEffectIconEntry entry))
            {
                continue;
            }

            AppendEffect(entry, effect, modifiers);
        }

        return BODY_BUILDER.Length == 0
            ? default
            : new TooltipContent(ResolveTitle(building), BODY_BUILDER.ToString());
    }

    private static void AppendEffect(
        BuildingEffectIconEntry entry,
        in BuildingEffectDescriptor effect,
        in TerrainPenaltyModifiers modifiers)
    {
        // 항목 사이는 빈 줄로 띄운다 - 이름과 설명이 각각 한 줄이라 구분선이 없으면 뭉쳐 보인다.
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
            BODY_BUILDER.AppendLine();
        }

        BODY_BUILDER.AppendFormat(NAME_FORMAT, StringTable.GetString(entry.NameLocKey));

        string description = FormatDescription(entry.DescriptionLocKey, effect, modifiers);

        if (!string.IsNullOrEmpty(description))
        {
            BODY_BUILDER.AppendLine();
            BODY_BUILDER.Append(description);
        }
    }

    // 종류마다 설명에 채울 인자가 다르다(감소율 1개 / 유지비는 인구 1명당 비율 + 현재 소모량).
    private static string FormatDescription(
        string descriptionLocKey,
        in BuildingEffectDescriptor effect,
        in TerrainPenaltyModifiers modifiers)
    {
        if (string.IsNullOrEmpty(descriptionLocKey))
        {
            return string.Empty;
        }

        string format = StringTable.GetString(descriptionLocKey);

        switch (effect.Kind)
        {
            case BuildingEffectKind.YieldDown:
            case BuildingEffectKind.AttackSpeedDown:
                return string.Format(format, BuildingEffectFormatter.ToPercent(effect.Ratio));

            case BuildingEffectKind.WoodUpkeep:
                return string.Format(
                    format,
                    BuildingEffectFormatter.FormatRate(modifiers.WoodUpkeepPerPopulation),
                    effect.DailyAmount);

            case BuildingEffectKind.StoneUpkeep:
                return string.Format(
                    format,
                    BuildingEffectFormatter.FormatRate(modifiers.StoneUpkeepPerPopulation),
                    effect.DailyAmount);

            default:
                return format;
        }
    }

    // 건물 이름은 각자의 Data 에셋이 단일 출처다(ResourceProductionData / TowerData / ResearchLabData).
    private static string ResolveTitle(Building building)
    {
        if (building is Factory factory && factory.Data != null)
        {
            return StringTable.GetString(factory.Data.NameLocKey);
        }

        if (building is Tower tower && tower.Data != null)
        {
            return StringTable.GetString(tower.Data.NameLocKey);
        }

        if (building is ResearchLab lab && lab.Data != null)
        {
            return StringTable.GetString(lab.Data.NameLocKey);
        }

        return string.Empty;
    }
}
