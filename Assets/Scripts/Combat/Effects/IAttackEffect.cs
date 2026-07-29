/// <summary>
/// 공격이 대상에 부여하는 개별 효과(데미지·둔화·화상 등)의 전략 계약.
/// 하나의 공격은 여러 효과를 조합해 적용하며, 효과별 파라미터는 각 구현이 보관한다.
/// 지속 효과(둔화 등)는 이 계약에서 직접 시간을 관리하지 않고,
/// 대상 쪽 상태 시스템에 위임한다. (상태 시스템은 [미정] — 팀 확정 후 도입)
/// </summary>
public interface IAttackEffect
{
    void Apply(IDamageable target, in AttackContext context);
}
