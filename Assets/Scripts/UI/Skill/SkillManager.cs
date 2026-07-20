using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 인스턴스 보관·틱·일일 리셋을 담당한다. 발동 자체는 SkillTargetingController가 맡는다.</summary>
public class SkillManager : MonoBehaviour
{
    [SerializeField] private List<SkillSO> _skillDataList;

    private readonly List<Skill> _skills = new();
    private CycleManager _cycleManager;

    public IReadOnlyList<Skill> Skills => _skills;

    private void Awake()
    {
        BuildSkills();
    }

    // SkillSO 목록을 실제 Skill 인스턴스로 변환한다 - 팩토리가 null을 반환하는 항목(미구현 타입)은 건너뛴다.
    private void BuildSkills()
    {
        _skills.Clear();

        if (_skillDataList == null)
            return;

        foreach (SkillSO skillData in _skillDataList)
        {
            if (skillData == null)
                continue;

            Skill skill = skillData.GetSkill();

            if (skill != null)
            {
                _skills.Add(skill);
            }
        }
    }

    // CycleLight.Construct와 동일한 패턴 - CycleManager를 주입받아 새 날 시작마다 스킬을 리셋한다.
    public void Construct(CycleManager cycleManager)
    {
        _cycleManager = cycleManager;
        _cycleManager.OnDayStart.AddListener(HandleDayStart);
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }
    }

    private void HandleDayStart(int cycle)
    {
        foreach (Skill skill in _skills)
        {
            skill.Reset();
        }
    }

    private void Update()
    {
        foreach(Skill skill in _skills)
        {
            skill.Tick(Time.deltaTime);
        }
    }
}
