using UnityEngine;

// 새끼용 지역형(시간, B 슬롯) - "점령 원정대에 포함 가능"을 소요일 감소로 근사한다.
// 새끼용이 실제로 원정에 동행하는 규칙은 기획에 없어(로드맵 §6-2) 이 근사치로 대체했다.
// 활성 속성과 무관하게 발동한다(로드맵 §1-3).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Kin Conquest",
    fileName = "KinConquestEffect")]
public sealed class KinConquestEffectSO : DragonSkillEffectSO
{
    [Min(0)]
    [SerializeField] private int _daysReduction;

    public override int GetConquestDaysReduction(DragonType? activeAttribute) => _daysReduction;
}
