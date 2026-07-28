using UnityEngine;

// 새끼용 타워형(A 슬롯) - 해당 속성 새끼용 타워의 기본공격에만 상태이상을 얹는다.
// 활성 속성과 무관하게 항상 발동하되(로드맵 §1-3), 대상은 같은 속성의 BabyDragonTower로 한정한다 -
// 그렇지 않으면 새끼용 하나 해금으로 모든 플레이어 타워가 슬로우를 얻게 된다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Tower Status",
    fileName = "KinTowerStatusEffect")]
public sealed class KinTowerStatusEffectSO : DragonSkillEffectSO
{
    [SerializeField] private StatusEffectSO _status;

    public override StatusEffectSO GetTowerHitStatus(DragonType? activeAttribute, TowerData towerData)
    {
        bool isOwnBabyDragonTower = towerData is BabyDragonData babyData && babyData.DragonType == Attribute;
        return isOwnBabyDragonTower ? _status : null;
    }
}
