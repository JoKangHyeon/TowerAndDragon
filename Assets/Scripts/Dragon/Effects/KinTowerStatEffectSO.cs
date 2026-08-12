using UnityEngine;

// 새끼용 타워형(A 슬롯) - 같은 속성 새끼용 "자신의" 전투 스탯만 올린다.
// KinTowerStatusEffectSO와 같은 대상 판정(BabyDragonData 한정)을 쓰며, 새끼용 효과는
// 활성 속성과 무관하게 항상 발동한다 - 그래서 Scale을 곱하지 않는다.
// 소비 지점은 ITowerStatMultiplierQuery(TowerAttack)라 별도 배선이 필요 없다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Tower Stat",
    fileName = "KinTowerStatEffect")]
public sealed class KinTowerStatEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _damageBonusRatio;

    [Min(0f)]
    [SerializeField] private float _attackSpeedBonusRatio;

    [Min(0f)]
    [SerializeField] private float _rangeBonusRatio;

    public override float GetTowerDamageMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        IsOwnKinTower(towerData) ? _damageBonusRatio : 0f;

    public override float GetTowerAttackSpeedMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        IsOwnKinTower(towerData) ? _attackSpeedBonusRatio : 0f;

    public override float GetTowerRangeMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        IsOwnKinTower(towerData) ? _rangeBonusRatio : 0f;

    private bool IsOwnKinTower(TowerData towerData) =>
        towerData is BabyDragonData babyData && babyData.DragonType == Attribute;
}
