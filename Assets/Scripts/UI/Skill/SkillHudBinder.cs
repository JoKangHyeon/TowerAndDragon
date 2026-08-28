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

    [Tooltip("스킬 아이콘 호버 툴팁을 그릴 표시기. 비우면 이 HUD의 스킬 아이콘은 호버해도 아무것도 뜨지 않는다.")]
    [WiringOptional]
    [SerializeField] private UI_TooltipPresenter _tooltipPresenter;

    // SkillManager.Awake가 스킬 목록을 이미 구성한 뒤여야 하므로 Awake가 아닌 Start에서 첫 바인딩한다.
    private void Start()
    {
        // 인디케이터는 인스펙터에 고정 배치된 슬롯이라 매 Rebind마다 새로 생기지 않는다 - 표시기는
        // 한 번만 주입하면 된다.
        foreach (UI_SkillIndicator indicator in _indicators)
        {
            indicator?.Construct(_tooltipPresenter, _dragonTreeManager);
        }

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
            _cycleManager.OnDayReady.AddListener(HandleOnDayStart);
            _cycleManager.OnNightStart.AddListener(HandleOnNightStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleOnDayStart);
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
        // 아이콘만 사라지고 타겟팅이 살아남으면 InputSuppressed가 걸린 채로 낮이 시작돼
        // 건물 선택/배치가 통째로 막힌다. 아이콘을 감추는 시점에 타겟팅도 함께 푼다.
        if (_targetingController != null)
        {
            _targetingController.CancelTargeting();
        }

        foreach(UI_SkillIndicator indicator in _indicators)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    public void ShowAll()
    {
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
