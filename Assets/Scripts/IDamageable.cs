/// <summary>
/// 데미지를 받을 수 있는 대상(적, 타워, 메인 성 등)의 공통 계약.
/// 공격자(타워 등)는 상대의 구체 타입을 몰라도 이 인터페이스로만 상호작용한다.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }

    void TakeDamage(DamageInfo damage);
    void Heal(float amount);
}
