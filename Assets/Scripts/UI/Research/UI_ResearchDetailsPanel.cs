using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 창의 상세 패널 - 선택된 노드의 이름·설명·상태·RP/자원 코스트를 표시하고 연구 버튼을 연결한다.
// 코스트 칸은 UI_DragonSkillDetailsPanel과 동일하게 UI_ResourceCostSlot을 필요한 개수만 인스턴스화한다.
public class UI_ResearchDetailsPanel : MonoBehaviour
{
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
    private Action _onChanged;

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
            _statusText.text = StringTable.GetString(ResearchLocKeys.ResolveStateLocKey(state));
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

    private void RebuildCostSlots()
    {
        if (_costContainer == null || _costSlotPrefab == null)
        {
            return;
        }

        foreach (UI_ResourceCostSlot slot in _costSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _costSlots.Clear();

        foreach (ResourceAmount cost in _selectedNode.ResourceCost)
        {
            UI_ResourceCostSlot slot = Instantiate(_costSlotPrefab, _costContainer);

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
            _costSlots.Add(slot);
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
