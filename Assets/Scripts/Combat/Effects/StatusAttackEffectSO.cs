using UnityEngine;

// 상태이상 1개를 대상에 부여하는 공격 효과. 기존 AttackSO._effects 배열에 드래그만 하면
// "슬로우 타워"·"화상 타워" 같은 조합이 코드 수정 없이 완성된다(DamageEffectSO와 조합 가능).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Attack Effects/Status",
    fileName = "StatusAttackEffect")]
public sealed class StatusAttackEffectSO : AttackEffectSO
{
    [SerializeField] private StatusEffectSO _status;

    public override void Apply(IDamageable target, in AttackContext context)
    {
        if (_status != null && target is IStatusEffectTarget statusTarget)
        {
            statusTarget.ApplyStatus(_status);
        }
    }
}
