using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 HUD 패널 하나를 담당한다 - SkillManager가 구성한 스킬들을 인덱스 순서대로
/// 인스펙터에 배치된 UI_SkillIndicator들에 연결한다.</summary>
public class SkillHudBinder : MonoBehaviour
{
    [SerializeField] private SkillManager _skillManager;
    [SerializeField] private SkillTargetingController _targetingController;
    [SerializeField] private List<UI_SkillIndicator> _indicators;

    // SkillManager.Awake가 스킬 목록을 이미 구성한 뒤여야 하므로 Awake가 아닌 Start에서 바인딩한다.
    private void Start()
    {
        if (_skillManager == null || _targetingController == null)
            return;

        IReadOnlyList<Skill> skills = _skillManager.Skills;
        int count = Mathf.Min(skills.Count, _indicators.Count);

        for (int i = 0; i < count; i++)
        {
            if (_indicators[i] != null)
            {
                _indicators[i].Bind(skills[i], _targetingController.BeginTargeting);
            }
        }
    }
}
