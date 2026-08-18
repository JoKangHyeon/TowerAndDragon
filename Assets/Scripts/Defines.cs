public static class Defines
{
    public const string ENEMY_LAYER_NAME = "Enemy";

    // 애니메이터 파라미터 이름. PixelWorld 임포트 컨트롤러가 정한 이름이라 바꿀 수 없고,
    // 몬스터와 인구 배치 연출 캐릭터가 같은 컨트롤러를 쓰므로 여기 모은다(CLAUDE.md 커밋규칙 3.2).
    public const string ANIM_MOVE = "Move";
    public const string ANIM_ENEMY_ATTACK = "EnemyAttack";

    // 안내가 지금 막고 있다는 사유 문구. 튜토리얼 러너와 새끼용 가이드가 같은 문구를 쓴다 -
    // 플레이어에게는 어느 안내가 막았는지가 아니라 "안내를 따라오면 된다"만 전해지면 된다.
    public const string TUTORIAL_BLOCKED_HINT_LOC_KEY = "tutorial_blocked_hint";
}
