using UnityEngine;

/// <summary>
/// 인스펙터에서 폴리모픽으로 직렬화·조합하기 위한 효과 SO의 추상 베이스.
/// 구체 효과(DamageEffectSO 등)는 이 클래스를 상속하고 CreateAssetMenu로
/// 애셋화하며, AttackSO의 효과 리스트에 드래그로 담는다.
/// 애셋은 여러 공격/유닛이 공유하므로 구현은 상태를 갖지 말 것
/// (런타임 상태는 실행 컴포넌트가 보관한다).
/// </summary>
public abstract class AttackEffectSO : ScriptableObject, IAttackEffect
{
    public abstract void Apply(IDamageable target, in AttackContext context);
}
