/// <summary>
/// 근거리·보스 공격의 명중 연출을 어느 기준점에 놓을지. 원거리는 이 값을 쓰지 않는다 -
/// 투사체 명중 연출은 <see cref="ProjectileImpactPlacement"/>를 쓰는 <see cref="ProjectileVisual"/>이
/// (<c>MonsterData._projectilePrefab</c>에 이미 배선돼) 대신 그린다.
/// </summary>
public enum AttackVfxAnchor
{
    /// <summary>대상 몸통 중앙. 근거리 공격이 "맞았다"를 알려야 할 때.</summary>
    TargetBody,

    /// <summary>공격을 시전한 몬스터의 발밑. 보스·광역(자폭·마비)이 "무겁다"를 알려야 할 때.</summary>
    CasterGround,
}
