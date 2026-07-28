using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 인스턴스 보관·틱·일일 리셋을 담당한다. 발동 자체는 SkillTargetingController가 맡는다.</summary>
public class SkillManager : MonoBehaviour
{
    [SerializeField] private List<SkillSO> _skillDataList;
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    private readonly List<Skill> _skills = new();
    private readonly HashSet<SkillSO> _availableBuffer = new();
    private CycleManager _cycleManager;

    // 등록된 스킬 전체(디버그 스킬 등 용 스킬트리와 무관한 것 포함) - 쿨다운·리셋 관리용.
    public IReadOnlyList<Skill> Skills => _skills;

    // 해금 + 활성 속성이 일치하는 스킬만 노출한다 - HUD/타게팅은 이 목록만 사용해야 한다.
    // 용 스킬트리와 무관한 스킬(_dragonTreeManager가 AllTreeSkills에 담지 않은 것)은
    // 항상 사용 가능한 것으로 취급한다(기존 디버그 스킬 동작 유지).
    public IEnumerable<Skill> AvailableSkills
    {
        get
        {
            _availableBuffer.Clear();

            // 해금 여부와 무관하게 "트리 소속 스킬 전체"로 판별해야 한다 - UnlockedActiveSkills를
            // 쓰면 미해금 상태에서 대상이 비어 "용 스킬트리 무관"으로 오판되고, 해금 전인데도
            // 항상 사용 가능한 것으로 새어나간다.
            if (_dragonTreeManager != null)
            {
                foreach (SkillSO skill in _dragonTreeManager.AllTreeSkills)
                {
                    _availableBuffer.Add(skill);
                }
            }

            foreach (Skill skill in _skills)
            {
                bool isDragonManaged = _availableBuffer.Contains(skill.Data);
                bool isAvailableNow = !isDragonManaged || IsCurrentlyAvailable(skill.Data);

                if (isAvailableNow)
                {
                    yield return skill;
                }
            }
        }
    }

    private bool IsCurrentlyAvailable(SkillSO skillData)
    {
        if (_dragonTreeManager == null)
        {
            return true;
        }

        foreach (SkillSO available in _dragonTreeManager.AvailableActiveSkills)
        {
            if (available == skillData)
            {
                return true;
            }
        }

        return false;
    }

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
                // 강화/궁극 노드의 위력·쿨다운 보너스를 읽으려면 인스턴스마다 조회원이 필요하다.
                skill.SetDragonTreeManager(_dragonTreeManager);
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
