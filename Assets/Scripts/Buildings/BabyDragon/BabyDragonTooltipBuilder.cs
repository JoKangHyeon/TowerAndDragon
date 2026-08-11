using System.Collections.Generic;
using System.Text;

/// <summary>
/// 배치된 새끼용의 효과를 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·UI_TooltipPresenter)는
/// 도메인을 모르므로 새끼용 전용 표현은 여기에만 둔다(ResourceForecastTooltipBuilder와 같은 구조).
///
/// 지금 걸린 모드와 <b>그 모드에서 나오는 효과</b>만 보여준다 - 공격 모드에서 버프 수치를,
/// 버프 모드에서 사거리를 보여주면 지금 무엇을 하고 있는지가 흐려진다.
/// 관리창이 데이터 원본을 나열하는 것과 역할이 다르다.
///
/// 출력 예)
///   생명 새끼용                        불 새끼용
///   모드        버프모드                모드        공격모드
///   상태        활성                    상태        활성
///   버프 배율    ×2.2                   사거리       5
///   적용 대상    식량 · 통나무           공격 간격    1.2초
///   적용 중      농장 (식량 40)          대상        지상 · 공중
/// </summary>
public static class BabyDragonTooltipBuilder
{
    private const string MODE_LABEL_LOC_KEY = "baby_dragon_tooltip_mode_label";
    private const string MULTIPLIER_LABEL_LOC_KEY = BabyDragonLocKeys.BUFF_MULTIPLIER_LABEL;
    private const string TARGETS_LABEL_LOC_KEY = "baby_dragon_tooltip_targets_label";
    private const string AFFECTED_LABEL_LOC_KEY = "baby_dragon_tooltip_affected_label";
    private const string NO_TARGET_LOC_KEY = "baby_dragon_tooltip_no_target";
    private const string STOPPED_LOC_KEY = "baby_dragon_tooltip_stopped";
    private const string STOPPED_EFFECT_FORMAT_LOC_KEY = "baby_dragon_tooltip_stopped_effect";
    private const string NO_EFFECT_LOC_KEY = "baby_dragon_tooltip_no_effect";
    private const string ROW_LOC_KEY = "baby_dragon_tooltip_row";
    private const string CONTINUED_ROW_LOC_KEY = "baby_dragon_tooltip_continued_row";
    private const string FACTORY_LOC_KEY = "baby_dragon_tooltip_factory";
    private const string CONSTRUCTION_UNLOCK_LOC_KEY = "baby_dragon_tooltip_construction_unlock";
    private const string PENALTY_MITIGATION_LOC_KEY = "baby_dragon_tooltip_penalty_mitigation";
    private const string AURA_LABEL_LOC_KEY = "baby_dragon_tooltip_aura_label";

    private const string RANGE_LABEL_LOC_KEY = "baby_dragon_tooltip_range_label";
    private const string INTERVAL_LABEL_LOC_KEY = "baby_dragon_tooltip_interval_label";
    private const string AREA_LABEL_LOC_KEY = "baby_dragon_tooltip_area_label";
    private const string TARGET_LABEL_LOC_KEY = "baby_dragon_tooltip_target_label";
    private const string TARGET_ALL_LOC_KEY = "baby_dragon_tooltip_target_all";
    private const string TARGET_GROUND_LOC_KEY = "baby_dragon_tooltip_target_ground";
    private const string TARGET_AIR_LOC_KEY = "baby_dragon_tooltip_target_air";

    private const string INTERVAL_VALUE_LOC_KEY = "baby_dragon_tooltip_interval_value";
    private const string RADIUS_VALUE_LOC_KEY = "baby_dragon_tooltip_radius_value";

    private const string MULTIPLIER_FORMAT = BabyDragonLocKeys.BUFF_MULTIPLIER_FORMAT;
    private const string DISTANCE_FORMAT = "{0:0.#}";
    private const string SEPARATOR = " · ";

    // 값이 바뀔 때만 다시 만들므로 StringBuilder 하나를 돌려 쓴다(ResourceForecastTooltipBuilder와 같은 관례).
    private static readonly StringBuilder BODY_BUILDER = new();

    // 구분자로 이어 붙이는 임시 버퍼. BODY_BUILDER를 쓰면 조립 중인 본문을 덮으므로 따로 둔다.
    private static readonly StringBuilder JOIN_BUILDER = new();

    /// <summary>
    /// buffTargets는 호출자가 소유하는 재사용 버퍼다(BabyDragonBuffSystem.CollectBuffTargets가 채운다).
    /// 버프 모드가 아니거나 생산 버프가 없으면 비어 있다.
    /// </summary>
    public static TooltipContent Build(
        BabyDragonTower babyDragon,
        float effectiveMultiplier,
        IReadOnlyList<BabyDragonBuffTarget> buffTargets,
        ResourceCatalog catalog)
    {
        BabyDragonData data = babyDragon == null ? null : babyDragon.DragonData;

        if (data == null)
        {
            return default;
        }

        BODY_BUILDER.Clear();

        AppendRow(
            StringTable.GetString(MODE_LABEL_LOC_KEY),
            StringTable.GetString(BabyDragonLocKeys.ModeLocKey(babyDragon.Mode)));

        AppendRow(
            StringTable.GetString(BabyDragonLocKeys.STATUS_LABEL),
            StringTable.GetString(BabyDragonLocKeys.StatusLocKey(babyDragon.CanOperate)));

        bool hasEffect = babyDragon.Mode == BabyDragonMode.Buff
            ? AppendBuffEffects(data, effectiveMultiplier, buffTargets, catalog, babyDragon.CanOperate)
            : AppendAttackEffects(data, babyDragon.GetComponent<TowerAttack>());

        // 지금 모드에서 아무 효과도 없다면 그 사실 자체가 알려야 할 정보다 - 빈칸으로 두면
        // 툴팁이 덜 만들어진 것처럼 보인다(모드를 반대로 골라 둔 경우가 여기에 걸린다).
        if (!hasEffect)
        {
            AppendLine(StringTable.GetString(NO_EFFECT_LOC_KEY));
        }

        return new TooltipContent(ResolveTitle(data), BODY_BUILDER.ToString());
    }

    // 사거리·간격은 데이터 원본이 아니라 실제 판정에 쓰이는 값을 보여준다 - 연구·오라·지형이
    // 곱해지므로 원본을 그대로 쓰면 버프 배율에서 고친 "표시값 ≠ 적용값"이 여기 남는다.
    // 공격 컴포넌트가 없으면(아직 Setup 전) 원본으로 물러난다.
    private static bool AppendAttackEffects(BabyDragonData data, TowerAttack attack)
    {
        if (!data.CanAttack)
        {
            return false;
        }

        AttackSO attackData = data.Attack;

        AppendRow(
            StringTable.GetString(RANGE_LABEL_LOC_KEY),
            string.Format(DISTANCE_FORMAT, attack != null ? attack.EffectiveRange : attackData.Range));

        AppendRow(
            StringTable.GetString(INTERVAL_LABEL_LOC_KEY),
            string.Format(
                StringTable.GetString(INTERVAL_VALUE_LOC_KEY),
                attack != null ? attack.EffectiveAttackInterval : attackData.Interval));

        if (attackData.HasArea)
        {
            AppendRow(
                StringTable.GetString(AREA_LABEL_LOC_KEY),
                string.Format(StringTable.GetString(RADIUS_VALUE_LOC_KEY), attackData.AreaRadius));
        }

        AppendRow(
            StringTable.GetString(TARGET_LABEL_LOC_KEY),
            StringTable.GetString(TargetLocKey(data.TargetMovementFilter)));

        return true;
    }

    private static bool AppendBuffEffects(
        BabyDragonData data,
        float effectiveMultiplier,
        IReadOnlyList<BabyDragonBuffTarget> buffTargets,
        ResourceCatalog catalog,
        bool canOperate)
    {
        bool hasEffect = AppendYieldBuff(data, effectiveMultiplier, buffTargets, catalog, canOperate);

        // 지형 효과와 오라도 굶주리면 함께 꺼진다(BabyDragonBuffSystem.RecomputeConstructionUnlocks,
        // RecomputePenaltyMitigation, TowerAuraSystem이 모두 CanOperate를 먼저 본다).
        // 생산 버프만 멈춘 것처럼 보이면, 건물이 정지되고 인구까지 회수된 이유를 툴팁에서 알 수 없다.
        hasEffect |= AppendTerrainList(data.ConstructionUnlockTerrains, CONSTRUCTION_UNLOCK_LOC_KEY, canOperate);
        hasEffect |= AppendTerrainList(data.PenaltyMitigationTerrains, PENALTY_MITIGATION_LOC_KEY, canOperate);
        hasEffect |= AppendTowerAura(data, canOperate);

        return hasEffect;
    }

    // 굶주려 멈춘 효과는 회색으로 죽이고 사유를 덧붙인다 - 줄을 아예 지우면 "원래 없는 효과"와
    // 구분되지 않고, 그대로 두면 지금 걸려 있는 것처럼 읽힌다.
    private static string MarkStopped(string line, bool canOperate)
    {
        if (canOperate)
        {
            return line;
        }

        return string.Format(
            StringTable.GetString(STOPPED_EFFECT_FORMAT_LOC_KEY),
            line,
            StringTable.GetString(STOPPED_LOC_KEY));
    }

    // 무엇에 걸리도록 만들어진 버프인지(적용 대상)와 지금 실제로 걸려 있는 곳(적용 중)을 나눠 보여준다 -
    // 범위 안에 대상 시설이 없을 때 "효과가 없는 것"인지 "원래 그런 버프가 아닌 것"인지 구분되어야 한다.
    private static bool AppendYieldBuff(
        BabyDragonData data,
        float effectiveMultiplier,
        IReadOnlyList<BabyDragonBuffTarget> buffTargets,
        ResourceCatalog catalog,
        bool canOperate)
    {
        if (!BabyDragonBuffFormula.HasYieldBuff(data))
        {
            return false;
        }

        AppendRow(
            StringTable.GetString(MULTIPLIER_LABEL_LOC_KEY),
            string.Format(MULTIPLIER_FORMAT, effectiveMultiplier));

        AppendRow(
            StringTable.GetString(TARGETS_LABEL_LOC_KEY),
            JoinResourceNames(data.BuffTargetResources, catalog));

        if (buffTargets == null || buffTargets.Count == 0)
        {
            // 굶주리면 BabyDragonBuffSystem이 대상을 아예 모으지 않는다 - 범위 안에 시설이 있어도
            // 빈 목록이 오므로, 그대로 "대상이 없다"고 쓰면 거짓말이 된다.
            AppendRow(
                StringTable.GetString(AFFECTED_LABEL_LOC_KEY),
                StringTable.GetString(canOperate ? NO_TARGET_LOC_KEY : STOPPED_LOC_KEY));

            return true;
        }

        for (int i = 0; i < buffTargets.Count; i++)
        {
            BabyDragonBuffTarget target = buffTargets[i];

            string line = string.Format(
                StringTable.GetString(FACTORY_LOC_KEY),
                ResolveFactoryName(target.Factory),
                ResolveResourceName(target.ResourceType, catalog),
                target.Factory.GetCurrentYield(target.ResourceType));

            // 라벨은 첫 줄에만 붙이고 나머지는 값만 이어 붙인다.
            if (i == 0)
            {
                AppendRow(StringTable.GetString(AFFECTED_LABEL_LOC_KEY), line);
                continue;
            }

            AppendLine(string.Format(StringTable.GetString(CONTINUED_ROW_LOC_KEY), line));
        }

        return true;
    }

    // 생산 버프가 없는 속성(불·얼음·시간)이 버프 모드에서 실제로 하는 일이 여기 있다.
    private static bool AppendTerrainList(
        IReadOnlyList<TerrainType> terrains, string formatLocKey, bool canOperate)
    {
        if (terrains == null || terrains.Count == 0)
        {
            return false;
        }

        string line = string.Format(
            StringTable.GetString(formatLocKey),
            JoinTerrainNames(terrains));

        AppendLine(MarkStopped(line, canOperate));

        return true;
    }

    // 지금은 어떤 속성도 오라 데이터를 갖고 있지 않지만, CanUseBuffMode가 오라만으로도 성립하므로
    // 여기서 빠지면 그런 개체가 "효과 없음"으로 잘못 표시된다.
    private static bool AppendTowerAura(BabyDragonData data, bool canOperate)
    {
        if (!data.HasTowerAura)
        {
            return false;
        }

        string value = string.Format(
            StringTable.GetString(RADIUS_VALUE_LOC_KEY),
            data.TowerAura.Radius);

        AppendRow(
            StringTable.GetString(AURA_LABEL_LOC_KEY),
            MarkStopped(value, canOperate));

        return true;
    }

    private static void AppendRow(string label, string value)
    {
        AppendLine(string.Format(StringTable.GetString(ROW_LOC_KEY), label, value));
    }

    private static void AppendLine(string line)
    {
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
        }

        BODY_BUILDER.Append(line);
    }

    private static string TargetLocKey(TargetMovementFilter filter) => filter switch
    {
        TargetMovementFilter.GroundOnly => TARGET_GROUND_LOC_KEY,
        TargetMovementFilter.AirOnly => TARGET_AIR_LOC_KEY,
        _ => TARGET_ALL_LOC_KEY,
    };

    // 커서를 올린 동안 주기적으로 다시 만들므로 목록을 매번 새로 할당하지 않는다.
    private static string JoinResourceNames(ResourceType mask, ResourceCatalog catalog)
    {
        JOIN_BUILDER.Clear();

        foreach (ResourceType resourceType in GridMap.EnumerateResourceFlags(mask))
        {
            AppendJoined(ResolveResourceName(resourceType, catalog));
        }

        return JOIN_BUILDER.ToString();
    }

    private static string JoinTerrainNames(IReadOnlyList<TerrainType> terrains)
    {
        JOIN_BUILDER.Clear();

        for (int i = 0; i < terrains.Count; i++)
        {
            AppendJoined(StringTable.GetString(TerrainLocKeys.NameLocKey(terrains[i])));
        }

        return JOIN_BUILDER.ToString();
    }

    private static void AppendJoined(string value)
    {
        if (JOIN_BUILDER.Length > 0)
        {
            JOIN_BUILDER.Append(SEPARATOR);
        }

        JOIN_BUILDER.Append(value);
    }

    private static string ResolveResourceName(ResourceType resourceType, ResourceCatalog catalog)
    {
        if (catalog != null && catalog.TryGet(resourceType, out ResourceData data))
        {
            return StringTable.GetString(data.NameLocKey);
        }

        return string.Empty;
    }

    private static string ResolveFactoryName(Factory factory) =>
        factory != null && factory.Data != null
            ? StringTable.GetString(factory.Data.NameLocKey)
            : string.Empty;

    private static string ResolveTitle(BabyDragonData data) =>
        string.Format(
            StringTable.GetString(BabyDragonLocKeys.TITLE_FORMAT),
            StringTable.GetString(DragonLocKeys.AttributeLocKey(data.DragonType)));
}
