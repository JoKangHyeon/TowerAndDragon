using UnityEngine;

// 새끼용 지역형(불, B 슬롯) - 시야 반경 증가. 새끼용 효과는 활성 속성과 무관하게 발동한다
// (노드 해금 + 해당 속성 새끼용 보유가 조건의 전부 - 로드맵 §1-3).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Vision",
    fileName = "KinVisionEffect")]
public sealed class DragonVisionEffectSO : DragonSkillEffectSO
{
    [Min(0)]
    [SerializeField] private int _bonusRadius;

    public override int GetVisionRadiusBonus(DragonType? activeAttribute) => _bonusRadius;
}
