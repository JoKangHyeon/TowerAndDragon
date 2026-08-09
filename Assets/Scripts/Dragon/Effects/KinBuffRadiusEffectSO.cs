using UnityEngine;

// 새끼용 버프모드의 효과 반경 증가(생명·시간 kin).
// KinAreaYieldEffectSO와 같은 계약 - dragonType은 대상 새끼용의 속성이며, 자신의 Attribute와
// 일치할 때만 값을 반환한다(활성 속성 무관).
// 소비 지점은 BabyDragonBuffSystem.GetEffectiveBuffRadius 한 곳이다 - 생산 버프뿐 아니라
// 건설 해제·지형 페널티 완화 반경까지 같은 값으로 커져야 하므로 반드시 그 헬퍼를 통해 쓴다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Buff Radius",
    fileName = "KinBuffRadiusEffect")]
public sealed class KinBuffRadiusEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public override float GetKinBuffRadiusBonusRatio(DragonType dragonType) =>
        dragonType == Attribute ? _bonusRatio : 0f;
}
