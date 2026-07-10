using UnityEngine;

/// <summary>
/// 선택 컴포넌트. 타워 공격형(원거리) 적에만 부착한다.
/// 공격 정의(AttackSO)를 주입받아 실행하는 "실행자" 역할이다.
/// 대상 선정(가장 가까운 타워 등)과 발사 루프는 팀 확정 후 구현한다. [미정]
///
/// 쿨다운 등 런타임 상태는 공유 애셋인 AttackSO가 아니라 이 컴포넌트가 보관한다.
/// </summary>
public class MonsterAttack : MonoBehaviour
{
    private AttackSO _attack;

    public float Range => _attack.Range;
    public float Interval => _attack.Interval;

    public void Initialize(AttackSO attack)
    {
        _attack = attack;
    }

    /// <summary>
    /// 사거리 내 대상에 공격을 적용한다. 실제 타깃팅·쿨다운 소비는 [미정] 발사 루프에서 호출한다.
    /// </summary>
    private void Fire(IDamageable target)
    {
        AttackContext context = new AttackContext(gameObject);
        _attack.Execute(target, in context);
    }
}
