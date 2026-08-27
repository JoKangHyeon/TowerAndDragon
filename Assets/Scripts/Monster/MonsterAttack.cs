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

        if (_animator != null)
            _animator.SetTrigger(_animKeyEnemyAttack);

        if (!_data.HasProjectile)
        {
            _attack.Execute(target, in context);
            PlayHitVfx(target);
            return;
        }

        LaunchProjectile(target, in context);
    }

    // 근거리·보스 전용 명중 연출. 원거리는 ProjectileVisual.OnHit이 대신 그린다(AttackSO 참고).
    // target이 null이면(ExecuteBlast의 광역) TargetBody를 고를 대상이 없으므로 발밑으로 그린다 -
    // 시전자 발밑 앵커(CasterGround)와 결과가 같아 별도 분기 없이 안전하게 흐른다.
    //
    // 성 공격은 여기서 거른다. Fire()가 이동 중 타워·방벽 공격과 도착 후 성 공격에 같은 AttackSO를
    // 쓰므로(구조가 갈려 있지 않다) target의 실제 타입으로 판단해야 한다 - 성은 스프라이트가 커서
    // 이펙트가 가려지고, 앵커가 CasterGround(보스)라도 시전자 자체가 성 앞에 붙어 있어 마찬가지로
    // 어색하다. 방벽은 성만큼 크지 않아 대상에서 뺀다. target이 null인 광역(ExecuteBlast)은 성을
    // 때릴 수 없으므로(Castle은 IMonsterTarget이 아니다) 이 가드에 걸리지 않는다.
    private void PlayHitVfx(IAttackTarget target)
    {
        if (!_attack.HasHitVfx || target is Castle || IsDestroyed(target))
        {
            return;
        }

        if (_attack.HitVfxAnchor == AttackVfxAnchor.TargetBody && target != null)
        {
            Vector3 bodyPosition = AttackVfxPlacement.ResolveVisualImpactPosition(
                target.TargetTransform.position, target.TargetObject, ProjectileImpactPlacement.Body);

            ProjectilePool.PlayForSeconds(
                _attack.HitVfxPrefab,
                bodyPosition,
                AttackVfxPlacement.ResolveDirectionRotation(bodyPosition - transform.position),
                _attack.HitVfxLifetimeSeconds);
            return;
        }

        // _movement를 넘겨 ResolveGroundY가 GetComponent<MonsterMovement>를 다시 돌지 않게 한다 -
        // 이 컴포넌트는 Initialize에서 이미 캐시해 뒀다. x도 raw transform.position이 아니라
        // 스프라이트 bounds 중심을 쓴다 - TargetBody 분기와 같은 기준이어야 피벗이 중앙이 아닌
        // 몬스터(발밑 이펙트가 옆으로 밀려 보이는 프리팹)에서도 어긋나지 않는다.
        Vector3 groundPosition = AttackVfxPlacement.ResolveVisualImpactPosition(
            transform.position, gameObject, ProjectileImpactPlacement.Ground, _movement);

        ProjectilePool.PlayForSeconds(
            _attack.HitVfxPrefab, groundPosition, Quaternion.identity, _attack.HitVfxLifetimeSeconds);
    }

    // IsCurrentTargetValid(133번째 줄)와 같은 이유다 - target은 인터페이스 타입이라 Unity가
    // 오버로드한 == 연산자를 타지 않는다. Destroy() 직후(같은 프레임, GC 전)에도 `target != null`이
    // 여전히 true를 반환할 수 있어, 이 검사가 없으면 그 틈을 놓친다.
    private static bool IsDestroyed(IAttackTarget target)
    {
        return target is Object unityObject && unityObject == null;
    }

    private void LaunchProjectile(IAttackTarget target, in AttackContext context)
    {
        Vector3 spawnPosition = _firePoint != null
            ? _firePoint.position
            : transform.position;

        // 타워와 같은 풀을 쓴다 - Projectile이 풀로 반납하기 시작했으므로 여기가 Instantiate로
        // 남으면 그 인스턴스는 돌아갈 곳이 없다.
        Projectile projectile = ProjectilePool.Spawn(_data.ProjectilePrefab, spawnPosition);

        if (projectile == null)
        {
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

        // 대상 수만큼 도는 Execute와 달리 "공격 1회"를 아는 것은 여기뿐이라 루프 밖에서 한 번만 띄운다.
        PlayHitVfx(null);

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
