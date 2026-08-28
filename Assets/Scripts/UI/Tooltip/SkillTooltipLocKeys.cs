// 스킬 툴팁의 "현재 적용" 수치 줄이 쓰는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 도메인별 LocKeys 클래스를 두는 기존 관례(MonsterLocKeys, StatusLocKeys, DragonLocKeys)를 따른다.
// 줄 서식 자체는 MonsterLocKeys.TOOLTIP_ROW("{0} {1}")를 그대로 재사용한다 - 내용이
// 도메인과 무관한 범용 서식이라, 새로 똑같은 값을 하나 더 두면 나중에 둘이 어긋난다.
public static class SkillTooltipLocKeys
{
    public const string STAT_COOLDOWN = "skill_tooltip_stat_cooldown";
    public const string STAT_DAMAGE = "skill_tooltip_stat_damage";
    public const string STAT_HEAL = "skill_tooltip_stat_heal";
    public const string STAT_USES_PER_DAY = "skill_tooltip_stat_uses_per_day";

    // 값 서식. "{0}" 자리에 숫자만 들어간다.
    public const string VALUE_SECONDS = "skill_tooltip_value_seconds";
    public const string VALUE_PERCENT = "skill_tooltip_value_percent";
    public const string VALUE_COUNT = "skill_tooltip_value_count";
}
