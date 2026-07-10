using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(TowerAttack))]
public class Tower : Building, IDamageable
{

    [SerializeField] private TowerData _towerData;

    private Health _health;
    private TowerAttack _attack;

    public bool IsDead => _health == null || _health.IsDead;

    public TowerAttack Attack => _attack;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _attack = GetComponent<TowerAttack>();
    }

    private void Setup(TowerData data)
    {
        data = _towerData;
        _health.Initialize(data.MaxHealth);
        _health.Died += HandleDisabled;

        _attack.Initialize(data.Attack);

    }

    public void TakeDamage(DamageInfo damage)
    {
        throw new System.NotImplementedException();
    }

    private void HandleDisabled()
    {
        // 공격 중지
        // 부활 대기 시작
    }

    public void RestoreAtMorning()
    {
        // 체력 완전 회복
        // 비활성 상태라면 재활성화 정책 확인
    }
}
