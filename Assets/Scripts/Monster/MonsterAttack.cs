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
    private BaseMonster _owner;
    private AttackSO _attack;
    private MonsterTargetType _enRouteTargetTypes;
    private MonsterMovement _movement;
    private IMonsterTarget _currentTarget;
    private Collider2D _currentTargetCollider;
    private Castle _finalTarget;
    private ResolvedEnemyStatModifier _attackPowerModifier;
    private Animator _animator;
    private bool _isAutoAttackEnabled;

    private float _nextAttackTime;
    private bool _isInitialized;
    private int _animKeyEnemyAttack = Animator.StringToHash("EnemyAttack");


    public float Range => _attack.Range;
    public float Interval => _attack.Interval;

    // 정지해서 공격 중일 때 스프라이트가 바라볼 대상. 공격 중이 아니면 null.
    public Transform FacingTargetTransform
    {
        get
        {
            if (!_isInitialized)
            {
                return null;
            }

            if (_currentTarget != null && IsCurrentTargetValid())
            {
                return _currentTarget.TargetTransform;
            }

            if (_movement != null && _movement.HasArrived &&
                _finalTarget != null && !_finalTarget.IsDead)
            {
                return _finalTarget.TargetTransform;
            }

            return null;
        }
    }

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
        _owner = GetComponent<BaseMonster>();
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
        if (!_isInitialized ||
            !_isAutoAttackEnabled ||
            _owner == null ||
            !_owner.CanAct)
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
        if (_movement == null)
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
            !CanAttackTarget(_currentTarget))
        {
            return false;
        }

        if (_currentTargetCollider != null)
        {
            Vector3 closest = _currentTargetCollider.ClosestPoint(transform.position);
            return (closest - transform.position).sqrMagnitude <= Range * Range;
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
                !CanAttackTarget(target))
            {
                continue;
            }

            Vector3 closestPoint = candidate.ClosestPoint(transform.position);
            float sqrDistance = (closestPoint - transform.position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestTarget = target;
            closestSqrDistance = sqrDistance;
        }

        return closestTarget;
    }

    private bool CanAttackTarget(IMonsterTarget target)
    {
        // 방벽(StoneBarricade)은 몬스터의 타겟 설정(EnRouteTargetTypes)과 무관하게
        // 길을 물리적으로 가로막고 있으므로 무조건 공격해서 뚫고 지나가도록 합니다.
        if (target is StoneBarricade)
        {
            return true;
        }
        return (_enRouteTargetTypes & target.TargetType) != MonsterTargetType.None;
    }

    private bool CanDamageTarget(IMonsterTarget target)
    {
        // 광역 폭발 시에는 은신(TargetType = None)이더라도 원래의 타겟 타입을 기준으로 피해를 줍니다.
        if (target is StoneBarricade)
        {
            return true;
        }
        return (_enRouteTargetTypes & target.BaseTargetType) != MonsterTargetType.None;
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
        _currentTargetCollider = target.TargetObject.GetComponentInChildren<Collider2D>();
        _movement.Stop();
    }

    private void ClearCurrentTarget()
    {
        _currentTarget = null;
        _currentTargetCollider = null;
        _movement.Begin();
    }

    /// <summary>
    /// 사거리 내 대상에 공격을 적용한다.
    /// 투사체가 설정된 경우(원거리) 투사체를 발사해 명중 시 피해를 적용하고,
    /// 없으면(근접) 즉시 피해를 적용한다.
    /// </summary>
    private void Fire(IAttackTarget target)
    {
        if (_owner == null || !_owner.CanAct)
        {
            return;
        }

        AttackContext context = new AttackContext(
            gameObject,
            _attackPowerModifier,
            _targetLayers);

        if (!_data.HasProjectile)
        {
            _attack.Execute(target, in context);
            return;
        }

        if (_animator != null)
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
        if (!_isInitialized || 
            radius <= 0 ||
            _owner == null ||
            !_owner.CanAct)
        {
            return;
        }

        AttackContext context = new AttackContext(gameObject, _attackPowerModifier, _targetLayers);

        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            radius,
            _targetLayers
        );


        HashSet<IMonsterTarget> hitTargets= new HashSet<IMonsterTarget>();

        foreach (Collider2D candidate in candidates)
        {
            IMonsterTarget target = candidate.GetComponentInParent<IMonsterTarget>();


            if (target == null || target.IsDead || !CanDamageTarget(target) || !hitTargets.Add(target))
            {
                continue;
            }


            _attack.Execute(target, in context);
        }
    }
}
