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

    // 굳은 맹세(sworn_element)가 이번 주기의 속성 변경을 이미 소진했다는 사유 문구.
    // 용 창(변경 버튼)과 메인 성 창(상태 줄)이 같은 문구를 쓰므로 여기에 둔다 -
    // DRAGON_TUTORIAL_LOCKED_LOC_KEY와 같은 형태다.
    public const string DRAGON_TYPE_CHANGE_CYCLE_LIMIT_LOC_KEY = "dragon_type_change_cycle_limit";

    // 안내가 아직 가르치지 않은 어미용 조작(스킬 해금·속성 변경)을 막았다는 사유 문구.
    // 스킬트리 상세 패널의 상태 줄과 튜토리얼 토스트가 같은 문구를 쓴다 - 어느 경로로 막혔든
    // 플레이어가 읽는 이유는 하나여야 한다.
    public const string DRAGON_TUTORIAL_LOCKED_LOC_KEY = "dragon_state_tutorial_locked";
}
