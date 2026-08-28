using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 스킬트리 창의 상세 패널 - 선택된 노드의 이름·설명·코스트·상태를 표시하고 해금 버튼을 연결한다.
// 코스트 칸은 UI_ConquestWindow와 동일하게 UI_ResourceCostSlot을 필요한 개수만 인스턴스화한다.
public class UI_DragonSkillDetailsPanel : MonoBehaviour
{
    private const float OPEN_START_SCALE = 0.8f;
    private const float OPEN_OVERSHOOT_SCALE = 1.1f;
    private const float NORMAL_SCALE = 1f;
    private const float DEFAULT_OPEN_OVERSHOOT_DURATION = 0.18f;
    private const float DEFAULT_OPEN_SETTLE_DURATION = 0.1f;
    private const float DEFAULT_CLOSE_DURATION = 0.12f;

    private static readonly Color UPGRADE_DISABLED_COLOR_DEFAULT = new Color(0.5f, 0.5f, 0.5f, 1f);

    [SerializeField] private GameObject _root;

    [Tooltip("트리 노드에 얹힌 것과 같은 아이콘. 아이콘이 없는 슬롯에서는 꺼진다.")]
    [SerializeField] private Image _iconImage;

    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Tooltip("설명문 안의 [스킬명]을 호버 가능한 링크로 바꾼다. 비우면 대괄호가 원문 그대로 보인다.")]
    [WiringOptional]
    [SerializeField] private UI_SkillNameLinkTooltip _skillNameLinks;
    [SerializeField] private Transform _costContainer;
    [SerializeField] private UI_ResourceCostSlot _costSlotPrefab;
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private TextMeshProUGUI _upgradeButtonText;
    [SerializeField] private Color _sufficientColor = Color.white;
    [SerializeField] private Color _insufficientColor = Color.red;

    [Header("업그레이드 버튼 비활성 연출")]
    [Tooltip("업그레이드 불가(자원 부족 등)일 때 버튼 이미지·텍스트에 입힐 회색.")]
    [SerializeField] private Color _upgradeDisabledColor = UPGRADE_DISABLED_COLOR_DEFAULT;

    [Header("패널 열림/닫힘 스케일 연출")]
    [Tooltip("열릴 때 시작(80%)에서 오버슈트(110%)까지 걸리는 시간.")]
    [SerializeField] private float _openOvershootDuration = DEFAULT_OPEN_OVERSHOOT_DURATION;
    [Tooltip("오버슈트(110%)에서 정상(100%)으로 정착하는 시간.")]
    [SerializeField] private float _openSettleDuration = DEFAULT_OPEN_SETTLE_DURATION;
    [Tooltip("닫힐 때 100%에서 80%로 줄며 사라지는 시간.")]
    [SerializeField] private float _closeDuration = DEFAULT_CLOSE_DURATION;

    private readonly List<UI_ResourceCostSlot> _costSlots = new();

    private DragonTreeManager _dragonTreeManager;
    private ResourceManager _resourceManager;
    private DragonSkillNodeData _selectedNode;

    // 아이콘을 어느 슬롯에 무엇으로 붙일지는 UI_DragonSkillWindow가 단독으로 정한다(슬롯 표 →
    // 액티브 스킬 아이콘 → 알 아이콘). 같은 규칙을 이쪽에서 다시 구현하면 두 화면이 어긋나므로
    // Show가 받은 스프라이트를 그대로 들고 있다가 갱신 때마다 다시 칠한다.
    private Sprite _selectedIcon;

    private Action _onChanged;
    private readonly List<RaycastResult> _raycastResults = new();
    private Color _upgradeImageBaseColor = Color.white;
    private Color _upgradeTextBaseColor = Color.white;
    private Tween _scaleTween;
    private bool _isOpen;

    // 트리 창이 "지금 열려 있는 패널만 다른 노드로 다시 바인딩"할 때 쓴다 -
    // 닫힌 패널에 Show를 부르면 사용자가 닫은 패널이 갱신 때마다 되살아난다.
    public bool IsOpen => _isOpen;

    public void Construct(DragonTreeManager dragonTreeManager, ResourceManager resourceManager, Action onChanged)
    {
        _dragonTreeManager = dragonTreeManager;
        _resourceManager = resourceManager;
        _onChanged = onChanged;

        if (_upgradeButtonText != null)
        {
            _upgradeButtonText.text = StringTable.GetString(DragonLocKeys.UPGRADE_BUTTON);
            _upgradeTextBaseColor = _upgradeButtonText.color;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            if (_upgradeButton.image != null)
            {
                _upgradeImageBaseColor = _upgradeButton.image.color;
            }
        }

        HideImmediate();
    }

    public void Clear()
    {
        _selectedNode = null;
        _selectedIcon = null;
        _isOpen = false;

        if (!WiringGuard.Require(_root, nameof(_root), this))
        {
            return;
        }

        _scaleTween?.Kill();

        // 실제로 보이는 상태에서만 닫힘 연출(100%→80%)을 재생하고,
        // 그 외(부모 탭이 꺼지는 중 등)에는 즉시 숨긴다.
        if (_root.activeInHierarchy)
        {
            _scaleTween = _root.transform
                .DOScale(OPEN_START_SCALE, _closeDuration)
                .SetEase(Ease.InQuad)
                .SetLink(_root)
                .OnComplete(() => _root.SetActive(false));
        }
        else
        {
            _root.SetActive(false);
        }
    }

    // 초기화 등 연출 없이 즉시 숨겨야 할 때 사용.
    private void HideImmediate()
    {
        _selectedNode = null;
        _selectedIcon = null;
        _isOpen = false;
        _scaleTween?.Kill();

        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    /// <param name="icon">트리 노드에 얹힌 것과 같은 스프라이트. 없으면 null을 넘긴다.</param>
    public void Show(DragonSkillNodeData node, Sprite icon)
    {
        _selectedNode = node;
        _selectedIcon = icon;

        if (_root != null)
        {
            // 닫혀 있거나 닫히는 중이었을 때만 팝 연출을 재생한다 - 이미 열린 채로 다른 노드를
            // 클릭해 상세만 바꿀 때는 스케일을 다시 튀기지 않는다.
            bool wasOpen = _isOpen;
            _isOpen = true;
            _root.SetActive(true);

            if (!wasOpen)
            {
                PlayOpenScale();
            }
        }

        Refresh();
    }

    // 열림 연출: 80% → 110% → 100%.
    private void PlayOpenScale()
    {
        Transform panelTransform = _root.transform;

        _scaleTween?.Kill();
        panelTransform.localScale = Vector3.one * OPEN_START_SCALE;

        _scaleTween = DOTween.Sequence()
            .Append(panelTransform.DOScale(OPEN_OVERSHOOT_SCALE, _openOvershootDuration).SetEase(Ease.OutQuad))
            .Append(panelTransform.DOScale(NORMAL_SCALE, _openSettleDuration).SetEase(Ease.InOutQuad))
            .SetLink(_root);
    }

    // 상세 패널이 열려 있을 때, 스킬 노드도 패널 자신도 아닌 빈 공간을 클릭하면 닫는다.
    private void Update()
    {
        if (_root == null || !_root.activeSelf)
        {
            return;
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (IsPointerOverPanelOrNode())
        {
            return;
        }

        Clear();
    }

    // 포인터 아래에 패널(자기 자신·자식)이나 스킬 노드가 있으면 닫지 않는다
    // - 노드 클릭은 다른 스킬의 상세로 전환하기 위한 것이므로 유지해야 한다.
    private bool IsPointerOverPanelOrNode()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        _raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _raycastResults);

        foreach (RaycastResult result in _raycastResults)
        {
            Transform hit = result.gameObject.transform;

            if (hit.IsChildOf(_root.transform))
            {
                return true;
            }

            if (hit.GetComponentInParent<UI_DragonSkillNode>() != null)
            {
                return true;
            }
        }

        return false;
    }

    public void Refresh()
    {
        if (_selectedNode == null || _root == null || !_root.activeSelf)
        {
            return;
        }

        if (_iconImage != null)
        {
            // 아이콘이 없는 슬롯에서 빈 사각형이 남지 않게 끈다(트리 노드와 같은 처리).
            _iconImage.sprite = _selectedIcon;
            _iconImage.enabled = _selectedIcon != null;
        }

        if (_nameText != null)
        {
            _nameText.text = StringTable.GetString(_selectedNode.NameLocKey);
        }

        if (_descriptionText != null)
        {
            string description = StringTable.GetString(_selectedNode.DescriptionLocKey);
            _descriptionText.text = _skillNameLinks != null ? _skillNameLinks.Decorate(description) : description;
        }

        ProgressionNodeState state = _dragonTreeManager.GetNodeState(_selectedNode);

        if (_statusText != null)
        {
            _statusText.text = StringTable.GetString(DragonLocKeys.ResolveStateLocKey(state));
        }

        RebuildCostSlots();

        UpdateUpgradeButtonState(state);
    }

    // 업그레이드 가능(Available)일 때만 버튼을 켜고, 아니면(자원 부족 등) 이미지·텍스트를 회색으로 바꾼다.
    // 자원 부족은 GetNodeState가 InsufficientResources를 돌려주므로 Available이 아니게 되어 여기서 걸러진다.
    //
    // 안내가 막은 경우(TutorialLocked)만은 버튼을 살려 둔다 - 회색으로 죽여 두면 눌러도 아무 일이
    // 없어 고장으로 읽힌다. 눌렀을 때 사유를 말해 주는 쪽이 낫다(HandleUpgradeClicked).
    private void UpdateUpgradeButtonState(ProgressionNodeState state)
    {
        bool canUpgrade = state == ProgressionNodeState.Available ||
                          state == ProgressionNodeState.TutorialLocked;

        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = canUpgrade;

            if (_upgradeButton.image != null)
            {
                _upgradeButton.image.color = canUpgrade ? _upgradeImageBaseColor : _upgradeDisabledColor;
            }
        }

        if (_upgradeButtonText != null)
        {
            _upgradeButtonText.color = canUpgrade ? _upgradeTextBaseColor : _upgradeDisabledColor;
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

            slot.Setup(icon, Color.white, cost.Amount.ToString(), textColor);
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

        // 안내가 막은 것만 사유를 알린다 - 나머지 실패(자원 부족 등)는 상태 줄과 비용 색이 이미 말하고 있고,
        // 그 상태에서는 버튼이 회색이라 여기까지 오지도 않는다.
        if (!_dragonTreeManager.TryUnlock(_selectedNode, out ProgressionFailureReason reason) &&
            reason == ProgressionFailureReason.TutorialLocked)
        {
            _dragonTreeManager.NotifyProgressionBlocked();
        }

        Refresh();
        _onChanged?.Invoke();
    }
}
