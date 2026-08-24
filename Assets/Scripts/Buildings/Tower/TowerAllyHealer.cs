using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 생명 타워 등, 범위 내 체력이 깎인 아군 타워를 찾아 회복(공격)을 발사하는 컴포넌트.
/// TowerAttack과 유사하게 동작하지만, BaseMonster가 아닌 아군 Tower를 타겟으로 삼는다.
/// </summary>
[RequireComponent(typeof(Tower))]
public class TowerAllyHealer : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private SpriteRenderer _aimingSprite;

    private Tower _ownerTower;
    private ITowerStaffing _staffing;
    private TowerAuraSystem _auraSystem;
    private GridMap _gridMap;
    private ITowerStatMultiplierQuery _statMultiplierQuery;
    
    private Tower _target;
    private float _nextHealTime;
    private Animator _animator;
    private static readonly int ATTACK_ANIM_KEY = Animator.StringToHash("Attack");

    private bool _isInitialized;

    private void Awake()
    {
        _ownerTower = GetComponent<Tower>();
        _staffing = GetComponent<ITowerStaffing>();
    }

    private void Start()
    {
        _gridMap = Object.FindFirstObjectByType<GridMap>();
        _animator = GetComponent<Animator>();
        _nextHealTime = Time.time;
        _isInitialized = true;
    }

    public void SetStatMultiplierQuery(ITowerStatMultiplierQuery statMultiplierQuery)
    {
        _statMultiplierQuery = statMultiplierQuery;
    }

    public void SetAuraSystem(TowerAuraSystem auraSystem)
    {
        _auraSystem = auraSystem;
    }

    private void Update()
    {
        if (!_isInitialized || _ownerTower.Data == null || !_ownerTower.Data.CanAttack)
            return;

        if (_ownerTower.IsDead || _ownerTower.IsParalyzed || _ownerTower.IsReviving)
            return;

        if (_staffing != null && !_staffing.CanOperate)
            return;

        if (!IsCurrentTargetValid())
        {
            _target = FindLowestHealthAlly();
        }

        if (_aimingSprite != null && _target != null)
        {
            float directionX = transform.position.x - _target.transform.position.x;
            _aimingSprite.flipX = directionX >= 0f;
        }

        if (_target == null || Time.time < _nextHealTime)
        {
            return;
        }

        Fire();
        _nextHealTime = Time.time + GetAttackInterval();
    }

    private bool IsCurrentTargetValid()
    {
        if (_target == null || _target.IsDead) return false;
        
        // 체력이 꽉 찬 타겟은 더 이상 유효하지 않다.
        Health targetHealth = _target.GetComponent<Health>();
        if (targetHealth == null || targetHealth.CurrentHealth >= targetHealth.MaxHealth)
        {
            return false;
        }

        // EffectiveRange는 읽을 때마다 사거리 배율을 다시 조회하는 파생값이다(아래 프로퍼티 참고).
        // FindLowestHealthAlly처럼 지역 변수로 한 번만 받는다.
        float radiusX = EffectiveRange;
        float radiusY = radiusX * IsometricMath.RADIUS_Y_RATIO;
        return IsometricMath.IsWithinEllipse(_target.transform.position, transform.position, radiusX, radiusY);
    }

    private Tower FindLowestHealthAlly()
    {
        if (_gridMap == null) return null;

        Tower bestTarget = null;
        float lowestHealthRatio = 1f;

        float range = EffectiveRange;
        float radiusY = range * IsometricMath.RADIUS_Y_RATIO;

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is not Tower targetTower || targetTower == _ownerTower || targetTower.IsDead)
            {
                continue;
            }

            Health targetHealth = targetTower.GetComponent<Health>();
            if (targetHealth == null || targetHealth.CurrentHealth >= targetHealth.MaxHealth)
            {
                continue;
            }

            if (!IsometricMath.IsWithinEllipse(targetTower.transform.position, transform.position, range, radiusY))
            {
                continue;
            }

            float healthRatio = targetHealth.CurrentHealth / targetHealth.MaxHealth;
            if (healthRatio < lowestHealthRatio)
            {
                lowestHealthRatio = healthRatio;
                bestTarget = targetTower;
            }
        }

        return bestTarget;
    }

    private float EffectiveRange
    {
        get
        {
            float rangeMultiplier = _statMultiplierQuery != null
                ? _statMultiplierQuery.GetRangeMultiplier(_ownerTower.Data)
                : 1f;
            return _ownerTower.Data.Attack.Range * rangeMultiplier;
        }
    }

    private float AttackSpeedMultiplier
    {
        get
        {
            float globalMultiplier = _statMultiplierQuery != null
                ? _statMultiplierQuery.GetAttackSpeedMultiplier(_ownerTower.Data)
                : 1f;

            float auraMultiplier = _auraSystem != null
                ? _auraSystem.ResolveModifiers(_ownerTower).AttackSpeedMultiplier
                : 1f;

            return globalMultiplier * auraMultiplier;
        }
    }

    private float GetAttackInterval()
    {
        float staffingRatio = _staffing != null ? _staffing.StaffingRatio : 0f;
        if (staffingRatio <= 0f) return float.PositiveInfinity;
        return _ownerTower.Data.Attack.Interval / staffingRatio / AttackSpeedMultiplier;
    }

    private void Fire()
    {
        SoundManager.Play(SoundId.TowerFire); // 타워 발사음 공용 사용

        float damageMultiplier = _statMultiplierQuery != null
            ? _statMultiplierQuery.GetDamageMultiplier(_ownerTower.Data)
            : 1f;

        if (_auraSystem != null)
        {
            damageMultiplier *= _auraSystem.ResolveModifiers(_ownerTower).DamageMultiplier;
        }

        var damageModifier = new ResolvedEnemyStatModifier(0f, damageMultiplier);

        // 아군 회복이므로 LayerMask는 무시(0)해도 됨
        AttackContext context = new AttackContext(
            gameObject,
            damageModifier,
            null,
            0,
            null,
            TargetMovementFilter.All);

        if (_animator != null)
        {
            _animator.SetTrigger(ATTACK_ANIM_KEY);
        }

        if (!_ownerTower.Data.HasProjectile)
        {
            _ownerTower.Data.Attack.Execute(_target, in context);
            return;
        }

        LaunchProjectile(_target, in context);
    }

    private void LaunchProjectile(Tower target, in AttackContext context)
    {
        Vector3 spawnPosition = _firePoint != null ? _firePoint.position : transform.position;
        GameObject projectileObject = Instantiate(_ownerTower.Data.ProjectilePrefab, spawnPosition, Quaternion.identity);

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Destroy(projectileObject);
            _ownerTower.Data.Attack.Execute(target, in context);
            return;
        }

        projectile.Launch(target, _ownerTower.Data.Attack, in context, _ownerTower.Data.ProjectileSpeed);
    }
}
