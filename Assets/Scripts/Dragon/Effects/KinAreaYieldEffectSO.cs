using UnityEngine;

// 새끼용 지역형(B 슬롯, 암석/생명) - BabyDragonBuffSystem이 계산하는 반경 생산배율에
// 곱해질 추가 보너스. 활성 속성과 무관하게 발동한다(로드맵 §1-3).
// dragonType은 대상 새끼용의 속성 - 자신의 Attribute와 일치할 때만 값을 반환한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Area Yield",
    fileName = "KinAreaYieldEffect")]
public sealed class KinAreaYieldEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public override float GetKinAreaYieldBonusRatio(DragonType dragonType) =>
        dragonType == Attribute ? _bonusRatio : 0f;
}
