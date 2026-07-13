using UnityEngine;

/// <summary>
/// 몬스터의 공격 실행 컴포넌트.
/// 이동 중 허용된 대상을 탐색해 공격하고,
/// 메인 성 도착 후에는 성을 최종 대상으로 공격한다.
/// 쿨다운과 현재 대상 같은 런타임 상태를 보관한다.
/// </summary>
public class MonsterAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers = Physics2D.DefaultRaycastLayers;

    private AttackSO _attack;
    private MonsterTargetType _enRouteTargetTypes;
    private MonsterMovement _movement;

    private IMonsterTarget _currentTarget;
    private float _nextAttackTime;
    private bool _isInitialized;

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
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized ||
            _enRouteTargetTypes == MonsterTargetType.None ||
            _movement == null ||
            _movement.HasArrived)
        {
            return;
        }

        if (_currentTarget != null && !IsCurrentTargetValid())
        {
            ClearCurrentTarget();
        }

        if (_currentTarget == null)
        {
            SetCurrentTarget(FindClosestTarget());
        }

        if (_currentTarget == null || Time.time < _nextAttackTime)
        {
            return;
        }

        Fire(_currentTarget);
        _nextAttackTime = Time.time + Interval;
    }

    private bool IsCurrentTargetValid()
    {
        if (_currentTarget is Object targetObject && targetObject == null)
        {
            return false;
        }

        if (_currentTarget.IsDead ||
            _currentTarget.TargetTransform == null ||
            !CanAttackTargetType(_currentTarget.TargetType))
        {
            return false;
        }

        return GetSqrDistance(_currentTarget.TargetTransform.position) <= Range * Range;
    }

    private IMonsterTarget FindClosestTarget()
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            Range,
            _targetLayers);

        IMonsterTarget closestTarget = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Collider2D candidate in candidates)
        {
            IMonsterTarget target = candidate.GetComponentInParent<IMonsterTarget>();
            if (target == null ||
                target.IsDead ||
                target.TargetTransform == null ||
                !CanAttackTargetType(target.TargetType))
            {
                continue;
            }

            float sqrDistance = GetSqrDistance(target.TargetTransform.position);
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestTarget = target;
            closestSqrDistance = sqrDistance;
        }

        return closestTarget;
    }

    private bool CanAttackTargetType(MonsterTargetType targetType)
    {
        return (_enRouteTargetTypes & targetType) != MonsterTargetType.None;
    }

    private float GetSqrDistance(Vector3 targetPosition)
    {
        return (targetPosition - transform.position).sqrMagnitude;
    }

    private void SetCurrentTarget(IMonsterTarget target)
    {
        if (target == null)
        {
            return;
        }

        _currentTarget = target;
        _movement.Stop();
    }

    private void ClearCurrentTarget()
    {
        _currentTarget = null;
        _movement.Begin();
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
