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

    public SkillCastContext(Vector3 targetPoint, BaseMonster targetEnemy, GameObject caster)
        : this(targetPoint, targetEnemy, caster, null, null)
    {
    }

    public SkillCastContext(
        Vector3 targetPoint,
        BaseMonster targetEnemy,
        GameObject caster,
        IReadOnlyList<BaseMonster> allMonsters,
        IEnumerable<Building> allBuildings)
    {
        TargetPoint = targetPoint;
        TargetEnemy = targetEnemy;
        Caster = caster;
        AllMonsters = allMonsters;
        AllBuildings = allBuildings;
    }
}



public abstract class Skill
{
    private const int UNLIMITED_USE_PER_DAY = -1;

    private readonly SkillSO _skillData;

    private float _cooltimeLeft;
    private int _usePerDayLeft;

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
    public float Cooltime
    {
        get
        {
            float reduction = _dragonTreeManager != null
                ? _dragonTreeManager.GetSkillCooldownReductionRatio(_skillData)
                : 0f;

            return _skillData.DefaultCooltime * (1f - reduction);
        }
    }

    public float CooltimeLeft => _cooltimeLeft;
    public float CooltimeRatio => Cooltime > 0f ? Mathf.Min(_cooltimeLeft / Cooltime, 1f) : 0f;
    public int UsePerDayLeft => _usePerDayLeft;
    public int UsePerDay => _skillData.DefaultUsePerDay;
    public bool IsUnlimitedUse => UsePerDay == UNLIMITED_USE_PER_DAY;
    public bool CanUse => IsUsePerDayLeft && _cooltimeLeft <= 0;
    public bool IsUsePerDayLeft => IsUnlimitedUse || UsePerDayLeft > 0;

    /// <summary>이 스킬이 발동 시 무엇을 지정해야 하는지 - 타겟팅 컨트롤러가 이 값으로 분기한다.</summary>
    public abstract SkillTargeting Targeting { get; }

    // 타겟팅 컨트롤러가 "커서 아래에서 어떤 레이어를 주울지" 판단하는 데도 필요하므로 public으로 노출한다.
    public LayerMask TargetLayers => _skillData.TargetLayers;

    // 강화·궁극 노드의 위력 보너스를 반영한다 - Meteor/GlobalDamage처럼 체력비례 데미지를 쓰는
    // 스킬만 실질적으로 영향을 받는다(빙결·타워수리는 값을 읽지 않음).
    protected float DamagePercent
    {
        get
        {
            float bonus = _dragonTreeManager != null
                ? _dragonTreeManager.GetSkillPowerMultiplierBonus(_skillData)
                : 0f;

            return _skillData.DamagePercentOfCurrentHealth * (1f + bonus);
        }
    }

    protected StatusEffectSO AppliedStatus => _skillData.AppliedStatus;

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
    }

    /// <summary>스킬 발동 진입점. 사용 가능할 때만 효과를 적용하고 쿨타임/사용횟수를 소비한다.</summary>
    public void Activate(in SkillCastContext context)
    {
        if (!CanUse)
            return;

        ApplyEffect(in context);
        SpendResources();
    }

    /// <summary>실제 스킬 효과 - 서브클래스가 구현한다.</summary>
    protected abstract void ApplyEffect(in SkillCastContext context);

    private void SpendResources()
    {
        _cooltimeLeft = Cooltime;
        if (!IsUnlimitedUse)
        {
            _usePerDayLeft -= 1;
        }
    }

    public void Reset()
    {
        _cooltimeLeft = 0f;
        _usePerDayLeft = _skillData.DefaultUsePerDay;
    }
}

public class DebugSkill : Skill
{
    public DebugSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override void ApplyEffect(in SkillCastContext context)
    {
        Debug.Log("Skill Actived");
    }
}

/// <summary>지정한 적 1기에게 현재 체력 비례 데미지를 준다.</summary>
public class SingleCurrentHealthDamageSkill : Skill
{
    public SingleCurrentHealthDamageSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Enemy;

    protected override void ApplyEffect(in SkillCastContext context)
    {
        BaseMonster target = context.TargetEnemy;

        if (target == null || target.IsDead)
            return;

        target.TakeDamage(new DamageInfo(target.CurrentHealth * DamagePercent));
    }
}

/// <summary>지정한 지점을 중심으로, 아이소메트릭 타일 비율에 맞춰 납작해진 타원 범위 안의
/// 모든 적에게 각자 현재 체력 비례 데미지를 준다.</summary>
public class AreaCurrentHealthDamageSkill : Skill
{
    public AreaCurrentHealthDamageSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.GroundPoint;

    protected override void ApplyEffect(in SkillCastContext context)
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
    }
}

/// <summary>용 스킬트리 얼음 액티브(모든 적 빙결) - 발동 시점에 스폰돼 있는 모든 몬스터에
/// SkillSO.AppliedStatus(보통 이동속도 0의 MoveSpeedStatusSO)를 부여한다.</summary>
public class FreezeAllSkill : Skill
{
    public FreezeAllSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override void ApplyEffect(in SkillCastContext context)
    {
        if (AppliedStatus == null || context.AllMonsters == null)
            return;

        foreach (BaseMonster monster in context.AllMonsters)
        {
            if (monster != null && !monster.IsDead)
            {
                monster.ApplyStatus(AppliedStatus);
            }
        }
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

    protected override void ApplyEffect(in SkillCastContext context)
    {
        if (context.AllMonsters == null)
            return;

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
    }
}

/// <summary>용 스킬트리 시간 액티브(파괴된 타워 즉시 수리) - 현재 비활성화(파괴) 상태인 모든
/// 타워를 재활성화 대기시간 없이 즉시 복구한다. Tower.RestoreAtMorning()과 동일한 복구 로직을
/// 재사용한다(아침 정산이 매일 모든 타워에 거는 것과 같은 처리) - 새 메서드를 만들지 않는다.</summary>
public class RepairTowersSkill : Skill
{
    public RepairTowersSkill(SkillSO skillData) : base(skillData) { }

    public override SkillTargeting Targeting => SkillTargeting.Instant;

    protected override void ApplyEffect(in SkillCastContext context)
    {
        if (context.AllBuildings == null)
            return;

        foreach (Building building in context.AllBuildings)
        {
            if (building is Tower tower && tower.IsDead)
            {
                tower.RestoreAtMorning();
            }
        }
    }
}
