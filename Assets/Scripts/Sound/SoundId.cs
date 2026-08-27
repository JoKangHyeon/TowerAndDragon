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

    // 아래는 타워 전투 효과음(Docs/타워_전투_효과음_설계.md §5).
    // SoundCatalog가 정수로 직렬화하므로 중간에 끼우지 말고 끝에만 추가한다.
    TowerManaLaunch,
    TowerManaResolve,
    TowerCrossbowLaunch,
    TowerCrossbowResolve,
    TowerMusketLaunch,
    TowerMusketResolve,
    TowerAntiAirLaunch,
    TowerAntiAirResolve,
    TowerFlameLaunch,
    TowerFlameResolve,
    TowerFrostLaunch,
    TowerFrostResolve,
    TowerBoulderLaunch,
    TowerBoulderResolve,
    TowerLifeLaunch,
    TowerLifeResolve,
    TowerAuraOn,

    // 새끼용 공격 모드 효과음. 새끼용도 TowerData를 상속하므로 같은 Launch/Resolve 구조를 쓴다.
    BabyDragonFireLaunch,
    BabyDragonFireResolve,
    BabyDragonIceLaunch,
    BabyDragonIceResolve,
    BabyDragonTimeLaunch,
    BabyDragonTimeResolve,
    BabyDragonStoneLaunch,
    BabyDragonStoneResolve,
    BabyDragonLifeLaunch,
    BabyDragonLifeResolve,
}
