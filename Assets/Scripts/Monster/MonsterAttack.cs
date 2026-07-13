using UnityEngine;

/// <summary>
/// 몬스터의 공격 실행 컴포넌트.
/// 이동 중 허용된 대상을 탐색해 공격하고,
/// 메인 성 도착 후에는 성을 최종 대상으로 공격한다.
/// 쿨다운과 현재 대상 같은 런타임 상태를 보관한다.
/// </summary>
public class MonsterAttack : MonoBehaviour
{
    private AttackSO _attack;
    private MonsterTargetType _enRouteTargetTypes;
    private MonsterMovement _movement;

    private IMonsterTarget _currentTarget;
    private float _nextAttackTime;

    public float Range => _attack.Range;
    public float Interval => _attack.Interval;

    public void Initialize(
        AttackSO attack,
        MonsterTargetType enRouteTargetTypes,
        MonsterMovement movement)
    {
        _attack = attack;
        _enRouteTargetTypes = enRouteTargetTypes;
        _movement = movement;

        _currentTarget = null;
        _nextAttackTime = Time.time;
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
