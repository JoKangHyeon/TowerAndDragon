using System.Collections.Generic;
using UnityEngine;

public enum SkillType
{
    DEBUG,
    SINGLE_CURRENT_HEALTH_DAMAGE,
    AREA_CURRENT_HEALTH_DAMAGE,

    // 용 스킬트리 액티브(로드맵 §6-1). 얼음=모든 적 빙결, 시간=파괴된 타워 즉시 수리.
    // 불의 "화상 틱뎀 가속 / 전역 대미지"는 GLOBAL_CURRENT_HEALTH_DAMAGE 하나로 근사한다 -
    // SkillSO.AppliedStatus를 함께 채우면 전역 대미지 + 화상 재부여를 동시에 표현할 수 있다.
    FREEZE_ALL,
    GLOBAL_CURRENT_HEALTH_DAMAGE,
    REPAIR_TOWERS,

    // 생명 액티브(로드맵 §6-1) 임시 대체 - 바리케이드는 설치 대상 프리팹이 없어
    // 기획 확정 전까지 성 즉시 회복으로 대신한다(팀 확인 대기, DragonSkillTreeAssetGenerator 참고).
    HEAL_CASTLE,
    METEOR_BARRICADE,
}

/// <summary>스킬 발동 시 무엇을 지정해야 하는지 - UI/입력 쪽에서 이 값에 따라 지정 방식을 분기한다.</summary>
public enum SkillTargeting
{
    Instant,     // 지정 없이 즉시 발동
    GroundPoint, // 월드 지점(광역 등) 지정
    Enemy,       // 적 1기 지정
}

/// <summary>스킬 발동에 필요한 대상 정보를 한 번에 묶어 전달한다. 필요 없는 필드는 비워 둔다
/// (예: GroundPoint/Enemy 스킬은 AllMonsters/AllBuildings를 안 쓴다).</summary>
public readonly struct SkillCastContext
{
    public Vector3 TargetPoint { get; }
    public BaseMonster TargetEnemy { get; }
    public GameObject Caster { get; }

    // 용 스킬트리 전역형 액티브(빙결·전역 대미지·타워 수리) 전용 - Instant 스킬만 채워서 넘긴다.
    public IReadOnlyList<BaseMonster> AllMonsters { get; }
    public IEnumerable<Building> AllBuildings { get; }

    // 생명 액티브(성 즉시 회복) 전용 - Instant 스킬만 채워서 넘긴다.
    public Castle TargetCastle { get; }

    public SkillCastContext(Vector3 targetPoint, BaseMonster targetEnemy, GameObject caster)
        : this(targetPoint, targetEnemy, caster, null, null, null)
    {
    }

    public SkillCastContext(
        Vector3 targetPoint,
        BaseMonster targetEnemy,
        GameObject caster,
        IReadOnlyList<BaseMonster> allMonsters,
        IEnumerable<Building> allBuildings)
        : this(targetPoint, targetEnemy, caster, allMonsters, allBuildings, null)
    {
    }

    public SkillCastContext(
        Vector3 targetPoint,
        BaseMonster targetEnemy,
        GameObject caster,
        IReadOnlyList<BaseMonster> allMonsters,
        IEnumerable<Building> allBuildings,
        Castle targetCastle)
    {
        TargetPoint = targetPoint;
        TargetEnemy = targetEnemy;
        Caster = caster;
        AllMonsters = allMonsters;
        AllBuildings = allBuildings;
        TargetCastle = targetCastle;
    }
}



public abstract class Skill
{
    private const int UNLIMITED_USE_PER_DAY = -1;
    private const float ANTI_SPAM_DELAY = 1f;

    private readonly SkillSO _skillData;

    private float _cooltimeLeft;
    private float _chargeRechargeTimer;
    private int _charges;

    // 용 스킬트리 강화/궁극 노드의 위력·쿨다운 보너스 조회원 - SkillManager가 생성 직후 주입한다.
    // 없으면(용 스킬트리와 무관한 스킬) 보너스 0으로 취급한다.
    private DragonTreeManager _dragonTreeManager;

    protected Skill(SkillSO skillData)
    {
        _skillData = skillData;
        Reset();
    }

    // SkillManager.AvailableSkills가 DragonTreeManager.AvailableActiveSkills(SkillSO 목록)와
    // 대조해 필터링하는 데 쓴다 - 인스턴스가 아니라 데이터로 키잉해야 재바인딩에도 안전하다.
    public SkillSO Data => _skillData;

    public void SetDragonTreeManager(DragonTreeManager dragonTreeManager)
    {
        _dragonTreeManager = dragonTreeManager;
    }

    public string Name
    {
        get
        {
            return StringTable.GetString(_skillData.NameStringKey);
        }
    }

    public string Description
    {
        get
        {
            return StringTable.GetString(_skillData.DescriptionStringKey);
        }
    }

    public Sprite Icon => _skillData.Sprite;

    // 강화·궁극 노드(DragonSkillPowerEffectSO)의 쿨다운 감소 비율을 반영한다 - 이 값을 읽지 않으면
    // 해당 10개 노드를 해금해도 쿨다운이 전혀 줄지 않는다.
    public float ChargeCooltime
    {
        get
        {
            float reduction = _dragonTreeManager != null
                ? _dragonTreeManager.GetSkillCooldownReductionRatio(_skillData)
                : 0f;

            return _skillData.DefaultCooltime * (1f - reduction);
        }
    }

    public float CooltimeLeft => (IsUnlimitedUse || _charges > 0) ? _cooltimeLeft : _chargeRechargeTimer;
    
    public float CooltimeRatio 
    {
        get 
        {
            if (IsUnlimitedUse)
            {
                return ChargeCooltime > 0f ? Mathf.Min(_cooltimeLeft / ChargeCooltime, 1f) : 0f;
            }
            else if (_charges > 0)
            {
                return _cooltimeLeft > 0f ? Mathf.Min(_cooltimeLeft / ANTI_SPAM_DELAY, 1f) : 0f;
            }
            else
            {
                return ChargeCooltime > 0f ? Mathf.Min(_chargeRechargeTimer / ChargeCooltime, 1f) : 0f;
            }
        }
    }
    
    public int UsePerDayLeft => _charges;

    // 궁극 노드(DragonSkillPowerEffectSO._extraUsePerDay)의 추가 횟수를 반영한다.
    // 무제한(-1) 스킬에 더하면 -1이 깨져 IsUnlimitedUse가 false가 되므로 먼저 걸러낸다.
    public int UsePerDay
    {
        get
        {
            if (_skillData.DefaultUsePerDay == UNLIMITED_USE_PER_DAY)
            {
                return UNLIMITED_USE_PER_DAY;
            }

            int extra = _dragonTreeManager != null
                ? _dragonTreeManager.GetSkillExtraUsePerDay(_skillData)
                : 0;

            return _skillData.DefaultUsePerDay + extra;
        }
    }
    public bool IsUnlimitedUse => UsePerDay == UNLIMITED_USE_PER_DAY;
    public bool CanUse => IsUsePerDayLeft && _cooltimeLeft <= 0;
    public bool IsUsePerDayLeft => IsUnlimitedUse || UsePerDayLeft > 0;

    /// <summary>이 스킬이 발동 시 무엇을 지정해야 하는지 - 타겟팅 컨트롤러가 이 값으로 분기한다.</summary>
    public abstract SkillTargeting Targeting { get; }

    // 타겟팅 컨트롤러가 "커서 아래에서 어떤 레이어를 주울지" 판단하는 데도 필요하므로 public으로 노출한다.
    public LayerMask TargetLayers => _skillData.TargetLayers;

    // 강화·궁극 노드의 위력 보너스를 반영한다 - Meteor/GlobalDamage처럼 체력비례 데미지를 쓰는
    // 스킬만 실질적으로 영향을 받는다(빙결·타워수리는 값을 읽지 않음).
    protected float DamagePercent => _skillData.DamagePercentOfCurrentHealth * (1f + PowerBonus);

    // 체력 비례가 아닌 고정 피해량(메테오). DamagePercent와 같은 위력 보너스를 받는다.
    protected float FlatDamage => _skillData.FlatDamage * (1f + PowerBonus);

    // 강화 노드가 더 강한 상태이상(지속시간이 긴 빙결 등)으로 교체할 수 있다 -
    // 교체분이 없으면 SkillSO의 기본값을 그대로 쓴다.
    protected StatusEffectSO AppliedStatus
    {
        get
        {
            StatusEffectSO overridden = _dragonTreeManager != null
                ? _dragonTreeManager.GetSkillStatusOverride(_skillData)
                : null;

            return overridden != null ? overridden : _skillData.AppliedStatus;
        }
    }

    // 생명 액티브(성 즉시 회복) 전용 회복량. 위력 보너스를 반영하지 않으면
    // 생명 갈래의 "회복량 증가" 노드가 아무 효과도 내지 못한다.
    protected float HealAmount => _skillData.HealAmount * (1f + PowerBonus);

    // 설치형 스킬(방벽)처럼 SkillSO 수치가 아닌 값을 강화해야 하는 스킬이 매니저를 직접 조회한다.
    protected DragonTreeManager DragonTree => _dragonTreeManager;

    private float PowerBonus => _dragonTreeManager != null
        ? _dragonTreeManager.GetSkillPowerMultiplierBonus(_skillData)
        : 0f;

    // 타겟팅 컨트롤러가 시전 범위 미리보기(원형 인디케이터) 크기를 결정하는 데도 필요하므로 public으로 노출한다.
    public float AreaRadius => _skillData.AreaRadius;

    public void Tick(float deltaTime)
    {
        if (_cooltimeLeft > 0)
        {
            _cooltimeLeft -= deltaTime;
            if (_cooltimeLeft < 0)
                _cooltimeLeft = 0;
        }

        if (!IsUnlimitedUse && _charges < UsePerDay && _chargeRechargeTimer > 0f)
        {
            _chargeRechargeTimer -= deltaTime;
            if (_chargeRechargeTimer <= 0)
            {
                _charges++;
                if (_charges < UsePerDay)
                {
                    _chargeRechargeTimer += ChargeCooltime;
                }
                else
                {
                    _chargeRechargeTimer = 0f;
                }
            }
        }
    }

    /// <summary>스킬 발동 진입점. 사용 가능할 때만 효과를 적용하고 쿨타임/사용횟수를 소비한다.</summary>
    public void Activate(in SkillCastContext context)
    {
        if (!CanUse)
            return;

        if (ApplyEffect(in context))
        {
            SpendResources();
        }
    }

    /// <summary>타겟팅 모드 중 커서 위치에 따른 스킬의 가시적 프리뷰(실루엣 등)를 업데이트한다.</summary>
    public virtual void UpdatePreview(Vector3 targetPoint) { }

    /// <summary>타겟팅 모드가 끝날 때 프리뷰 관련 리소스를 정리한다.</summary>
    public virtual void ClearPreview() { }

    /// <summary>마우스 좌표를 받아 스킬이 실제로 적용될 중심점을 반환한다 (스냅 처리용).</summary>
    public virtual Vector3 GetTargetCenter(Vector3 pointerWorldPosition)
    {
        return pointerWorldPosition;
    }

    /// <summary>실제 스킬 효과 - 서브클래스가 구현한다. 실패 시 false를 반환하면 자원을 소모하지 않는다.</summary>
    protected abstract bool ApplyEffect(in SkillCastContext context);

    private void SpendResources()
    {
        if (IsUnlimitedUse)
        {
            // 무제한 스킬(얼음, 불 등)은 스택(Charge) 개념이 없으므로, 기본 쿨타임 자체를 쿨다운으로 적용한다.
            _cooltimeLeft = ChargeCooltime;
        }
        else
        {
            // 스택 기반 스킬(메테오)은 1초의 연사 방지 대기 시간만 주고 스택을 소모한다.
            _cooltimeLeft = ANTI_SPAM_DELAY; 
            _charges -= 1;
            
            if (_charges < UsePerDay && _chargeRechargeTimer <= 0)
            {
                _chargeRechargeTimer = ChargeCooltime;
            }
        }
    }

    public void Reset()
    {
        _cooltimeLeft = 0f;
        _chargeRechargeTimer = 0f;
        _charges = UsePerDay;
    }
}

public class DebugSkill : Skill
{
    public DebugSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        Debug.Log("Skill Actived");
        return true;
    }
}

/// <summary>지정한 적 1기에게 현재 체력 비례 데미지를 준다.</summary>
public class SingleCurrentHealthDamageSkill : Skill
{
    public SingleCurrentHealthDamageSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Enemy;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        BaseMonster target = context.TargetEnemy;

        if (target == null || target.IsDead)
            return false;

        target.TakeDamage(new DamageInfo(target.CurrentHealth * DamagePercent));
        return true;
    }
}

/// <summary>지정한 지점을 중심으로, 아이소메트릭 타일 비율에 맞춰 납작해진 타원 범위 안의
/// 모든 적에게 각자 현재 체력 비례 데미지를 준다.</summary>
public class AreaCurrentHealthDamageSkill : Skill
{
    public AreaCurrentHealthDamageSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.GroundPoint;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        float radiusX = AreaRadius;
        float radiusY = AreaRadius * IsometricMath.RADIUS_Y_RATIO;

        // 브로드페이즈: 더 큰 쪽인 X 반지름의 원으로 넉넉히 후보를 모은 뒤 타원 방정식으로 정확히 걸러낸다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(context.TargetPoint, radiusX, TargetLayers);
        HashSet<BaseMonster> targets = new HashSet<BaseMonster>();

        foreach (Collider2D hit in hits)
        {
            BaseMonster monster = hit.GetComponentInParent<BaseMonster>();

            if (monster == null)
                continue;

            if (!IsometricMath.IsWithinEllipse(monster.transform.position, context.TargetPoint, radiusX, radiusY))
                continue;

            targets.Add(monster);
        }

        foreach (BaseMonster monster in targets)
        {
            if (monster.IsDead)
                continue;

            monster.TakeDamage(new DamageInfo(monster.CurrentHealth * DamagePercent));
        }
        return true;
    }
}

/// <summary>용 스킬트리 얼음 액티브(모든 적 빙결) - 발동 시점에 스폰돼 있는 모든 몬스터에
/// SkillSO.AppliedStatus(보통 이동속도 0의 MoveSpeedStatusSO)를 부여한다.</summary>
public class FreezeAllSkill : Skill
{
    public FreezeAllSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        if (AppliedStatus == null || context.AllMonsters == null)
            return false;

        foreach (BaseMonster monster in context.AllMonsters)
        {
            if (monster != null && !monster.IsDead)
            {
                monster.ApplyStatus(AppliedStatus);
            }
        }
        return true;
    }
}

/// <summary>용 스킬트리 불 액티브(전역 대미지 / 화상 재부여) - 발동 시점에 스폰돼 있는 모든
/// 몬스터에게 각자 현재 체력 비례 데미지를 주고, AppliedStatus가 설정돼 있으면 함께 다시 건다.
/// 기획의 "화상 틱뎀 가속 or 전역 대미지"를 하나의 스킬로 근사한 것 - AppliedStatus를 비워두면
/// 순수 전역 대미지, 채우면 대미지 + 화상 갱신이 된다.</summary>
public class GlobalCurrentHealthDamageSkill : Skill
{
    public GlobalCurrentHealthDamageSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        if (context.AllMonsters == null)
            return false;

        foreach (BaseMonster monster in context.AllMonsters)
        {
            if (monster == null || monster.IsDead)
                continue;

            monster.TakeDamage(new DamageInfo(monster.CurrentHealth * DamagePercent));

            if (AppliedStatus != null)
            {
                monster.ApplyStatus(AppliedStatus);
            }
        }
        return true;
    }
}

/// <summary>용 스킬트리 시간 액티브(파괴된 타워 즉시 수리) - 현재 비활성화(파괴) 상태인 모든
/// 타워를 재활성화 대기시간 없이 즉시 복구한다. 밤 전투 중 복구는 편의 연구 해금 조건을 따른다.
///
/// 타워의 "하룻밤 재활성화 횟수" 제한은 이 스킬에 걸리지 않는다 - 그 밤에 이미 스스로 일어섰던
/// 타워도 여기서는 다시 세운다(Tower.TryRestoreDuringCombat 주석 참고).</summary>
public class RepairTowersSkill : Skill
{
    public RepairTowersSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        if (context.AllBuildings == null)
            return false;

        bool restoredAny = false;
        foreach (Building building in context.AllBuildings)
        {
            if (building is Tower tower && tower.IsDead)
            {
                restoredAny |= tower.TryRestoreDuringCombat();
            }
        }
        return restoredAny;
    }
}

/// <summary>용 스킬트리 생명 액티브 임시 대체(성 즉시 회복) - 바리케이드는 설치 대상 프리팹이 없어
/// 기획 확정 전까지 성 즉시 회복으로 대신한다(팀 확인 대기). Castle.Repair를 재사용한다
/// (convenience_castle_regen_1이 매일 낮 자동 회복에 쓰는 것과 동일 경로) - 사망 상태 가드는
/// Castle.Repair가 호출하는 Health.Heal이 담당하므로 여기서 다시 검사하지 않는다.</summary>
public class HealCastleSkill : Skill
{
    public HealCastleSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        if (context.TargetCastle != null)
        {
            context.TargetCastle.Repair(HealAmount);
            return true;
        }
        return false;
    }
}

/// <summary>용 스킬트리 암석 액티브 - 지정 위치에 광역 데미지를 주고 방벽을 설치합니다.</summary>
public class MeteorBarricadeSkill : Skill
{
    private const int BARRICADE_SNAP_DISTANCE = 2;
    private GameObject _previewInstance;
    private GridMap _gridMap;
    private Building _buildingPrefab;

    private void EnsureCached()
    {
        if (_gridMap == null)
            _gridMap = Object.FindFirstObjectByType<GridMap>();
        if (_buildingPrefab == null && Data.BarricadePrefab != null)
            _buildingPrefab = Data.BarricadePrefab.GetComponent<Building>();
    }

    public MeteorBarricadeSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.GroundPoint;

    public override Vector3 GetTargetCenter(Vector3 pointerWorldPosition)
    {
        EnsureCached();
        if (_gridMap == null || _buildingPrefab == null) return pointerWorldPosition;

        Vector3Int gridPos = _gridMap.PickCellAtWorldPoint(pointerWorldPosition);
        
        if (_gridMap.TryResolveTemporaryObstacleAnchor(
                gridPos, _buildingPrefab.BaseFootprintShape, BARRICADE_SNAP_DISTANCE, out Vector3Int anchor))
        {
            return _gridMap.GetFootprintCenterWorld(anchor, _buildingPrefab.BaseFootprintShape)
                + _buildingPrefab.ComputePlacementOffset(0)
                + _gridMap.ComputeRotationCompensation(_buildingPrefab.BaseFootprintShape, 0);
        }
        
        return pointerWorldPosition;
    }

    public override void UpdatePreview(Vector3 targetPoint)
    {
        EnsureCached();
        if (_gridMap == null || _buildingPrefab == null) return;

        Vector3Int gridPos = _gridMap.PickCellAtWorldPoint(targetPoint);
        
        bool canPlace = _gridMap.TryResolveTemporaryObstacleAnchor(
                gridPos, _buildingPrefab.BaseFootprintShape, BARRICADE_SNAP_DISTANCE, out Vector3Int anchor);

        if (canPlace)
        {
            if (_previewInstance == null)
            {
                _previewInstance = Object.Instantiate(Data.BarricadePrefab);
                
                // 로직 비활성화
                Building building = _previewInstance.GetComponent<Building>();
                if (building != null) building.enabled = false;
                
                Collider2D[] colliders = _previewInstance.GetComponentsInChildren<Collider2D>();
                foreach (var col in colliders) col.enabled = false;

                // 반투명 처리
                SpriteRenderer[] renderers = _previewInstance.GetComponentsInChildren<SpriteRenderer>();
                foreach (var r in renderers)
                {
                    Color c = r.color;
                    c.a = 0.5f;
                    r.color = c;
                    r.sortingOrder = 32767; // 최상단 노출
                }
            }
            
            _previewInstance.SetActive(true);

            Vector3 worldPos = _gridMap.GetFootprintCenterWorld(anchor, _buildingPrefab.BaseFootprintShape)
                + _buildingPrefab.ComputePlacementOffset(0)
                + _gridMap.ComputeRotationCompensation(_buildingPrefab.BaseFootprintShape, 0);

            _previewInstance.transform.position = worldPos;
        }
        else
        {
            if (_previewInstance != null)
            {
                _previewInstance.SetActive(false);
            }
        }
    }

    public override void ClearPreview()
    {
        if (_previewInstance != null)
        {
            Object.Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    protected override bool ApplyEffect(in SkillCastContext context)
    {
        EnsureCached();
        // 1. [설치 가능 여부 검증 파트] 방벽을 세울 수 있는 경로 칸인지 가장 먼저 검사합니다.
        if (_gridMap == null || _buildingPrefab == null) return false;

        Vector3Int gridPos = _gridMap.PickCellAtWorldPoint(context.TargetPoint);

        if (!_gridMap.TryResolveTemporaryObstacleAnchor(
                gridPos, _buildingPrefab.BaseFootprintShape, BARRICADE_SNAP_DISTANCE, out Vector3Int anchor))
        {
            string reason = _gridMap.MonsterPathQuery == null
                ? "씬에 MonsterPathMap이 없습니다(조회원 미등록)"
                : $"주변 {BARRICADE_SNAP_DISTANCE}칸 안에 설치 가능한 몬스터 경로 칸이 없습니다";

            Debug.LogWarning($"[MeteorBarricadeSkill] 방벽 설치 실패 - 셀 {gridPos}: {reason}");
            return false; // 여기서 false를 반환하면 쿨타임과 스택이 차감되지 않습니다.
        }

        // 2. [데미지 파트] 설치가 확실시되었으므로 데미지를 먼저 줍니다.
        float radiusX = AreaRadius;
        float radiusY = AreaRadius * IsometricMath.RADIUS_Y_RATIO;
        LayerMask layers = TargetLayers != 0 ? TargetLayers : LayerMasks.Enemy;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(context.TargetPoint, radiusX, layers);
        HashSet<BaseMonster> targets = new HashSet<BaseMonster>();

        foreach (var hit in hitColliders)
        {
            BaseMonster monster = hit.GetComponentInParent<BaseMonster>();
            if (monster != null && !monster.IsDead && IsometricMath.IsWithinEllipse(monster.transform.position, context.TargetPoint, radiusX, radiusY))
            {
                targets.Add(monster);
            }
        }

        foreach (BaseMonster monster in targets)
        {
            monster.TakeDamage(new DamageInfo(FlatDamage));
        }

        // 3. [설치 파트] 데미지 부여 후 방벽 설치를 마저 진행합니다.

        // ConstructBuilding은 일반 건설 판정(점령 여부, 지형 등)을 거치기 때문에 몬스터 경로(길/미점령) 위에서는 항상 실패합니다.
        // 따라서 직접 위치를 잡아 Instantiate 한 뒤 RegisterFootprint로 우회하여 강제 등록합니다.
        Vector3 baseOffset = _buildingPrefab.transform.localPosition;
        Vector3 baseScale = _buildingPrefab.transform.localScale;
        Vector3 worldPos = _gridMap.GetFootprintCenterWorld(anchor, _buildingPrefab.BaseFootprintShape)
            + _buildingPrefab.ComputePlacementOffset(0)
            + _gridMap.ComputeRotationCompensation(_buildingPrefab.BaseFootprintShape, 0);

        Building spawned = Object.Instantiate(
            _buildingPrefab,
            worldPos,
            _buildingPrefab.transform.rotation,
            _gridMap.transform);

        spawned.SetPlacementOffset(baseOffset);
        spawned.SetBaseScale(baseScale);
        spawned.SetRotation(0);
        // SetDepthSortOrder는 RegisterFootprint 내부에서 호출됩니다.

        if (_gridMap.RegisterFootprint(spawned, anchor))
        {
            if (spawned is StoneBarricade barricade)
            {
                float healthMultiplier = DragonTree != null ? DragonTree.GetBarricadeHealthMultiplier() : 1f;
                barricade.Initialize(healthMultiplier);
                barricade.RegisterAutoDestroy(Object.FindFirstObjectByType<CycleManager>());
            }
            return true;
        }
        else
        {
            Object.Destroy(spawned.gameObject);
            return false;
        }
    }
}
