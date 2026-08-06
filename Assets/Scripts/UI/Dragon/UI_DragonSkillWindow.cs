using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 용 스킬트리 창 - DragonSkillTree.asset을 순회해 30노드+30엣지를 방사형으로 런타임 배치하고,
// 클릭 시 상세 패널에 바인딩한다. blocker 루트(전체화면 dim Image)에 부착되어
// 바깥 클릭 닫기와 IExclusiveMode(UIManager.OpenExclusive) 조정을 겸한다.
// 해금/속성 변경 규칙은 여기서 재구현하지 않는다 - 전부 DragonTreeManager에 위임.
public class UI_DragonSkillWindow : MonoBehaviour, IExclusiveMode
{
    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private UIManager _uiManager;

    [Header("Layout Targets")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private UI_DragonSkillNode _nodePrefab;
    [SerializeField] private RectTransform _edgePrefab;
    [SerializeField] private TextMeshProUGUI[] _attributeLabels = new TextMeshProUGUI[5];
    [SerializeField] private RectTransform _activeAttributeRing;
    [SerializeField] private UI_DragonSkillDetailsPanel _detailsPanel;
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _blockerButton;

    [Header("Radii (Docs/용_스킬트리_프로토타입.html 기준)")]
    [SerializeField] private float _radiusAttributeLabel = 92f;
    [SerializeField] private float _radiusAwaken = 156f;
    [SerializeField] private float _radiusKin = 210f;
    [SerializeField] private float _kinAngleOffset = 24f;
    [SerializeField] private float _radiusActive = 236f;
    [SerializeField] private float _radiusEnhance = 316f;
    [SerializeField] private float _radiusUltimate = 396f;

    [Header("Colors")]
    // 속성 색은 DragonAttributePalette가 단일 출처다(창마다 따로 지정하면 값이 어긋난다).
    [SerializeField] private Color _edgeLockedColor = new Color(0.2f, 0.18f, 0.16f, 0.6f);

    [Header("Keys")]
    [SerializeField] private InputActionReference _closeAction;

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    private readonly Dictionary<string, DragonSkillNodeData> _nodeLookup = new();
    private readonly Dictionary<string, UI_DragonSkillNode> _nodeViews = new();
    private readonly List<EdgeView> _edgeViews = new();
    private readonly Dictionary<string, Vector2> _nodePositions = new();

    private bool _built;
    private bool _isOpen;

    // 낮/밤 이벤트용. 별도 SerializeField를 두지 않고 이미 배선된 _gameManager에서 받아 캐시한다
    // (GameManager.CycleManager는 SerializeField 기반 접근자라 Awake 시점부터 유효하다).
    // 구독과 해제가 반드시 같은 인스턴스를 보게 하려고 필드로 들고 있는다.
    private CycleManager _cycleManager;

    private struct EdgeView
    {
        public RectTransform Transform;
        public string DependentNodeId;
        public DragonType Attribute;
    }

    private void Awake()
    {
        if (_gameManager != null)
        {
            _cycleManager = _gameManager.CycleManager;
        }

        if (_exitButton != null)
        {
            _exitButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        if (_headerText != null)
        {
            _headerText.text = StringTable.GetString(DragonLocKeys.WINDOW_HEADER);
        }

        if (_detailsPanel != null)
        {
            _detailsPanel.Construct(_dragonTreeManager, _resourceManager, RefreshAll);
        }
    }

    private void OnEnable()
    {
        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.NodeUnlocked.AddListener(HandleNodeUnlocked);
            _dragonTreeManager.ActiveAttributeChanged.AddListener(HandleActiveAttributeChanged);
        }

        if (_gameManager != null && _gameManager.CurrentRun != null)
        {
            _gameManager.CurrentRun.OnInventoryChanged.AddListener(HandleInventoryChanged);
        }

        // 노드 상태는 자원 보유량과 낮/밤에도 걸리므로(코스트 부족 → 해금 버튼 비활성, 밤 → 해금 불가)
        // 창이 열려 있는 동안 그 변화도 반영해야 한다 - UI_ResearchWindow.OnEnable과 같은 구성이다.
        // OnInventoryChanged는 알·새끼용 인벤토리 전용 이벤트라 이 둘을 대신하지 못한다.
        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleCycleProgressed);
            _cycleManager.OnNightEnd.AddListener(HandleCycleProgressed);
        }

        if(_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleNodeUnlocked);
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(HandleActiveAttributeChanged);
        }

        if (_gameManager != null && _gameManager.CurrentRun != null)
        {
            _gameManager.CurrentRun.OnInventoryChanged.RemoveListener(HandleInventoryChanged);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.RemoveListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleCycleProgressed);
            _cycleManager.OnNightEnd.RemoveListener(HandleCycleProgressed);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        gameObject.SetActive(true);
        BuildTreeIfNeeded();
        RefreshAll();
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        _detailsPanel?.Clear();
        gameObject.SetActive(false);
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    // 성 창(UI_MainCastleWindow)의 스킬트리 버튼이 호출하는 진입점.
    public void ToggleFromEntryPoint()
    {
        // 클릭음을 내지 않는다 - 창을 여닫는 제스처는 Open/Close의 창음만 낸다.
        if (_isOpen)
        {
            Close();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            Open();
        }
    }

    private void BuildTreeIfNeeded()
    {
        if (_built || _dragonTreeManager == null || _dragonTreeManager.Tree == null || _content == null || _nodePrefab == null)
        {
            return;
        }

        var byAttribute = new Dictionary<DragonType, Dictionary<DragonNodeKind, DragonSkillNodeData>>();

        foreach (DragonSkillNodeData node in _dragonTreeManager.Tree.DragonNodes)
        {
            if (node == null)
            {
                continue;
            }

            _nodeLookup[node.NodeId] = node;

            if (!byAttribute.TryGetValue(node.Attribute, out Dictionary<DragonNodeKind, DragonSkillNodeData> byKind))
            {
                byKind = new Dictionary<DragonNodeKind, DragonSkillNodeData>();
                byAttribute[node.Attribute] = byKind;
            }
            byKind[node.Kind] = node;

            UI_DragonSkillNode view = Instantiate(_nodePrefab, _content);
            _nodeViews[node.NodeId] = view;
        }

        for (int i = 0; i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            DragonType attribute = ATTRIBUTES_IN_ORDER[i];
            float baseAngle = DragonSkillTreeLayout.AttributeBaseAngle(i);

            PlaceAttributeLabel(i, attribute, baseAngle);

            if (!byAttribute.TryGetValue(attribute, out Dictionary<DragonNodeKind, DragonSkillNodeData> byKind))
            {
                continue;
            }

            PlaceNode(byKind, DragonNodeKind.MotherAwaken, baseAngle, _radiusAwaken);
            PlaceNode(byKind, DragonNodeKind.MotherActive, baseAngle, _radiusActive);
            PlaceNode(byKind, DragonNodeKind.MotherEnhance, baseAngle, _radiusEnhance);
            PlaceNode(byKind, DragonNodeKind.MotherUltimate, baseAngle, _radiusUltimate);
            PlaceNode(byKind, DragonNodeKind.KinTower, baseAngle - _kinAngleOffset, _radiusKin);
            PlaceNode(byKind, DragonNodeKind.KinArea, baseAngle + _kinAngleOffset, _radiusKin);

            BuildEdges(byKind, attribute);
        }

        _built = true;
    }

    private void PlaceAttributeLabel(int index, DragonType attribute, float baseAngle)
    {
        if (_attributeLabels == null || index >= _attributeLabels.Length || _attributeLabels[index] == null)
        {
            return;
        }

        TextMeshProUGUI label = _attributeLabels[index];
        label.text = StringTable.GetString(DragonLocKeys.AttributeLocKey(attribute));
        label.color = ColorForAttribute(attribute);
        label.rectTransform.anchoredPosition = DragonSkillTreeLayout.PositionAt(baseAngle, _radiusAttributeLabel);
    }

    private void PlaceNode(
        Dictionary<DragonNodeKind, DragonSkillNodeData> byKind,
        DragonNodeKind kind,
        float angle,
        float radius)
    {
        if (!byKind.TryGetValue(kind, out DragonSkillNodeData node))
        {
            return;
        }

        Vector2 position = DragonSkillTreeLayout.PositionAt(angle, radius);
        _nodePositions[node.NodeId] = position;

        if (_nodeViews.TryGetValue(node.NodeId, out UI_DragonSkillNode view))
        {
            view.GetComponent<RectTransform>().anchoredPosition = position;
        }
    }

    private void BuildEdges(Dictionary<DragonNodeKind, DragonSkillNodeData> byKind, DragonType attribute)
    {
        CreateEdgeFromCenter(byKind, DragonNodeKind.MotherAwaken, attribute);
        CreateEdgeBetween(byKind, DragonNodeKind.MotherAwaken, DragonNodeKind.MotherActive, attribute);
        CreateEdgeBetween(byKind, DragonNodeKind.MotherActive, DragonNodeKind.MotherEnhance, attribute);
        CreateEdgeBetween(byKind, DragonNodeKind.MotherEnhance, DragonNodeKind.MotherUltimate, attribute);
        CreateEdgeBetween(byKind, DragonNodeKind.MotherAwaken, DragonNodeKind.KinTower, attribute);
        CreateEdgeBetween(byKind, DragonNodeKind.MotherAwaken, DragonNodeKind.KinArea, attribute);
    }

    private void CreateEdgeFromCenter(
        Dictionary<DragonNodeKind, DragonSkillNodeData> byKind,
        DragonNodeKind toKind,
        DragonType attribute)
    {
        if (!byKind.TryGetValue(toKind, out DragonSkillNodeData toNode))
        {
            return;
        }

        CreateEdge(Vector2.zero, toNode.NodeId, attribute);
    }

    private void CreateEdgeBetween(
        Dictionary<DragonNodeKind, DragonSkillNodeData> byKind,
        DragonNodeKind fromKind,
        DragonNodeKind toKind,
        DragonType attribute)
    {
        if (!byKind.TryGetValue(fromKind, out DragonSkillNodeData fromNode) ||
            !byKind.TryGetValue(toKind, out DragonSkillNodeData toNode) ||
            !_nodePositions.TryGetValue(fromNode.NodeId, out Vector2 fromPosition))
        {
            return;
        }

        CreateEdge(fromPosition, toNode.NodeId, attribute);
    }

    private void CreateEdge(Vector2 from, string toNodeId, DragonType attribute)
    {
        if (_edgePrefab == null || !_nodePositions.TryGetValue(toNodeId, out Vector2 to))
        {
            return;
        }

        RectTransform edge = Instantiate(_edgePrefab, _content);
        edge.SetAsFirstSibling(); // 노드 원 아래로 - 선이 클릭을 가로채거나 위에 그려지지 않게 한다
        edge.anchoredPosition = from;

        Vector2 delta = to - from;
        float distance = delta.magnitude;
        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        edge.localRotation = Quaternion.Euler(0f, 0f, -angle);

        Vector2 size = edge.sizeDelta;
        size.y = distance;
        edge.sizeDelta = size;

        _edgeViews.Add(new EdgeView { Transform = edge, DependentNodeId = toNodeId, Attribute = attribute });
    }

    private void HandleNodeUnlocked(ProgressionNodeData node) => RefreshAll();
    private void HandleActiveAttributeChanged(DragonType attribute) => RefreshActiveAttributeRing();
    private void HandleInventoryChanged() => RefreshAll();
    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();
    private void HandleCycleProgressed(int value) => RefreshAll();

    private void RefreshAll()
    {
        RefreshNodeViews();
        RefreshEdgeViews();
        RefreshActiveAttributeRing();
        _detailsPanel?.Refresh();
    }

    private void RefreshNodeViews()
    {
        if (_dragonTreeManager == null)
        {
            return;
        }

        foreach (KeyValuePair<string, UI_DragonSkillNode> entry in _nodeViews)
        {
            if (!_nodeLookup.TryGetValue(entry.Key, out DragonSkillNodeData node))
            {
                continue;
            }

            ProgressionNodeState state = _dragonTreeManager.GetNodeState(node);
            Color attributeColor = ColorForAttribute(node.Attribute);
            string badge = BuildBadgeText(node, state);

            entry.Value.Bind(node, state, attributeColor, badge, HandleNodeClicked);
        }
    }

    private void RefreshEdgeViews()
    {
        if (_dragonTreeManager == null)
        {
            return;
        }

        foreach (EdgeView edge in _edgeViews)
        {
            if (edge.Transform == null)
            {
                continue;
            }

            Image image = edge.Transform.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            bool lit = _dragonTreeManager.IsUnlocked(edge.DependentNodeId);
            image.color = lit ? ColorForAttribute(edge.Attribute) : _edgeLockedColor;
        }
    }

    private void RefreshActiveAttributeRing()
    {
        if (_activeAttributeRing == null || _dragonTreeManager == null)
        {
            return;
        }

        DragonType? active = _dragonTreeManager.ActiveAttribute;

        if (!active.HasValue)
        {
            _activeAttributeRing.gameObject.SetActive(false);
            return;
        }

        int index = Array.IndexOf(ATTRIBUTES_IN_ORDER, active.Value);
        if (index < 0 || _attributeLabels == null || index >= _attributeLabels.Length || _attributeLabels[index] == null)
        {
            _activeAttributeRing.gameObject.SetActive(false);
            return;
        }

        _activeAttributeRing.gameObject.SetActive(true);
        _activeAttributeRing.anchoredPosition = _attributeLabels[index].rectTransform.anchoredPosition;

        Image ringImage = _activeAttributeRing.GetComponent<Image>();
        if (ringImage != null)
        {
            ringImage.color = ColorForAttribute(active.Value);
        }
    }

    private string BuildBadgeText(DragonSkillNodeData node, ProgressionNodeState state)
    {
        if (state == ProgressionNodeState.GateLocked)
        {
            string lockedLocKey = _dragonTreeManager.GetFirstFailingGateLocKey(node);
            return lockedLocKey != null ? StringTable.GetString(lockedLocKey) : null;
        }

        if (state == ProgressionNodeState.PrerequisiteLocked)
        {
            return StringTable.GetString(DragonLocKeys.STATE_PREREQUISITE_LOCKED);
        }

        return null;
    }

    private void HandleNodeClicked(DragonSkillNodeData node)
    {
        _detailsPanel?.Show(node);
    }

    private Color ColorForAttribute(DragonType attribute) => DragonAttributePalette.ColorOf(attribute);
}
