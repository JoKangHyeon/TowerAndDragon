using UnityEngine;

// 어미용 타워 스탯 강화(시간 각성 공속↑, 각 속성 "가동 강화", 생명 최대체력↑ 공용).
// _requireMatchingElement를 켜면 이 효과와 같은 속성의 타워에만 적용된다 -
// 끄면 종류를 가리지 않고 모든 타워에 걸린다(구 동작).
//
// 속성 판정을 IElementalAttackData가 아니라 ElementalTowerData로 하는 이유:
// 새끼용(BabyDragonData)도 그 인터페이스를 구현하므로 인터페이스로 재면 어미용 "가동 강화"가
// 새끼용까지 같이 올려 준다. 새끼용 강화는 이 트리의 새끼용 갈래(KinTowerStatEffectSO)가
// 담당하므로, 같은 대상에 두 갈래가 이중으로 얹히지 않게 여기서 갈라 둔다.
// 연구 쪽 TowerTargetFilter.Matches가 같은 이유로 같은 판정을 쓴다.
// 속성이 없는 일반 타워는 어느 쪽으로 재도 대상에서 빠진다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Tower Stat",
    fileName = "DragonTowerStatEffect")]
public sealed class DragonTowerStatEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _attackSpeedBonusRatio;

    [Min(0f)]
    [SerializeField] private float _damageBonusRatio;

    // 최대체력은 다른 스탯과 달리 매 프레임 pull되지 않는다(Health가 최대치를 값으로 들고 있다) -
    // TowerMaxHealthApplier가 밤 시작 시점에 한 번 읽어 적용한다.
    [Min(0f)]
    [SerializeField] private float _maxHealthBonusRatio;

    [Tooltip("켜면 이 효과와 같은 속성의 타워에만 적용한다. 끄면 모든 타워(구 동작).")]
    [SerializeField] private bool _requireMatchingElement;

    public override float GetTowerAttackSpeedMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        Matches(towerData) ? _attackSpeedBonusRatio * Scale(activeAttribute) : 0f;

    public override float GetTowerDamageMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        Matches(towerData) ? _damageBonusRatio * Scale(activeAttribute) : 0f;

    public override float GetTowerMaxHealthMultiplierBonus(DragonType? activeAttribute, TowerData towerData) =>
        Matches(towerData) ? _maxHealthBonusRatio * Scale(activeAttribute) : 0f;

    private bool Matches(TowerData towerData)
    {
        if (!_requireMatchingElement)
        {
            return true;
        }

        return towerData is ElementalTowerData elemental && elemental.DragonType == Attribute;
    }
}
