using UnityEngine;

/// <summary>
/// 하나의 공격 정의. 사거리·발동 간격과 함께 부여할 효과 목록을 보관한다.
/// 타워와 적이 동일 애셋을 공유할 수 있어, "화염 화살(데미지+화상)" 같은
/// 조합을 코드 수정 없이 기획자가 애셋으로 만든다.
///
/// Execute는 효과 리스트를 대상에 순차 적용할 뿐 상태를 갖지 않는다.
/// 쿨다운 등 런타임 상태는 이 애셋을 사용하는 실행 컴포넌트(MonsterAttack 등)가 보관한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Attack", fileName = "Attack")]
public class AttackSO : ScriptableObject
{
    [Header("Firing")]
    [SerializeField] private float _range;
    [SerializeField] private float _interval;

    [Header("Effects")]
    [SerializeField] private AttackEffectSO[] _effects;

    public float Range => _range;
    public float Interval => _interval;

    public void Execute(IDamageable target, in AttackContext context)
    {
        foreach (AttackEffectSO effect in _effects)
        {
            effect.Apply(target, in context);
        }
    }
}
