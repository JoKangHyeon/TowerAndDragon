using System.Collections.Generic;
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
    [SerializeField] private Transform _firePoint;

    private MonsterData _data;
    private AttackSO _attack;
    private MonsterTargetType _enRouteTargetTypes;
    private MonsterMovement _movement;
    private IMonsterTarget _currentTarget;
    private Castle _finalTarget;
    private ResolvedEnemyStatModifier _attackPowerModifier;
    private Animator _animator;
    private bool _isAutoAttackEnabled;

    private float _nextAttackTime;
    private bool _isInitialized;
    private int _animKeyEnemyAttack = Animator.StringToHash("EnemyAttack");


    public float Range => _attack.Range;
    public float Interval => _attack.Interval;

    public void Initialize(MonsterData data, MonsterMovement movement, Animator animator)
    {
        Initialize(data, movement,animator, ResolvedEnemyStatModifier.Neutral);
    }

    public void Initialize(
        MonsterData data,
        MonsterMovement movement,
        Animator animator,
        ResolvedEnemyStatModifier attackPowerModifier)
    {
        _animator= animator; 

        _data = data;
        _attack = data.Attack;
        _enRouteTargetTypes = data.EnRouteTargetTypes;
        _movement = movement;
        _attackPowerModifier = attackPowerModifier;

        _currentTarget = null;
        _nextAttackTime = Time.time;
        _isAutoAttackEnabled = true;
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized || !_isAutoAttackEnabled)
        {
            return;
        }

        if (_movement != null && _movement.HasArrived)
        {
            UpdateFinalTargetAttack();
            return;
        }

        UpdateEnRouteAttack();
    }

    private void UpdateEnRouteAttack()
    {
        if (_enRouteTargetTypes == MonsterTargetType.None ||
        _movement == null)
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
    /// 사거리 내 대상에 공격을 적용한다.
    /// 투사체가 설정된 경우(원거리) 투사체를 발사해 명중 시 피해를 적용하고,
    /// 없으면(근접) 즉시 피해를 적용한다.
    /// </summary>
    private void Fire(IAttackTarget target)
    {
        AttackContext context = new AttackContext(
            gameObject,
            _attackPowerModifier);

        if (!_data.HasProjectile)
        {
            _attack.Execute(target, in context);
            return;
        }

        _animator.SetTrigger(_animKeyEnemyAttack);
        LaunchProjectile(target, in context);
    }
    private void LaunchProjectile(IAttackTarget target, in AttackContext context)
    {
        Vector3 spawnPosition = _firePoint != null
            ? _firePoint.position
            : transform.position;

        GameObject projectileObject = Instantiate(
            _data.ProjectilePrefab,
            spawnPosition,
            Quaternion.identity);

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogError("[MonsterAttack] 투사체 프리팹에 Projectile이 없습니다.", projectileObject);
            Destroy(projectileObject);
            _attack.Execute(target, in context);
            return;
        }

        projectile.Launch(target, _attack, in context, _data.ProjectileSpeed);
    }

    public void SetFinalTarget(Castle target)
    {
        _currentTarget = null;
        _finalTarget = target;
        _movement?.Stop();
    }

    private void UpdateFinalTargetAttack()
    {
        if (_finalTarget == null || _finalTarget.IsDead)
        {
            return;
        }

        if (Time.time < _nextAttackTime)
        {
            return;
        }

        Fire(_finalTarget);
        _nextAttackTime = Time.time + Interval;
    }

    public void DisableAutoAttack()
    {
        _isAutoAttackEnabled = false;
    }

    public bool HasTargetInRange()
    {
        return FindClosestTarget() != null;
    }

    // 범위 만큼 폭발
    public void ExecuteBlast(float radius)
    {
        if (!_isInitialized || radius <= 0)
        {
            return;
        }

        AttackContext context = new AttackContext(gameObject, _attackPowerModifier);

        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            radius,
            _targetLayers
        );

        HashSet<IMonsterTarget> hitTargets= new HashSet<IMonsterTarget>();

        foreach (Collider2D candidate in candidates)
        {
            IMonsterTarget target = candidate.GetComponentInParent<IMonsterTarget>();

            if (target == null || target.IsDead || !CanAttackTargetType(target.TargetType) || !hitTargets.Add(target))
            {
                continue;
            }

            _attack.Execute(target, in context);
        }
    }
}
