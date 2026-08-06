using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 용 스킬트리 - DragonSkillTree.asset을 순회해 30노드+30엣지를 방사형으로 런타임 배치하고,
// 클릭 시 상세 패널에 바인딩한다. 해금/속성 변경 규칙은 여기서 재구현하지 않는다
// - 전부 DragonTreeManager에 위임.
//
// Dragon_window의 Panel_MotherDragon/Right_Scroll View_skillTree에 부착해 쓰는 '심는 패널'이다.
// 독립 창(blocker + ExitButton + IExclusiveMode)이었던 시절의 여닫기 기능은 제거했다 -
// 이제 창 자체를 여닫는 건 UI_DragonWindow가 하고, 이 컴포넌트는 부모 패널이 켜질 때 도는
// OnEnable에서 트리를 빌드·갱신한다.
public class UI_DragonSkillWindow : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("Layout Targets")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private UI_DragonSkillNode _nodePrefab;
    [SerializeField] private RectTransform _edgePrefab;
    [SerializeField] private TextMeshProUGUI[] _attributeLabels = new TextMeshProUGUI[5];
    [SerializeField] private UI_DragonSkillDetailsPanel _detailsPanel;

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

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    private readonly Dictionary<string, DragonSkillNodeData> _nodeLookup = new();
    private readonly Dictionary<string, UI_DragonSkillNode> _nodeViews = new();
    private readonly List<EdgeView> _edgeViews = new();
    private readonly Dictionary<string, Vector2> _nodePositions = new();

    private bool _built;

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

        // ExitButton/blocker/헤더는 독립 창이던 시절의 것이라 제거했다 - 여닫기는 UI_DragonWindow 담당.

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

        // 어미용 탭이 켜질 때(부모 Panel_MotherDragon의 SetActive) 여기가 진입점이 된다 -
        // 창을 여는 주체가 UI_DragonWindow로 옮겨가 Open()이 없어졌기 때문이다.
        // _built 플래그가 트리 재생성을 막고 RefreshAll은 멱등하므로 탭을 여러 번 오가도 안전하다.
        BuildTreeIfNeeded();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleNodeUnlocked);
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

        // 상세 팝업(Popup_SkillDetailsPanel)은 이 스크롤뷰가 아니라 Dragon_window 루트의 자식이라
        // 어미용 탭이 꺼져도 스스로 사라지지 않는다 - 새끼용 탭 위에 남지 않도록 여기서 닫는다.
        // (없어진 Close()가 하던 일이다.)
        _detailsPanel?.Clear();
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
    // 속성 링(_activeAttributeRing)은 제거했다 - 노드 상태가 ActiveAttribute에 걸리지 않아
    // 이 창이 속성 변경을 구독할 이유가 없다(속성 표시는 UI_DragonWindow 좌측 프레임 담당).
    private void HandleInventoryChanged() => RefreshAll();
    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();
    private void HandleCycleProgressed(int value) => RefreshAll();

    private void RefreshAll()
    {
        RefreshNodeViews();
        RefreshEdgeViews();
        _detailsPanel?.Refresh();
    }

    private void RefreshNodeViews()
    {
        if (!WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this))
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
        if (!WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this))
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
