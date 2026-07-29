using UnityEngine;

// 액티브 스킬 해금 효과. DragonTreeManager.UnlockedActiveSkills가 GetUnlockedSkill()을 통해
// 이 값을 pull한다 - 해금은 활성 속성과 무관하게 영구 유지된다(로드맵 §1-3).
// 실제 사용 가능 여부(활성 속성 일치) 필터링은 SkillManager.AvailableSkills가 담당한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Active Skill Unlock",
    fileName = "ActiveSkillUnlockEffect")]
public sealed class ActiveSkillUnlockEffectSO : DragonSkillEffectSO
{
    [SerializeField] private SkillSO _skill;

    public SkillSO Skill => _skill;

    public override SkillSO GetUnlockedSkill() => _skill;
}
