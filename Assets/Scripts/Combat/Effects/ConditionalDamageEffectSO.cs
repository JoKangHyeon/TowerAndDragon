using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Attack Effects/Conditional Damage",
    fileName = "ConditionalDamageEffect"
)]
public sealed class ConditionalDamageEffectSO : AttackEffectSO
{
    [Tooltip("대상이 이 상태이상 id를 갖고 있어야 발동한다 - 화상 애셋의 status id와 일치 시킬것")]
    [SerializeField] private string _requiredStatusId;

    [Min(0f)]
    [SerializeField] private float _bonusAmount;

    public override void Apply(IDamageable target, in AttackContext context)
    {
        if (target is not IStatusEffectTarget statusTarget ||
            !statusTarget.HasStatus(_requiredStatusId))
        {
            return;
        }

        float amount = Mathf.Max(0f, context.AttackPowerModifier.Apply(_bonusAmount));
        target.TakeDamage(new DamageInfo(amount, context.AttackElement));
    }

}