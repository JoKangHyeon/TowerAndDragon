using UnityEngine;

/// <summary>
/// 대상에 즉발 데미지를 부여하는 효과. 기존 MonsterData의 attackDamage가
/// 이곳으로 이전된다. 방어막/속성 상성은 DamageInfo 확장으로 이후 반영한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Attack Effects/Damage", fileName = "DamageEffect")]
public class DamageEffectSO : AttackEffectSO
{
    [SerializeField] private float _amount;

    public float Amount => _amount;

    public override void Apply(IDamageable target, in AttackContext context)
    {
        target.TakeDamage(new DamageInfo(_amount));
    }
}