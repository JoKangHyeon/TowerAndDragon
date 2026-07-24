using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private Transform _firePoint;

    private TowerData _towerData;
    private BaseMonster _target;
    private TowerPopulation _towerPopulation;
    private ITowerDamageMultiplierQuery _damageMultiplierQuery;
    private float _nextAttackTime;
    private bool _isAttackEnabled;

    [SerializeField] private bool _showDebugLogs;

    private AttackSO Attack => _towerData.Attack;

    public BaseMonster CurrentTarget => _target;

    private void Awake()
    {
        _towerPopulation = GetComponent<TowerPopulation>();
    }

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

    public void SetDamageMultiplierQuery(
        ITowerDamageMultiplierQuery damageMultiplierQuery)
    {
        _damageMultiplierQuery = damageMultiplierQuery;
    }

    private void Update()
    {
        if (!_isAttackEnabled || !CanAttackWithCurrentPopulation())
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
        _nextAttackTime = Time.time + GetAttackInterval();
    }

    private float GetAttackInterval()
    {
        float staffingRatio = _towerPopulation.StaffingRatio;

        if (staffingRatio <= 0f)
        {
            return float.PositiveInfinity;
        }

        return Attack.Interval / staffingRatio;
    }

    private bool IsCurrentTargetValid()
    {
        if (_target == null || _target.IsDead)
        {
            return false;
        }

        return IsWithinAttackRange(_target.transform.position);
    }

    // 판정 반경은 타일 종횡비를 반영한 타원이다 - 그리드 셀이 세로로 눌려있어(IsometricMath 참고)
    // 월드 좌표 기준 진짜 원으로 판정하면 세로 방향으로 타일 두 배만큼 더 멀리 닿는 비대칭이 생긴다.
    private bool IsWithinAttackRange(Vector3 targetPosition)
    {
        float radiusY = Attack.Range * IsometricMath.RADIUS_Y_RATIO;
        return IsometricMath.IsWithinEllipse(targetPosition, transform.position, Attack.Range, radiusY);
    }

    private bool CanAttackWithCurrentPopulation()
    {
        return _towerPopulation != null &&
            _towerPopulation.IsInitialized &&
            _towerPopulation.HasAssignedPopulation;
    }

    // 후에 몬스터의 종류, 및 타워종류에 따라 공격 우선도 다르게
    private BaseMonster FindClosestTarget()
    {
        // 브로드페이즈: 타원의 두 반지름 중 더 큰 X 반지름의 원으로 넉넉히 후보를 모은 뒤
        // 타원 방정식으로 정확히 걸러낸다 (IsWithinAttackRange와 동일한 판정).
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            Attack.Range,
            _targetLayers);

        BaseMonster closestTarget = null;
        float closestNormalizedDistanceSqr = float.PositiveInfinity;
        float radiusY = Attack.Range * IsometricMath.RADIUS_Y_RATIO;

        foreach (Collider2D candidate in candidates)
        {
            BaseMonster monster = candidate.GetComponentInParent<BaseMonster>();
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            float normalizedDistanceSqr = IsometricMath.EllipseNormalizedDistanceSqr(
                monster.transform.position, transform.position, Attack.Range, radiusY);

            if (normalizedDistanceSqr > 1f || normalizedDistanceSqr >= closestNormalizedDistanceSqr)
            {
                continue;
            }

            closestTarget = monster;
            closestNormalizedDistanceSqr = normalizedDistanceSqr;
        }

        return closestTarget;
    }

    /// <summary>
    /// 사거리 내 대상에 공격을 적용한다.
    /// 투사체가 설정된 경우 투사체가 피해를 운반해 명중 시점에 적용하고,
    /// 없으면 즉시 피해를 적용한다. (MonsterAttack과 동일한 패턴)
    /// </summary>
    private void Fire()
    {
        float damageMultiplier = _damageMultiplierQuery != null
            ? _damageMultiplierQuery.GetDamageMultiplier(_towerData)
            : 1f;
        var damageModifier = new ResolvedEnemyStatModifier(0f, damageMultiplier);
        AttackContext context = new AttackContext(gameObject, damageModifier);

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

        // 실제 판정(IsWithinAttackRange)과 같은 타원을 그린다 - Gizmos엔 타원 API가 없으므로
        // Y축만 압축한 행렬로 원을 그려 근사한다.
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, IsometricMath.RADIUS_Y_RATIO, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, Attack.Range);
        Gizmos.matrix = previousMatrix;
    }
#endif
}
