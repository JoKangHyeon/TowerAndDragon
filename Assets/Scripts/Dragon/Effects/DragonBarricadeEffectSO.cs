using UnityEngine;

// 암석 액티브(메테오+방벽)가 설치하는 방벽의 최대체력을 올린다.
// 위력/쿨다운과 달리 SkillSO 수치가 아니라 설치되는 건물의 체력이라
// DragonSkillPowerEffectSO가 아닌 별도 효과로 둔다 - 소비 지점은 MeteorBarricadeSkill이다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Barricade Health",
    fileName = "DragonBarricadeEffect")]
public sealed class DragonBarricadeEffectSO : DragonSkillEffectSO
{
    [Min(0f)]
    [SerializeField] private float _healthBonusRatio;

    public override float GetBarricadeHealthBonusRatio(DragonType? activeAttribute) =>
        _healthBonusRatio * Scale(activeAttribute);
}
