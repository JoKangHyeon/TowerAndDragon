using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 HUD 패널 하나를 담당한다 - SkillManager.AvailableSkills(해금 + 활성 속성 일치)를
/// 인스펙터에 배치된 UI_SkillIndicator들에 연결한다.
///
/// 인덱스 1회 바인딩 대신 매번 AvailableSkills를 다시 읽어 재바인딩하는 이유: 용 스킬트리
/// 해금·속성 변경으로 노출 목록이 게임 중에 바뀌는데, Start() 1회 바인딩만 하면 (a) 목록이
/// 밀려 슬롯과 스킬이 어긋나고 (b) 속성이 바뀌어도 HUD가 갱신되지 않는다.</summary>
public class SkillHudBinder : MonoBehaviour
{
    [SerializeField] private SkillManager _skillManager;
    [SerializeField] private SkillTargetingController _targetingController;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private List<UI_SkillIndicator> _indicators;

    // SkillManager.Awake가 스킬 목록을 이미 구성한 뒤여야 하므로 Awake가 아닌 Start에서 첫 바인딩한다.
    private void Start()
    {
        Rebind();
    }

    private void OnEnable()
    {
        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleActiveAttributeChanged);
            _dragonTreeManager.NodeUnlocked.AddListener(HandleNodeUnlocked);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleOnDayStart);
            _cycleManager.OnNightStart.AddListener(HandleOnNightStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleOnDayStart);
            _cycleManager.OnNightStart.RemoveListener(HandleOnNightStart);
        }

        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleActiveAttributeChanged);
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleNodeUnlocked);
        }
    }

    private void HandleActiveAttributeChanged(DragonType attribute) => Rebind();

    private void HandleNodeUnlocked(ProgressionNodeData node) => Rebind();

    private void HandleOnDayStart(int day) => HideAll();
    private void HandleOnNightStart(int day) => ShowAll();

    public void HideAll()
    {
        Debug.Log("HIDE");
        foreach(UI_SkillIndicator indicator in _indicators)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    public void ShowAll()
    {
        Debug.Log("SHOW");
        foreach (UI_SkillIndicator indicator in _indicators)
        {
            indicator.ShowIfBinded();
        }
    }

    private void Rebind()
    {
        if (_skillManager == null || _targetingController == null || _indicators == null)
        {
            return;
        }

        int slotIndex = 0;

        foreach (Skill skill in _skillManager.AvailableSkills)
        {
            if (slotIndex >= _indicators.Count)
            {
                break;
            }

            if (_indicators[slotIndex] != null)
            {
                _indicators[slotIndex].Bind(skill, _targetingController.BeginTargeting);
            }

            slotIndex++;
        }

        // 남은 슬롯은 비워 이전 바인딩이 남아있지 않게 한다.
        for (; slotIndex < _indicators.Count; slotIndex++)
        {
            _indicators[slotIndex]?.Bind(null, null);
        }

        if (_cycleManager != null)
        {
            if(_cycleManager.CurrentCycle  == CycleManager.CycleState.Day)
            {
                HideAll();
            }
            else
            {
                ShowAll();
            }
        }
    }
}
