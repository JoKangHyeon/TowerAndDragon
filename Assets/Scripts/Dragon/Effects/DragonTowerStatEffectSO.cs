using UnityEngine;

// 어미용 시간 속성 각성(공속↑)·강화/궁극(추가 강화) 공용 효과.
// 활성 속성일 때만 발동하며, 모든 타워에 동일하게 적용된다(타워 종류 무관).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Tower Stat",
    fileName = "DragonTowerStatEffect")]
public sealed class DragonTowerStatEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _attackSpeedBonusRatio;

    [Min(0f)]
    [SerializeField] private float _damageBonusRatio;

    public override float GetTowerAttackSpeedMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        IsActive(activeAttribute) ? _attackSpeedBonusRatio : 0f;

    public override float GetTowerDamageMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        IsActive(activeAttribute) ? _damageBonusRatio : 0f;
}
