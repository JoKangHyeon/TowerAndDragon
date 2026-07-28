using UnityEngine;

// 강화·궁극 노드 공용 - 활성 속성의 액티브 스킬 위력/쿨다운을 강화한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Skill Power",
    fileName = "DragonSkillPowerEffect")]
public sealed class DragonSkillPowerEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _powerBonusRatio;

    [Range(0f, 1f)]
    [SerializeField] private float _cooldownReductionRatio;

    public override float GetSkillPowerMultiplierBonus(DragonType? activeAttribute, SkillSO skill) =>
        IsActive(activeAttribute) ? _powerBonusRatio : 0f;

    public override float GetSkillCooldownReductionRatio(DragonType? activeAttribute, SkillSO skill) =>
        IsActive(activeAttribute) ? _cooldownReductionRatio : 0f;
}
