using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private SpriteRenderer _aimingSprite;    

    private TowerData _towerData;
    private BaseMonster _target;
    private ITowerStaffing _staffing;
    private ITowerStatMultiplierQuery _statMultiplierQuery;
    private ITowerHitStatusQuery _hitStatusQuery;

    // 지역(지형) 페널티 조회원. ITowerStatMultiplierQuery는 TowerData만 받아 타워 종류 단위로만
    // 판정하므로, 같은 종류라도 서 있는 지역에 따라 달라지는 이 페널티는 담을 수 없다.
    // TerrainPenaltyCoordinator가 주입하며, 미배선 씬에서는 null로 남아 페널티 없이 동작한다.
    private IBuildingTerrainPenaltyQuery _terrainPenaltyQuery;
    private Tower _ownerTower;
    private TowerAuraSystem _auraSystem;
    private float _nextAttackTime;
    private bool _isAttackEnabled;

    private Animator _animator;

    [SerializeField] private bool _showDebugLogs;

    private AttackSO Attack => _towerData.Attack;

    // 대공/대지 전용 여부. 타겟 선정과 피해 적용(AttackContext를 타고 AttackSO까지)이
    // 반드시 같은 값을 봐야 한다.
    private TargetMovementFilter MovementFilter => _towerData.TargetMovementFilter;

    // 판정(브로드페이즈·정밀 타원)과 표시(사거리 원)가 반드시 같은 값을 봐야 하므로
    // 런타임 공격과 아직 Awake가 실행되지 않은 설치 프리팹이 아래 계산식을 공유한다.
    public float EffectiveRange => CalculateEffectiveRange(_towerData, _statMultiplierQuery);

    public static float CalculateEffectiveRange(
        TowerData towerData,
        ITowerStatMultiplierQuery statMultiplierQuery)
    {
        if (towerData == null || !towerData.CanAttack)
        {
            return 0f;
        }

        float rangeMultiplier = statMultiplierQuery != null
            ? statMultiplierQuery.GetRangeMultiplier(towerData)
            : 1f;

        return towerData.Attack.Range * rangeMultiplier;
    }

    private float AttackSpeedMultiplier
    {
        get
        {
            float globalMultiplier =
                _statMultiplierQuery != null
                ? _statMultiplierQuery
                    .GetAttackSpeedMultiplier(_towerData)
                : 1f;

            return globalMultiplier *
                ResolveAuraModifiers().AttackSpeedMultiplier *
                ResolveTerrainModifiers().AttackSpeedMultiplier;
        }
    }

    public BaseMonster CurrentTarget => _target;
    private static readonly int ATTACK_ANIM_KEY = Animator.StringToHash("Attack");

    private void Awake()
    {
        _ownerTower = GetComponent<Tower>();
        _staffing = GetComponent<ITowerStaffing>();
    }

    public void Initialize(TowerData towerData, Animator animator)
    {
        _towerData = towerData;
        _target = null;
        _nextAttackTime = Time.time;
        _isAttackEnabled = _towerData != null && _towerData.CanAttack;
        _animator = animator;
    }

    public void SetAttackEnabled(bool isEnabled)
    {
        _isAttackEnabled = isEnabled && _towerData != null && _towerData.CanAttack;

        if (!_isAttackEnabled)
        {
            _target = null;
        }
    }

    public void SetStatMultiplierQuery(
        ITowerStatMultiplierQuery statMultiplierQuery)
    {
        _statMultiplierQuery = statMultiplierQuery;
    }

    public void SetAuraSystem(TowerAuraSystem auraSystem)
    {
        _auraSystem = auraSystem;
    }

    public void SetHitStatusQuery(ITowerHitStatusQuery hitStatusQuery)
    {
        _hitStatusQuery = hitStatusQuery;
    }

    public void SetTerrainPenaltyQuery(
        IBuildingTerrainPenaltyQuery terrainPenaltyQuery)
    {
        _terrainPenaltyQuery = terrainPenaltyQuery;
    }

    private void Update()
    {
        if (!_isAttackEnabled || !CanAttackWithCurrentStaffing())
        {
            return;
        }

        if (!IsCurrentTargetValid())
        {
            _target = FindClosestTarget();

            // 타워 공격 디버깅용
            if (_showDebugLogs && _target != null)
            {
                Debug.Log($"[TowerAttack] {name}이(가) 타겟 {_target.name} 선정했습니다. (필터 {MovementFilter}, 대상 {_target.MovementType})", this);
            }
            //까지
        }


        if (_aimingSprite != null & _target != null)
        {
            float directionX = transform.position.x - _target.TargetTransform.position.x;
            _aimingSprite.flipX = directionX >= 0f;
        }

        if (_target == null || Time.time < _nextAttackTime)
        {
            return;
        }

        Fire();
        _nextAttackTime = Time.time + GetAttackInterval();
    }

    /// <summary>
    /// 다음 발사까지의 실제 간격(초). 연구·오라·지형이 반영된 값이라 데이터 원본(Attack.Interval)과 다르다 -
    /// 표시(툴팁)와 판정이 같은 값을 보게 하려고 노출한다. EffectiveRange와 같은 이유다.
    ///
    /// 아직 Setup 전이거나(데이터 없음) 지금 쏠 수 없으면 값이 성립하지 않으므로 false를 돌려준다 -
    /// 발사 스케줄은 그 경우를 무한대로 표현하지만, 표시하는 쪽에 "∞초"는 숫자가 아니라 오류로 보인다.
    /// </summary>
    public bool TryGetEffectiveAttackInterval(out float interval)
    {
        interval = 0f;

        // Attack 프로퍼티가 _towerData를 그대로 역참조하므로 간격을 구하기 전에 먼저 막는다.
        if (_towerData == null || !_towerData.CanAttack)
        {
            return false;
        }

        interval = GetAttackInterval();

        return !float.IsInfinity(interval);
    }

    private float GetAttackInterval()
    {
        // 인구 할당 생성에 실패한 타워는 구현체가 없다(CanAttackWithCurrentStaffing과 같은 이유).
        float staffingRatio = _staffing != null ? _staffing.StaffingRatio : 0f;

        if (staffingRatio <= 0f)
        {
            return float.PositiveInfinity;
        }

        return Attack.Interval / staffingRatio / AttackSpeedMultiplier;
    }

    private bool IsCurrentTargetValid()
    {
        if (_target == null || _target.IsDead)
        {
            return false;
        }

        // 사거리보다 먼저 본다 - 여기서 걸러주지 않으면 Update가 재탐색을 하지 않아
        // (재탐색은 현재 타겟이 무효일 때만 일어난다) 공격할 수 없는 적을 문 채로
        // 바로 옆의 유효한 적을 영영 찾지 못한다.
        if (!MovementFilter.Allows(_target.MovementType))
        {
            return false;
        }

        return IsWithinAttackRange(_target.transform.position);
    }

    // 판정 반경은 타일 종횡비를 반영한 타원이다 - 그리드 셀이 세로로 눌려있어(IsometricMath 참고)
    // 월드 좌표 기준 진짜 원으로 판정하면 세로 방향으로 타일 두 배만큼 더 멀리 닿는 비대칭이 생긴다.
    private bool IsWithinAttackRange(Vector3 targetPosition)
    {
        float radiusY = EffectiveRange * IsometricMath.RADIUS_Y_RATIO;
        return IsometricMath.IsWithinEllipse(targetPosition, transform.position, EffectiveRange, radiusY);
    }

    // 구현체가 없으면 공격하지 않는다 - 인구 할당 생성에 실패한 타워가 지금처럼
    // 침묵하도록 유지하기 위함(컴포넌트 부재를 만가동으로 오해하면 오설정이 묻힌다).
    private bool CanAttackWithCurrentStaffing()
    {
        return _staffing != null && _staffing.CanOperate;
    }

    // 후에 몬스터의 종류, 및 타워종류에 따라 공격 우선도 다르게
    private BaseMonster FindClosestTarget()
    {
        // 브로드페이즈: 타원의 두 반지름 중 더 큰 X 반지름의 원으로 넉넉히 후보를 모은 뒤
        // 타원 방정식으로 정확히 걸러낸다 (IsWithinAttackRange와 동일한 판정).
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            EffectiveRange,
            _targetLayers);

        BaseMonster closestTarget = null;
        float closestNormalizedDistanceSqr = float.PositiveInfinity;
        float radiusY = EffectiveRange * IsometricMath.RADIUS_Y_RATIO;

        foreach (Collider2D candidate in candidates)
        {
            BaseMonster monster = candidate.GetComponentInParent<BaseMonster>();
            if (monster == null ||
                monster.IsDead ||
                !MovementFilter.Allows(monster.MovementType))
            {
                continue;
            }

            float normalizedDistanceSqr = IsometricMath.EllipseNormalizedDistanceSqr(
                monster.transform.position, transform.position, EffectiveRange, radiusY);

            if (normalizedDistanceSqr > 1f || normalizedDistanceSqr >= closestNormalizedDistanceSqr)
            {
                continue;
            }

            closestTarget = monster;
            closestNormalizedDistanceSqr = normalizedDistanceSqr;
        }

        return closestTarget;
    }

    private TowerAuraModifiers ResolveAuraModifiers()
    {
        if (_auraSystem == null || _ownerTower == null)
        {
            return TowerAuraModifiers.Neutral;
        }

        return _auraSystem.ResolveModifiers(_ownerTower);
    }

    private TerrainPenaltyModifiers ResolveTerrainModifiers()
    {
        if (_terrainPenaltyQuery == null || _ownerTower == null)
        {
            return TerrainPenaltyModifiers.Neutral;
        }

        return _terrainPenaltyQuery.Resolve(_ownerTower);
    }

    /// <summary>
    /// 사거리 내 대상에 공격을 적용한다.
    /// 투사체가 설정된 경우 투사체가 피해를 운반해 명중 시점에 적용하고,
    /// 없으면 즉시 피해를 적용한다. (MonsterAttack과 동일한 패턴)
    /// </summary>
    private void Fire()
    {
        if (_showDebugLogs)
        {
            Debug.Log($"[TowerAttack] {name} → {_target.name} 공격 발사!", this);
        }

        SoundManager.Play(SoundId.TowerFire);

        float globalDamageMultiplier = _statMultiplierQuery != null
            ? _statMultiplierQuery.GetDamageMultiplier(_towerData)
            : 1f;

        TowerAuraModifiers auraModifiers =
            ResolveAuraModifiers();

        float damageMultiplier =
            globalDamageMultiplier *
            auraModifiers.DamageMultiplier;

        var damageModifier = new ResolvedEnemyStatModifier(0f, damageMultiplier);

        StatusEffectSO hitStatus = _hitStatusQuery?.GetTowerHitStatus(_towerData);
        StatusEffectSO[] extraStatuses = hitStatus != null
            ? new[] { hitStatus }
            : null;

        DragonType? attackElement =
            _towerData is IElementalAttackData elementalAttackData
                ? elementalAttackData.DragonType
                : null;

        AttackContext context = new AttackContext(
            gameObject,
            damageModifier,
            extraStatuses,
            _targetLayers,
            attackElement,
            MovementFilter);

        if(_animator != null)
        {
            _animator.SetTrigger(ATTACK_ANIM_KEY);
        }


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

        // 사거리만으로는 대공/대지 전용 타워를 구분할 수 없어 색으로 함께 보여준다.
        Gizmos.color = MovementFilter switch
        {
            TargetMovementFilter.GroundOnly => Color.green,
            TargetMovementFilter.AirOnly => Color.cyan,
            _ => Color.red,
        };

        // 실제 판정(IsWithinAttackRange)과 같은 타원을 그린다 - Gizmos엔 타원 API가 없으므로
        // Y축만 압축한 행렬로 원을 그려 근사한다.
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, IsometricMath.RADIUS_Y_RATIO, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, EffectiveRange);
        Gizmos.matrix = previousMatrix;
    }
#endif
}
