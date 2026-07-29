using UnityEngine;

[CreateAssetMenu(fileName = "New Skill SO", menuName = "Skill/SkillSO")]
public class SkillSO:ScriptableObject
{
    public SkillType Type;
    public string NameStringKey;
    public string DescriptionStringKey;
    public Sprite Sprite;

    public float DefaultCooltime;
    public int DefaultUsePerDay;

    [Header("체력비례 스킬 수치 - 현재 체력 기준 데미지")]
    [Range(0f, 1f)]
    public float DamagePercentOfCurrentHealth;
    [Tooltip("광역 스킬의 적용 반경.")]
    public float AreaRadius;
    [Tooltip("타겟팅 시 인식할 레이어 - 보통 Enemy 레이어.")]
    public LayerMask TargetLayers;

    [Header("용 스킬트리 액티브 - 상태이상 부여형")]
    [Tooltip("발동 시 대상에 부여할 상태이상(빙결·화상 재부여 등). 필요 없는 스킬은 비워 둔다.")]
    public StatusEffectSO AppliedStatus;

    [Header("용 스킬트리 액티브 - 회복형 수치")]
    [Tooltip("발동 시 성에 즉시 회복시킬 체력량. 회복형이 아닌 스킬은 사용하지 않는다.")]
    public float HealAmount;

    public Skill GetSkill()
    {
        switch (Type)
        {
            case SkillType.DEBUG:
                return new DebugSkill(this);
            case SkillType.SINGLE_CURRENT_HEALTH_DAMAGE:
                return new SingleCurrentHealthDamageSkill(this);
            case SkillType.AREA_CURRENT_HEALTH_DAMAGE:
                return new AreaCurrentHealthDamageSkill(this);
            case SkillType.FREEZE_ALL:
                return new FreezeAllSkill(this);
            case SkillType.GLOBAL_CURRENT_HEALTH_DAMAGE:
                return new GlobalCurrentHealthDamageSkill(this);
            case SkillType.REPAIR_TOWERS:
                return new RepairTowersSkill(this);
            case SkillType.HEAL_CASTLE:
                return new HealCastleSkill(this);
            default:
                return null;
        }
    }
}
