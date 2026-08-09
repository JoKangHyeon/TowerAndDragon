using UnityEngine;

// 어미용 얼음 각성(타워 기본공격에 슬로우) - 활성 속성일 때 모든 타워 공격에 상태이상을 얹는다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Tower Hit Status",
    fileName = "DragonTowerHitStatusEffect")]
public sealed class DragonTowerHitStatusEffectSO : DragonSkillEffectSO
{
    [SerializeField] private StatusEffectSO _status;

    public override StatusEffectSO GetTowerHitStatus(DragonType? activeAttribute, TowerData towerData) =>
        IsEffective(activeAttribute) ? _status : null;
}
