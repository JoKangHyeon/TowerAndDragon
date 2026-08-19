// 몬스터 툴팁이 쓰는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 전역 Defines 대신 도메인별 LocKeys 클래스를 두는 기존 관례(ResourceLocKeys, ResearchLocKeys, DragonLocKeys)를 따른다.
// 몬스터 이름·설명 키는 MonsterData가 직접 들고 있으므로 여기 포함하지 않는다.
public static class MonsterLocKeys
{
    // 툴팁 한 줄: "{0}" 스탯 이름, "{1}" 값.
    public const string TOOLTIP_ROW = "monster_tooltip_row";

    // 살아 있는 개체의 값: "{0}" 현재, "{1}" 최대. 예고 카드처럼 개체가 없으면 쓰지 않는다.
    public const string STAT_CURRENT_OF_MAX = "monster_stat_current_of_max";

    public const string STAT_HEALTH = "monster_stat_health";
    public const string STAT_MOVE_SPEED = "monster_stat_move_speed";
    public const string STAT_MOVEMENT = "monster_stat_movement";
    public const string STAT_SHIELD = "monster_stat_shield";
    public const string STAT_ELEMENT = "monster_stat_element";
    public const string STAT_TARGET = "monster_stat_target";

    private const string MOVEMENT_GROUND = "monster_movement_ground";
    private const string MOVEMENT_AIR = "monster_movement_air";

    // 속성 규칙은 속성 이름을 "{0}"으로 받는 서식이다 (예: "불 면역", "불 공격만 통함").
    // 속성 이름 자체는 DragonLocKeys.AttributeLocKey를 그대로 쓴다 - 여기 또 적으면
    // 속성 이름을 바꿀 때 용 쪽과 몬스터 쪽 표기가 조용히 어긋난다.
    private const string ELEMENT_RULE_IMMUNE = "monster_element_rule_immune";
    private const string ELEMENT_RULE_ONLY = "monster_element_rule_only";

    // 표적은 조합별로 키를 따로 둔다 - 구분자를 코드에서 붙이면 언어마다 다른 나열 순서·조사를
    // 스트링테이블에서 고칠 수 없게 된다.
    private const string TARGET_TOWER = "monster_target_tower";
    private const string TARGET_DRAGON = "monster_target_dragon";
    private const string TARGET_TOWER_AND_DRAGON = "monster_target_tower_and_dragon";

    public static string MovementLocKey(MonsterMovementType movementType)
    {
        return movementType switch
        {
            MonsterMovementType.Air => MOVEMENT_AIR,
            _ => MOVEMENT_GROUND,
        };
    }

    /// <summary>속성 규칙 문구의 서식 키. Normal은 표시할 것이 없으므로 빈 문자열을 돌려준다.</summary>
    public static string ElementRuleFormatLocKey(MonsterElementRule rule)
    {
        return rule switch
        {
            MonsterElementRule.OnlyMatchingElement => ELEMENT_RULE_ONLY,
            MonsterElementRule.ImmuneToMatchingElement => ELEMENT_RULE_IMMUNE,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// 이동 중 노리는 대상. 비트 조합이므로 값 비교가 아니라 플래그로 판정한다
    /// (데이터에 "전부"를 뜻하는 -1이 들어 있어 Tower|Dragon과 값이 다르다).
    /// 노리는 대상이 없으면 빈 문자열을 돌려준다.
    /// </summary>
    public static string TargetLocKey(MonsterTargetType targets)
    {
        bool hitsTower = (targets & MonsterTargetType.Tower) != 0;
        bool hitsDragon = (targets & MonsterTargetType.Dragon) != 0;

        if (hitsTower && hitsDragon)
        {
            return TARGET_TOWER_AND_DRAGON;
        }

        if (hitsTower)
        {
            return TARGET_TOWER;
        }

        if (hitsDragon)
        {
            return TARGET_DRAGON;
        }

        return string.Empty;
    }
}
