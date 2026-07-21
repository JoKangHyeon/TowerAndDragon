using System.Collections.Generic;
using UnityEngine;

public enum SkillType
{
    DEBUG,
    SINGLE_CURRENT_HEALTH_DAMAGE,
    AREA_CURRENT_HEALTH_DAMAGE,
}

/// <summary>스킬 발동 시 무엇을 지정해야 하는지 - UI/입력 쪽에서 이 값에 따라 지정 방식을 분기한다.</summary>
public enum SkillTargeting
{
    Instant,     // 지정 없이 즉시 발동
    GroundPoint, // 월드 지점(광역 등) 지정
    Enemy,       // 적 1기 지정
}

/// <summary>스킬 발동에 필요한 대상 정보를 한 번에 묶어 전달한다. 필요 없는 필드는 비워 둔다(예: Instant는 전부 비움).</summary>
public readonly struct SkillCastContext
{
    public Vector3 TargetPoint { get; }
    public BaseMonster TargetEnemy { get; }
    public GameObject Caster { get; }

    public SkillCastContext(Vector3 targetPoint, BaseMonster targetEnemy, GameObject caster)
    {
        TargetPoint = targetPoint;
        TargetEnemy = targetEnemy;
        Caster = caster;
    }
}



public abstract class Skill
{
    private const int UNLIMITED_USE_PER_DAY = -1;

    // 아이소메트릭 타일 종횡비(Grid_HeightVariant의 Grid.cellSize = 1.0 x, 0.5 y)에 맞춰
    // 원형 판정/미리보기를 타원으로 납작하게 만드는 비율 - 판정(AreaCurrentHealthDamageSkill)과
    // 미리보기(SkillRangeIndicator)가 항상 같은 값을 쓰도록 여기 한 곳에만 둔다.
    public const float ISOMETRIC_RADIUS_Y_RATIO = 0.5f;

    private readonly SkillSO _skillData;

    private float _cooltimeLeft;
    private int _usePerDayLeft;

    protected Skill(SkillSO skillData)
    {
        _skillData = skillData;
        Reset();
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

    public float Cooltime => _skillData.DefaultCooltime;
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

    protected float DamagePercent => _skillData.DamagePercentOfCurrentHealth;

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
        float radiusY = AreaRadius * ISOMETRIC_RADIUS_Y_RATIO;

        // 브로드페이즈: 더 큰 쪽인 X 반지름의 원으로 넉넉히 후보를 모은 뒤 타원 방정식으로 정확히 걸러낸다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(context.TargetPoint, radiusX, TargetLayers);
        HashSet<BaseMonster> targets = new HashSet<BaseMonster>();

        foreach (Collider2D hit in hits)
        {
            BaseMonster monster = hit.GetComponentInParent<BaseMonster>();

            if (monster == null)
                continue;

            if (!IsWithinEllipse(monster.transform.position, context.TargetPoint, radiusX, radiusY))
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

    private static bool IsWithinEllipse(Vector3 point, Vector3 center, float radiusX, float radiusY)
    {
        float dx = point.x - center.x;
        float dy = point.y - center.y;
        float normalizedDistanceSqr = (dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY);

        return normalizedDistanceSqr <= 1f;
    }
}
