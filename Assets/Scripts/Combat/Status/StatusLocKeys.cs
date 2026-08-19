// 상태(개체 상태이상·전역 강화) 표시가 쓰는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 도메인별 LocKeys 클래스를 두는 기존 관례(MonsterLocKeys, ResourceLocKeys, DragonLocKeys)를 따른다.
// 상태 이름 키는 StatusEffectSO 애셋이 직접 들고 있으므로 여기 포함하지 않는다 -
// 새 상태를 추가할 때 코드를 고치지 않아도 되게 한다.
public static class StatusLocKeys
{
    // 상태 구역 제목. 개체와 전역을 반드시 나눠 적는다 - 섞으면 새끼용 화상이
    // 전역 강화처럼 읽혀 "이 적만 지금 타고 있다"는 판단이 불가능해진다.
    public const string SECTION_INSTANCE = "status_section_instance";
    public const string SECTION_GLOBAL = "status_section_global";

    // 남은 시간: "{0}" 초.
    public const string ROW_DURATION = "status_row_duration";

    // 지속시간이 없는 상태(불 패시브 상시 화상 등)에 시간 대신 적는다.
    public const string ROW_INFINITE = "status_row_infinite";

    // 스택: "{0}" 현재, "{1}" 임계치.
    public const string ROW_STACKS = "status_row_stacks";

    // 전역 강화 줄의 이름. 스탯 이름은 몬스터 툴팁 쪽(MonsterLocKeys.STAT_*)을 그대로 쓰지 않는다 -
    // 그쪽은 "체력 42 / 120"의 이름이고 이쪽은 "체력 +30%"의 이름이라, 언어에 따라
    // 다르게 적어야 할 수 있다.
    public const string ENHANCEMENT_MAX_HEALTH = "status_enhancement_max_health";
    public const string ENHANCEMENT_SHIELD = "status_enhancement_shield";
    public const string ENHANCEMENT_ATTACK_POWER = "status_enhancement_attack_power";
    public const string ENHANCEMENT_MOVE_SPEED = "status_enhancement_move_speed";

    // 강화 수치: "{0}" 부호를 포함한 백분율 (예: "+30").
    public const string ENHANCEMENT_PERCENT = "status_enhancement_percent";
}
