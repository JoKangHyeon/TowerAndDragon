using UnityEngine;

// 어미용 타워 스탯 강화(시간 각성 공속↑, 각 속성 "가동 강화", 생명 최대체력↑ 공용).
// _requireMatchingElement를 켜면 이 효과와 같은 속성의 타워에만 적용된다 -
// 끄면 종류를 가리지 않고 모든 타워에 걸린다(구 동작).
// 속성 판정은 IElementalAttackData로 한다 - ElementalTowerData와 BabyDragonData가 이미
// 구현하고 있어, 속성이 없는 일반 타워는 자연스럽게 대상에서 빠진다.
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

        return towerData is IElementalAttackData elemental && elemental.DragonType == Attribute;
    }
}
