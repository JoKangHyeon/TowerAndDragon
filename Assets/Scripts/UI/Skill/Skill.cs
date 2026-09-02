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

    /// <summary>
    /// 효과를 실제로 준 대상 위에 시전 연출(B계층)을 띄운다. 프리팹이 비어 있으면 아무 일도 하지 않는다.
    ///
    /// <b>ApplyEffect가 성공을 확정한 뒤에만 부른다.</b> 화면 전체 배경 오버레이(A계층)는
    /// SkillTargetingController가 Activate의 반환값을 보고 따로 띄우므로, 여기서 A를 신경 쓰지 않는다.
    ///
    /// 여러 대상에 거는 스킬(전역 화염 등)은 대상마다 부른다 - 풀에서 꺼내 쓰므로
    /// 인스턴스가 쌓이지 않는다.
    /// </summary>
    protected void PlayTargetVfx(Vector3 position)
    {
        if (_skillData.TargetVfxPrefab == null)
        {
            return;
        }

        ProjectilePool.PlayForSeconds(
            _skillData.TargetVfxPrefab,
            position,
            Quaternion.identity,
            _skillData.TargetVfxLifetimeSeconds);
    }

    /// <summary>
    /// 건물(성·타워)의 스프라이트 경계 중앙. <b>대상을 감싸는 크기</b>의 연출은 발밑이 아니라
    /// 여기에 띄운다 - 발밑에 두면 아래로 치우쳐 절반이 지면에 묻힌다.
    ///
    /// `transform.position`을 쓰지 않는 이유: 그것은 피벗이라 스프라이트의 시각적 중앙과 다르고,
    /// 건물마다 피벗 위치가 갈린다.
    ///
    /// (몬스터에는 쓰지 않는다 - 그쪽은 <see cref="PlayTargetVfxOnMonster"/>가 발밑을 쓴다.)
    /// </summary>
    protected static Vector3 ResolveBuildingCenter(Building building)
    {
        SpriteRenderer renderer = building.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = building.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (renderer == null)
        {
            return building.transform.position;
        }

        Bounds bounds = renderer.bounds;

        // z는 건물의 것을 그대로 쓴다 - 경계의 z는 스프라이트 두께라 정렬 기준이 되지 못한다
        // (MonsterStatusVfx.ResolveAnchors와 같은 이유).
        return new Vector3(bounds.center.x, bounds.center.y, building.transform.position.z);
    }

    /// <summary>
    /// 몬스터 발밑에 시전 연출을 띄운다. 앵커를 <see cref="AttackVfxPlacement.ResolveGroundY"/>로
    /// 구하는 것이 핵심이다 - <b>`SpriteRenderer.bounds`로 잡으면 안 된다.</b> 스프라이트 사각형의
    /// 밑변과 실제 발 피벗이 셀 높이의 18~25%까지 어긋나고, 보스에서 −0.8 월드 단위까지 벌어진
    /// 전례가 있다(설계 §6 / 그쪽 주석 참고).
    /// </summary>
    protected void PlayTargetVfxOnMonster(BaseMonster monster)
    {
        if (monster == null)
        {
            return;
        }

        Vector3 body = monster.transform.position;
        float groundY = AttackVfxPlacement.ResolveGroundY(monster.gameObject);

        PlayTargetVfx(new Vector3(body.x, groundY, body.z));
    }

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

    /// <summary>스킬 발동 진입점. 사용 가능할 때만 효과를 적용하고 쿨타임/사용횟수를 소비한다.
    /// 반환값은 <b>발동이 성립했는가</b>다 - 시전 연출을 띄울지 판단하는 데 쓴다(SkillCastOverlayHost).
    ///
    /// ⚠️ <b>"대상이 없으면 false"가 아니다.</b> 대상이 0마리·만피여도 발동으로 친다 -
    /// 언제 쓸지는 플레이어의 선택이고, 헛발질이면 쿨타임과 스택을 무는 것까지가 그 선택의 결과다
    /// (2026-09-02 결정. 마일스톤 "대상 0 판정 철회" 항목).
    ///
    /// false가 되는 것은 <see cref="CanUse"/> 실패와 각 스킬의 <b>진짜 실패 경로</b>뿐이다 -
    /// 방벽을 놓을 자리가 막혔거나, 회복할 성이 없거나, 비활성 타워가 0개인 경우
    /// (마지막 하나는 master의 원래 동작이라 남겨 둔 의도된 예외다).</summary>
    public bool Activate(in SkillCastContext context)
    {
        if (!CanUse)
            return false;

        if (!ApplyEffect(in context))
            return false;

        SpendResources();
        return true;
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

        // 얼린 마릿수를 세지 않는다 - 대상이 0마리여도 발동으로 친다. 스킬을 언제 쓸지는
        // 플레이어의 선택이고, 헛발질이면 쿨타임과 스택을 무는 것까지가 그 선택의 결과다.
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

            // 상태이상 파이프라인이 아니라 여기서 직접 띄운다 - "화상이 걸려 있다"가 아니라
            // "지금 막 타격했다"는 신호다. DS_FireBurn의 지속 연출과는 별개다(설계 §2-2).
            PlayTargetVfxOnMonster(monster);
        }

        // 빙결과 같은 이유로 타격 여부를 세지 않는다 - FreezeAllSkill.ApplyEffect 주석 참고.
        // 대상이 0마리면 대상별 연출만 안 나올 뿐, 발동 자체는 성립한다.
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
            if (building is Tower tower && tower.IsDead && tower.TryRestoreDuringCombat())
            {
                restoredAny = true;

                // ⚠️ Tower.RestoreAndReactivate에 걸지 않는다. 그 메서드는 세 경로가 공유한다 -
                // 부활 게이지 완주(NightRevive)·이 스킬(SkillRepair)·아침 일괄 복구(MorningRestore).
                // 거기 걸면 자연 부활과 아침 복구에도 연출이 뜬다(설계 §2-3). 스킬에서 직접 띄우면
                // 분기가 필요 없다.
                //
                // 앵커는 발밑이 아니라 스프라이트 중앙이다 - 성벽 재생과 같은 "대상을 감싸는"
                // 연출이라 발밑에 두면 아래로 치우쳐 절반이 지면에 묻힌다.
                PlayTargetVfx(ResolveBuildingCenter(tower));
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
        Castle castle = context.TargetCastle;

        if (castle == null)
            return false;

        // 만피·사망 가드를 두지 않는다. 그 구간에서 Health.Heal이 조용히 아무 일도 하지 않는 것은
        // 맞지만, 그때 발동을 막으면 플레이어가 누른 스킬이 아무 반응 없이 삼켜진다.
        // 언제 쓸지는 플레이어의 선택이므로 헛발질이어도 발동시키고 쿨타임·스택을 문다.
        castle.Repair(HealAmount);

        // ⚠️ Castle.Repair가 아니라 여기서 띄운다. Repair는 연구 convenience_castle_regen_1의
        // 매일 낮 자동 회복과 convenience_castle_repair의 즉시 수리도 지나는 공유 경로라,
        // 그쪽에 걸면 스킬을 쓰지 않은 아침에도 회복 연출이 뜬다.
        //
        // 앵커는 발밑이 아니라 <b>성 스프라이트 중앙</b>이다 - 성을 감싸는 크기의 연출이라
        // 발밑에 두면 아래쪽으로 치우쳐 절반이 지면에 묻힌다.
        PlayTargetVfx(ResolveBuildingCenter(castle));
        return true;
    }
}

/// <summary>용 스킬트리 암석 액티브 - 지정 위치에 광역 데미지를 주고 방벽을 설치합니다.</summary>
public class MeteorBarricadeSkill : Skill
{
    private const int BARRICADE_SNAP_DISTANCE = 2;

    private GameObject _previewInstance;

    // 타게팅 중 계속 떠 있는 바닥 마커. 방벽 고스트와 수명을 같이한다.
    private GameObject _previewMarkerInstance;

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

            UpdatePreviewMarker(worldPos);
        }
        else
        {
            if (_previewInstance != null)
            {
                _previewInstance.SetActive(false);
            }

            if (_previewMarkerInstance != null)
            {
                _previewMarkerInstance.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 설치될 자리를 가리키는 바닥 마커를 그 자리로 옮긴다. 인스턴스를 <b>하나만</b> 두고 매 프레임
    /// 위치만 갱신한다 - 방벽 고스트와 같은 방식이라 커서에 그대로 붙어 따라온다.
    ///
    /// ⚠️ <b>타일이 바뀔 때마다 새로 뿌리는 1회성 펄스로 되돌리지 말 것.</b> 그 형태였을 때는
    /// 같은 타일에 파티클이 겹쳐 쌓이는 것을 막으려 재생 간격 제한을 둬야 했고, 그 간격만큼 마커가
    /// 커서를 뒤늦게 따라와 "느리게 따라온다"가 됐다(2026-09-02에 되돌렸다).
    /// 인스턴스가 하나뿐이면 겹침이 구조적으로 불가능해 간격 제한 자체가 필요 없다.
    ///
    /// 풀(ProjectilePool)을 쓰지 않는다 - 풀은 "정해진 시간 뒤에 반납"하는 1회성 연출용이고,
    /// 이것은 타게팅이 끝날 때까지 사는 물건이라 고스트와 같은 Instantiate/Destroy가 맞다.
    ///
    /// 프리팹의 파티클 시뮬레이션 공간은 <b>Local</b>이어야 한다 - World로 두면 마커를 옮겨도
    /// 이미 나온 파티클이 제자리에 남아 꼬리처럼 끌린다.
    /// </summary>
    private void UpdatePreviewMarker(Vector3 worldPosition)
    {
        if (Data.BarricadePreviewMoveVfxPrefab == null)
        {
            return;
        }

        if (_previewMarkerInstance == null)
        {
            _previewMarkerInstance = Object.Instantiate(Data.BarricadePreviewMoveVfxPrefab);
        }

        _previewMarkerInstance.SetActive(true);
        _previewMarkerInstance.transform.position = worldPosition;
    }

    public override void ClearPreview()
    {
        if (_previewInstance != null)
        {
            Object.Destroy(_previewInstance);
            _previewInstance = null;
        }

        if (_previewMarkerInstance != null)
        {
            Object.Destroy(_previewMarkerInstance);
            _previewMarkerInstance = null;
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

            // ⚠️ 반경 1.5라 대상은 한 자리 수지만, 그렇다고 전역 화염보다 가벼운 것은 아니다.
            // 배선된 Impact_BD_Stone은 ParticleSystem이 20개(화염 임팩트의 2.2배)이고 수명도
            // 2.5초(2.5배)라, 9마리만 맞아도 180개가 2.5초 동안 살아 있다 - 화염 20마리와 같은 자릿수다.
            // "대상 수가 적으니 괜찮다"는 판단으로 여기를 다시 읽지 말 것.
            PlayTargetVfxOnMonster(monster);
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

            // ⚠️ 반드시 이 성공 분기 안에서만 띄운다. 설치 실패에 메테오가 떨어지면
            // 스킬이 소비됐다는 잘못된 신호가 된다(실패 경로는 쿨타임·스택을 차감하지 않는다).
            if (Data.BarricadePlacementVfxPrefab != null)
            {
                ProjectilePool.PlayForSeconds(
                    Data.BarricadePlacementVfxPrefab,
                    worldPos,
                    Quaternion.identity,
                    Data.BarricadePlacementVfxLifetimeSeconds);
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
