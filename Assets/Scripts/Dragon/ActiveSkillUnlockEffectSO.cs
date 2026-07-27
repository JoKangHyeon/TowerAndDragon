using UnityEngine;

// 액티브 스킬 해금 효과. SkillSO 참조만 보유하고, 실제 사용 가능 여부 필터링은
// DragonTreeManager가 pull API(UnlockedActiveSkills)로 노출한다.
// SkillManager 자체는 이번 마일스톤에서 수정하지 않는다(비목표).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Active Skill Unlock",
    fileName = "ActiveSkillUnlockEffect")]
public sealed class ActiveSkillUnlockEffectSO : DragonSkillEffectSO
{
    [SerializeField] private SkillSO _skill;

    public SkillSO Skill => _skill;
}
