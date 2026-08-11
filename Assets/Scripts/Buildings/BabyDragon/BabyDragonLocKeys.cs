/// <summary>
/// 새끼용 관련 로컬 키 중 두 개 이상의 스크립트가 함께 쓰는 것 (CLAUDE.md 커밋규칙 §3.2).
/// 관리창(클릭)과 툴팁(호버)이 같은 항목을 다른 형태로 보여주므로 라벨이 겹친다.
/// 한쪽에서만 쓰는 키는 그 스크립트의 private const로 둔다.
/// </summary>
public static class BabyDragonLocKeys
{
    // 관리창과 툴팁이 함께 쓰는 라벨.
    public const string TITLE_FORMAT = "baby_dragon_manage_title_format";
    public const string STATUS_LABEL = "baby_dragon_manage_status_label";
    public const string STATUS_ACTIVE = "baby_dragon_manage_status_active";
    public const string STATUS_STARVING = "baby_dragon_manage_status_starving";
    public const string FEED_LABEL = "baby_dragon_manage_feed_label";
    public const string MODE_ATTACK = "baby_dragon_manage_mode_attack";
    public const string MODE_BUFF = "baby_dragon_manage_mode_buff";
    public const string BUFF_MULTIPLIER_LABEL = "baby_dragon_manage_buff_multiplier_label";

    // 배율 표기. 두 화면이 같은 자릿수로 보여야 같은 값인지 한눈에 비교된다.
    public const string BUFF_MULTIPLIER_FORMAT = "×{0:0.##}";

    public static string ModeLocKey(BabyDragonMode mode) =>
        mode == BabyDragonMode.Buff ? MODE_BUFF : MODE_ATTACK;

    public static string StatusLocKey(bool canOperate) =>
        canOperate ? STATUS_ACTIVE : STATUS_STARVING;
}
