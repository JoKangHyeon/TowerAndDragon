using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private Transform _firePoint;

    private TowerData _towerData;
    private BaseMonster _target;
    private float _nextAttackTime;
    private bool _isAttackEnabled;

    [SerializeField] private bool _showDebugLogs;

    private AttackSO Attack => _towerData.Attack;

    public BaseMonster CurrentTarget => _target;

    public void Initialize(TowerData towerData)
    {
        _towerData = towerData;
        _target = null;
        _nextAttackTime = Time.time;
        _isAttackEnabled = _towerData != null && _towerData.CanAttack;
    }

    public void SetAttackEnabled(bool isEnabled)
    {
        _isAttackEnabled = isEnabled && _towerData != null && _towerData.CanAttack;

        if (!_isAttackEnabled)
        {
            _target = null;
        }
    }

    private void Update()
    {
        if (!_isAttackEnabled)
        {
            return;
        }

        if (!IsCurrentTargetValid())
        {
            _target = FindClosestTarget();

            // 타워 공격 디버깅용
            if (_showDebugLogs && _target != null)
            {
                Debug.Log($"[TowerAttack] {name}이(가) 타겟 {_target.name} 선정했습니다.", this);
            }
            //까지
        }

        if (_target == null || Time.time < _nextAttackTime)
        {
            return;
        }

        Fire();
        _nextAttackTime = Time.time + Attack.Interval;
    }

    private bool IsCurrentTargetValid()
    {
        if (_target == null || _target.IsDead)
        {
            return false;
        }

        return GetSqrDistance(_target.transform.position) <= Attack.Range * Attack.Range;
    }

    // 후에 몬스터의 종류, 및 타워종류에 따라 공격 우선도 다르게
    private BaseMonster FindClosestTarget()
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            Attack.Range,
            _targetLayers);

        BaseMonster closestTarget = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Collider2D candidate in candidates)
        {
            BaseMonster monster = candidate.GetComponentInParent<BaseMonster>();
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            float sqrDistance = GetSqrDistance(monster.transform.position);
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestTarget = monster;
            closestSqrDistance = sqrDistance;
        }

        return closestTarget;
    }

    private float GetSqrDistance(Vector3 targetPosition)
    {
        return (targetPosition - transform.position).sqrMagnitude;
    }

    /// <summary>
    /// 사거리 내 대상에 공격을 적용한다.
    /// 투사체가 설정된 경우 투사체가 피해를 운반해 명중 시점에 적용하고,
    /// 없으면 즉시 피해를 적용한다. (MonsterAttack과 동일한 패턴)
    /// </summary>
    private void Fire()
    {
        AttackContext context = new AttackContext(gameObject);

        if (!_towerData.HasProjectile)
        {
            Attack.Execute(_target, in context);
            return;
        }

        LaunchProjectile(_target, in context);
    }

    private void LaunchProjectile(BaseMonster target, in AttackContext context)
    {
        Vector3 spawnPosition = _firePoint != null
            ? _firePoint.position
            : transform.position;

        GameObject projectileObject = Instantiate(
            _towerData.ProjectilePrefab,
            spawnPosition,
            Quaternion.identity);

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogError("[TowerAttack] 투사체 프리팹에 Projectile이 없습니다.", projectileObject);
            Destroy(projectileObject);
            Attack.Execute(target, in context);
            return;
        }

        projectile.Launch(target, Attack, in context, _towerData.ProjectileSpeed);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_towerData == null || !_towerData.CanAttack)
        {
            return;
        }
    
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Attack.Range);
    }
#endif
}
