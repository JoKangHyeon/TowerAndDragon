/// <summary>
/// 효과음(SE) 식별자. SoundCatalog가 이 값을 실제 AudioClip에 매핑한다.
/// 코드에서는 문자열 대신 이 enum만 쓴다(CLAUDE.md 3번 - 문자열 리터럴 금지).
/// BGM은 원샷으로 재생하면 안 되므로 <see cref="BgmId"/>로 분리해 둔다.
/// </summary>
public enum SoundId
{
    UiButtonClick,
    UiWindowOpen,
    UiWindowClose,
    BuildPlace,
    BuildBlocked,
    TowerFire,
    MonsterHit,
    MonsterDeath,
    CastleDamaged,
    WaveStart,
    ResearchComplete,
    ConquestComplete,
    GameOver,
    Victory,
}
