using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 창의 상세 패널 - 선택된 노드의 이름·설명·상태·RP/자원 코스트를 표시하고 연구 버튼을 연결한다.
// 코스트 칸은 UI_DragonSkillDetailsPanel과 동일하게 UI_ResourceCostSlot을 필요한 개수만 인스턴스화한다.
public class UI_ResearchDetailsPanel : MonoBehaviour
{
    private const string PREREQUISITE_SEPARATOR = ", ";

    [SerializeField] private GameObject _root;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _researchPointCostText;
    [SerializeField] private Transform _costContainer;
    [SerializeField] private UI_ResourceCostSlot _costSlotPrefab;
    [SerializeField] private Button _researchButton;
    [SerializeField] private TextMeshProUGUI _researchButtonText;
    [SerializeField] private Color _sufficientColor = Color.white;
    [SerializeField] private Color _insufficientColor = Color.red;

    private readonly List<UI_ResourceCostSlot> _costSlots = new();

    private ResearchManager _researchManager;
    private ResourceManager _resourceManager;
    private ResearchNodeData _selectedNode;

    /// <summary>
    /// 연구 창의 전체 갱신(UI_ResearchWindow.RefreshAll)을 되부르는 콜백.
    ///
    /// <b>이 콜백은 <see cref="HandleResearchClicked"/>에서만 호출한다.</b>
    /// RefreshAll이 이 패널의 <see cref="Refresh"/>를 부르므로, Refresh에서 이 콜백을 부르면
    /// <c>RefreshAll → Refresh → _onChanged → RefreshAll</c>로 무한 재귀가 된다.
    /// 연구 시스템에서 유일하게 순환이 될 수 있는 지점이라 <see cref="_isRefreshing"/>으로
    /// 실수까지 막아 둔다.
    /// </summary>
    private Action _onChanged;

    private bool _isRefreshing;

    /// <summary>
    /// 지금 화면에 떠 있는지. 연구 창이 Esc를 창 닫기에 쓸지 이 패널 닫기에 쓸지 정하는 데 본다
    /// (<see cref="UI_ResearchWindow.OnCloseActionPerformed"/>).
    /// </summary>
    public bool IsShown => _root != null && _root.activeSelf;

    public void Construct(
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Action onChanged)
    {
        _researchManager = researchManager;
        _resourceManager = resourceManager;
        _onChanged = onChanged;

        if (_researchButtonText != null)
        {
            _researchButtonText.text = StringTable.GetString(ResearchLocKeys.RESEARCH_BUTTON);
        }

        if (_researchButton != null)
        {
            _researchButton.onClick.AddListener(HandleResearchClicked);
        }

        Clear();
    }

    public void Clear()
    {
        _selectedNode = null;

        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    public void Show(ResearchNodeData node)
    {
        _selectedNode = node;

        if (_root != null)
        {
            _root.SetActive(true);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_selectedNode == null || _root == null || !_root.activeSelf || _researchManager == null)
        {
            return;
        }

        // 재진입만 막는다 - 갱신이 끝난 뒤 다시 부르는 것은 재진입이 아니라 그대로 동작한다.
        // (막는 대상은 _onChanged 설명에 적은 RefreshAll -> Refresh -> _onChanged 순환이다)
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            RefreshContents();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RefreshContents()
    {
        if (_nameText != null)
        {
            _nameText.text = StringTable.GetString(_selectedNode.NameLocKey);
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = StringTable.GetString(_selectedNode.DescriptionLocKey);
        }

        ResearchNodeState state = _researchManager.GetNodeState(_selectedNode);

        if (_statusText != null)
        {
            _statusText.text = BuildStatusText(state);
        }

        if (_researchPointCostText != null)
        {
            _researchPointCostText.text = string.Format(
                StringTable.GetString(ResearchLocKeys.RP_COST),
                _selectedNode.ResearchPointCost);

            bool hasEnoughPoints =
                _researchManager.ResearchPoints >= _selectedNode.ResearchPointCost;
            _researchPointCostText.color = hasEnoughPoints ? _sufficientColor : _insufficientColor;
        }

        RebuildCostSlots();

        if (_researchButton != null)
        {
            _researchButton.interactable = state == ResearchNodeState.Available;
        }
    }

    private string BuildStatusText(ResearchNodeState state)
    {
        if (state != ResearchNodeState.PrerequisiteLocked)
        {
            return StringTable.GetString(ResearchLocKeys.ResolveStateLocKey(state));
        }

        string prerequisiteNames = BuildMissingPrerequisiteNames();
        if (string.IsNullOrEmpty(prerequisiteNames))
        {
            return StringTable.GetString(ResearchLocKeys.STATE_PREREQUISITE_LOCKED);
        }

        return string.Format(
            StringTable.GetString(ResearchLocKeys.STATE_PREREQUISITE_LOCKED_WITH_NAMES),
            prerequisiteNames);
    }

    private string BuildMissingPrerequisiteNames()
    {
        if (_selectedNode == null)
        {
            return string.Empty;
        }

        List<string> names = new();
        foreach (ResearchNodeData prerequisite in _selectedNode.Prerequisites)
        {
            if (prerequisite == null)
            {
                continue;
            }

            if (_researchManager != null && _researchManager.IsCompleted(prerequisite.NodeId))
            {
                continue;
            }

            names.Add(StringTable.GetString(prerequisite.NameLocKey));
        }

        return string.Join(PREREQUISITE_SEPARATOR, names);
    }

    /// <summary>
    /// 코스트 슬롯을 <b>파괴·재생성하지 않고 재사용한다.</b>
    ///
    /// 예전에는 Refresh마다 전부 Destroy하고 다시 Instantiate했다. Destroy는 프레임 끝으로
    /// 지연되는데 Instantiate는 즉시라, 한 프레임 동안 구 슬롯과 신 슬롯이 컨테이너에 같이 남아
    /// 코스트 아이콘이 여러 벌 겹쳐 보였다 - 연구 한 번에 Refresh가 대여섯 번
    /// (자원별 ResourceChanged + RP 변경 + NodeCompleted + 직접 호출) 돌기 때문에 눈에 띄었다.
    ///
    /// 아이콘은 항상 있다고 본다(자원 카탈로그의 RD_* 전부에 아이콘이 배선돼 있다).
    /// <see cref="UI_ResourceCostSlot.Setup"/>은 icon이 null이면 기존 스프라이트를 그대로 두므로,
    /// 아이콘 없는 자원이 생기면 재사용된 슬롯에 앞 자원의 아이콘이 남는다.
    /// </summary>
    private void RebuildCostSlots()
    {
        if (_costContainer == null || _costSlotPrefab == null)
        {
            return;
        }

        // 바깥에서 파괴된 슬롯이 섞이면 그 자리 코스트가 통째로 안 그려진다 - 먼저 걸러낸다.
        _costSlots.RemoveAll(slot => slot == null);

        IReadOnlyList<ResourceAmount> costs = _selectedNode.ResourceCost;

        while (_costSlots.Count < costs.Count)
        {
            _costSlots.Add(Instantiate(_costSlotPrefab, _costContainer));
        }

        for (int i = 0; i < _costSlots.Count; i++)
        {
            UI_ResourceCostSlot slot = _costSlots[i];
            bool isUsed = i < costs.Count;
            slot.gameObject.SetActive(isUsed);

            if (!isUsed)
            {
                continue;
            }

            ResourceAmount cost = costs[i];

            Sprite icon = null;
            if (_resourceManager != null && _resourceManager.Catalog != null &&
                _resourceManager.Catalog.TryGet(cost.Type, out ResourceData resourceData))
            {
                icon = resourceData.Icon;
            }

            bool hasEnough = _resourceManager != null &&
                _resourceManager.GetAmount(cost.Type) >= cost.Amount;
            Color textColor = hasEnough ? _sufficientColor : _insufficientColor;

            slot.Setup(icon, Color.white, cost.Amount.ToString(), textColor);
        }
    }

    private void HandleResearchClicked()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_selectedNode == null || _researchManager == null)
        {
            return;
        }

        _researchManager.TryResearch(_selectedNode, out ResearchFailureReason _);
        Refresh();
        _onChanged?.Invoke();
    }
}
