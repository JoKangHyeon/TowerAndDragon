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

    [Header("데미지 스킬 전용 - 현재 체력 비례 데미지")]
    [Range(0f, 1f)]
    public float DamagePercentOfCurrentHealth;
    [Tooltip("광역 스킬의 판정 반경.")]
    public float AreaRadius;
    [Tooltip("타겟으로 인식할 레이어 - 보통 Enemy 레이어.")]
    public LayerMask TargetLayers;


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
            default:
                return null;
        }
    }
}
