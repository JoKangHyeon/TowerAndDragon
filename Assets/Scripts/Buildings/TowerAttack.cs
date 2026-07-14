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

    private void Fire()
    {
        AttackContext context = new AttackContext(gameObject);
        Attack.Execute(_target, in context);

        if (_towerData.ProjectilePrefab == null || _towerData.ProjectileSpeed <= 0)
        {
            Debug.LogWarning("[TowerAttack] 투사체 프리팹 또는 투사체 속도가 설정되지 않았습니다.", this);
            return;
        }

        Vector3 spawnPosition = _firePoint != null
            ? _firePoint.position
            : transform.position;

        GameObject projectileObject = Instantiate(
            _towerData.ProjectilePrefab,
            spawnPosition,
            Quaternion.identity);

        TowerProjectile projectile = projectileObject.GetComponent<TowerProjectile>();
        if (projectile == null)
        {
            Debug.LogError("[TowerAttack] 투사체 프리팹에 TowerProjectile이 없습니다.", projectileObject);
            Destroy(projectileObject);
            return;
        }

        projectile.Launch(_target.transform, _towerData.ProjectileSpeed);
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
