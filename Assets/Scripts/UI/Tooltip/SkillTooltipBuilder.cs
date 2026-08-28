using System.Text;

/// <summary>
/// 스킬 하나를 툴팁 문구로 만든다. 밤 HUD의 스킬 아이콘 호버와 용 스킬트리 설명문 안의
/// [스킬명] 링크 호버가 이 하나를 함께 쓴다 - 같은 스킬을 두 화면에서 서로 다른 문구로
/// 보여주면 어느 쪽이 최신인지 알 수 없게 된다.
///
/// 설명문(dragon_skill_*_desc)은 "쿨다운 60초"처럼 기본값을 문장으로 담고 있을 뿐, 용
/// 스킬트리 강화 노드의 보너스는 반영하지 않는다. 그래서 설명문 아래에 "지금 실제로
/// 적용된" 수치 줄을 따로 붙인다(MonsterTooltipBuilder와 같은 자리 - 설명문과 스탯을
/// 분리해서, 설명문은 고쳐 쓰지 않고 스탯만 갱신되게 한다).
/// </summary>
public static class SkillTooltipBuilder
{
    // 위력·쿨다운 보너스는 데이터상 float이지만 대부분 정수다. 소수점 이하는 있을 때만 두 자리까지.
    private const string STAT_NUMBER_FORMAT = "0.##";

    private const int UNLIMITED_USE_PER_DAY = -1;

    private static readonly StringBuilder BODY_BUILDER = new();

    /// <param name="dragonTreeManager">강화 노드의 보너스를 조회할 매니저. 비우면 기본값(보너스 0)으로 표시한다.</param>
    public static TooltipContent Build(SkillSO data, DragonTreeManager dragonTreeManager = null)
    {
        if (data == null)
        {
            return default;
        }

        BODY_BUILDER.Clear();
        // 뒤에 붙는 첫 스탯 줄과 사이에 빈 줄 하나를 두기 위해 AppendLine으로 끝맺는다
        // (MonsterTooltipBuilder.AppendDescription과 같은 방식).
        BODY_BUILDER.AppendLine(StringTable.GetString(data.DescriptionStringKey));

        AppendCurrentStats(data, dragonTreeManager);

        return new TooltipContent(StringTable.GetString(data.NameStringKey), BODY_BUILDER.ToString());
    }

    private static void AppendCurrentStats(SkillSO data, DragonTreeManager dragonTreeManager)
    {
        float cooldownReduction = dragonTreeManager != null
            ? dragonTreeManager.GetSkillCooldownReductionRatio(data)
            : 0f;
        float powerBonus = dragonTreeManager != null
            ? dragonTreeManager.GetSkillPowerMultiplierBonus(data)
            : 0f;

        float cooldown = data.DefaultCooltime * (1f - cooldownReduction);
        AppendRow(
            SkillTooltipLocKeys.STAT_COOLDOWN,
            string.Format(StringTable.GetString(SkillTooltipLocKeys.VALUE_SECONDS), cooldown.ToString(STAT_NUMBER_FORMAT)));

        // 체력비례 피해와 고정 피해는 같은 스킬에서 동시에 쓰이지 않는다 - 데이터에 있는 쪽만 보여준다.
        if (data.DamagePercentOfCurrentHealth > 0f)
        {
            float damagePercent = data.DamagePercentOfCurrentHealth * (1f + powerBonus) * 100f;
            AppendRow(
                SkillTooltipLocKeys.STAT_DAMAGE,
                string.Format(StringTable.GetString(SkillTooltipLocKeys.VALUE_PERCENT), damagePercent.ToString(STAT_NUMBER_FORMAT)));
        }
        else if (data.FlatDamage > 0f)
        {
            float flatDamage = data.FlatDamage * (1f + powerBonus);
            AppendRow(SkillTooltipLocKeys.STAT_DAMAGE, flatDamage.ToString(STAT_NUMBER_FORMAT));
        }

        if (data.HealAmount > 0f)
        {
            float healAmount = data.HealAmount * (1f + powerBonus);
            AppendRow(SkillTooltipLocKeys.STAT_HEAL, healAmount.ToString(STAT_NUMBER_FORMAT));
        }

        if (data.DefaultUsePerDay != UNLIMITED_USE_PER_DAY)
        {
            int extraUses = dragonTreeManager != null ? dragonTreeManager.GetSkillExtraUsePerDay(data) : 0;
            int usePerDay = data.DefaultUsePerDay + extraUses;
            AppendRow(
                SkillTooltipLocKeys.STAT_USES_PER_DAY,
                string.Format(StringTable.GetString(SkillTooltipLocKeys.VALUE_COUNT), usePerDay));
        }
    }

    private static void AppendRow(string labelLocKey, string value)
    {
        BODY_BUILDER.AppendLine();
        BODY_BUILDER.AppendFormat(
            StringTable.GetString(MonsterLocKeys.TOOLTIP_ROW),
            StringTable.GetString(labelLocKey),
            value);
    }
}
