using UnityEngine;

// 액티브 갈래·궁극 노드 공용 - 활성 속성의 액티브 스킬 위력/쿨다운/사용횟수를 강화한다.
// 위력 보너스는 체력비례 데미지·고정 데미지·회복량이 공통으로 읽는다(Skill.cs 참고) -
// 빙결·타워수리처럼 위력 개념이 없는 스킬은 쿨다운·사용횟수만 영향을 받는다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Skill Power",
    fileName = "DragonSkillPowerEffect")]
public sealed class DragonSkillPowerEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _powerBonusRatio;

    [Range(0f, 1f)]
    [SerializeField] private float _cooldownReductionRatio;

    [Tooltip("하루 사용 가능 횟수 추가분. 무제한(-1) 스킬에는 영향이 없다.")]
    [Min(0)]
    [SerializeField] private int _extraUsePerDay;

    public override float GetSkillPowerMultiplierBonus(DragonType? activeAttribute, SkillSO skill) =>
        _powerBonusRatio * Scale(activeAttribute);

    public override float GetSkillCooldownReductionRatio(DragonType? activeAttribute, SkillSO skill) =>
        _cooldownReductionRatio * Scale(activeAttribute);

    // 횟수는 정수라 배율을 곱할 수 없다 - 발동 여부만 판정해 통째로 주거나 주지 않는다.
    public override int GetSkillExtraUsePerDay(DragonType? activeAttribute, SkillSO skill) =>
        IsEffective(activeAttribute) ? _extraUsePerDay : 0;
}
