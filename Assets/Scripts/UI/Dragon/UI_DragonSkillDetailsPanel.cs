using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬트리 창의 상세 패널 - 선택된 노드의 이름·설명·코스트·상태를 표시하고 해금 버튼을 연결한다.
// 코스트 칸은 UI_ConquestWindow와 동일하게 UI_ResourceCostSlot을 필요한 개수만 인스턴스화한다.
public class UI_DragonSkillDetailsPanel : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Transform _costContainer;
    [SerializeField] private UI_ResourceCostSlot _costSlotPrefab;
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private TextMeshProUGUI _upgradeButtonText;
    [SerializeField] private Color _sufficientColor = Color.white;
    [SerializeField] private Color _insufficientColor = Color.red;

    private readonly List<UI_ResourceCostSlot> _costSlots = new();

    private DragonTreeManager _dragonTreeManager;
    private ResourceManager _resourceManager;
    private DragonSkillNodeData _selectedNode;
    private Action _onChanged;

    public void Construct(DragonTreeManager dragonTreeManager, ResourceManager resourceManager, Action onChanged)
    {
        _dragonTreeManager = dragonTreeManager;
        _resourceManager = resourceManager;
        _onChanged = onChanged;

        if (_upgradeButtonText != null)
        {
            _upgradeButtonText.text = StringTable.GetString(DragonLocKeys.UPGRADE_BUTTON);
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.AddListener(HandleUpgradeClicked);
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

    public void Show(DragonSkillNodeData node)
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
        if (_selectedNode == null || _root == null || !_root.activeSelf)
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

        ProgressionNodeState state = _dragonTreeManager.GetNodeState(_selectedNode);

        if (_statusText != null)
        {
            _statusText.text = StringTable.GetString(DragonLocKeys.ResolveStateLocKey(state));
        }

        RebuildCostSlots();

        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = state == ProgressionNodeState.Available;
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

            bool hasEnough = _resourceManager != null && _resourceManager.GetAmount(cost.Type) >= cost.Amount;
            Color textColor = hasEnough ? _sufficientColor : _insufficientColor;

            slot.Setup(icon, DragonAttributePalette.TintFor(cost.Type), cost.Amount.ToString(), textColor);
            _costSlots.Add(slot);
        }
    }

    private void HandleUpgradeClicked()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_selectedNode == null || _dragonTreeManager == null)
        {
            return;
        }

        _dragonTreeManager.TryUnlock(_selectedNode, out ProgressionFailureReason _);
        Refresh();
        _onChanged?.Invoke();
    }
}
