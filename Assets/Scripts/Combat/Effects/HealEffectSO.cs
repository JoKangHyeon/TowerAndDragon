using UnityEngine;

/// <summary>
/// 대상에 즉발 회복을 부여하는 효과. 생명 타워(Life Tower) 등이 사용한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Attack Effects/Heal", fileName = "HealEffect")]
public class HealEffectSO : AttackEffectSO
{
    [SerializeField] private float _amount;

    public float Amount => _amount;

    public override void Apply(IDamageable target, in AttackContext context)
    {
        // 공격력(Attack Power) 배율을 회복량에도 동일하게 적용한다.
        // 생명 타워는 강화 타워의 버프를 받으면 힐량도 증가한다.
        float amount = Mathf.Max(
            0f,
            context.AttackPowerModifier.Apply(_amount));
            
        target.Heal(amount);
    }
}
