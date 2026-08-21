using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// 용 스킬트리 - DragonSkillTree.asset을 순회해 방사형으로 런타임 배치하고, 클릭 시 상세 패널에
// 바인딩한다. 해금/속성 변경 규칙은 여기서 재구현하지 않는다 - 전부 DragonTreeManager에 위임.
//
// 노드 하나 = 강화 한 단계다. 랭크는 에셋 단계에서 이미 별도 노드로 체인돼 있으므로
// (속성 5 × 슬롯 8 × 랭크 = 70개), 뷰도 그 노드를 그대로 하나씩 그린다.
// 같은 슬롯의 랭크는 같은 갈래의 "다음 칸"을 차지하고, 라벨이 같으므로 Lv 1/2 뱃지로 구분한다.
//
// Dragon_window의 Panel_MotherDragon/Right_Scroll View_skillTree에 부착해 쓰는 '심는 패널'이다.
// 창 자체를 여닫는 건 UI_DragonWindow가 하고, 이 컴포넌트는 부모 패널이 켜질 때 도는
// OnEnable에서 트리를 빌드·갱신한다.
public class UI_DragonSkillWindow : MonoBehaviour
{
    // 속성 이름표를 피해 기준선을 시작할 여유. 이름표 높이의 절반보다 크게 잡는다.
    private const float ATTRIBUTE_LABEL_CLEARANCE = 34f;

    // 가장 바깥 노드의 원 바깥으로도 라벨·뱃지가 나가므로, Content를 그만큼 더 키워 둔다.
    // 여기가 모자라면 ScrollRect가 바깥 링을 화면 안으로 못 끌어와 클릭조차 되지 않는다.
    private const float CONTENT_EDGE_MARGIN = 160f;

    // 창을 열었을 때의 배율 상한. 원본 크기(1)를 넘겨 확대하지는 않는다.
    private const float MAX_INITIAL_ZOOM = 1f;

    private const float DEFAULT_INITIAL_ZOOM_MULTIPLIER = 1.6f;
    private const float MIN_INITIAL_ZOOM_MULTIPLIER = 0.1f;

    // 반지름 → 지름.
    private const float DIAMETER_PER_RADIUS = 2f;

    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("Layout Targets")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private UI_DragonSkillNode _nodePrefab;
    [SerializeField] private UI_DragonSkillEdge _edgePrefab;
    [Tooltip("속성 이름표. 5속성만큼 런타임에 찍어 방사형 기준선 위에 놓는다.")]
    [SerializeField] private TextMeshProUGUI _attributeLabelPrefab;
    [SerializeField] private UI_DragonSkillDetailsPanel _detailsPanel;

    [Tooltip("새끼용 노드에 얹을 속성별 알 아이콘의 출처. 비워 두면 아이콘 없이 테두리로만 구분한다.")]
    [SerializeField] private BabyDragonDataCatalog _babyDragonCatalog;

    [Tooltip("슬롯 종류별 아이콘. 여기 채운 것이 우선하고, 비워 둔 슬롯은 기존 규칙을 그대로 쓴다 " +
        "(루트=그 속성의 액티브 스킬 아이콘, 새끼용=알 아이콘, 나머지=아이콘 없음).")]
    [SerializeField] private SlotIcon[] _slotIcons;

    // 반지름 간격(Step)은 "노드 지름 + 라벨 + 뱃지"보다 커야 한다 - 좁히면 안쪽 노드의 뱃지가
    // 바깥 노드의 원 위에 올라탄다. 노드 규격은 DragonSkillNodeStyleTable이 들고 있다.
    [Header("Radii")]
    [SerializeField] private float _radiusAttributeLabel = 135f;
    // 구 이름을 남겨 프리팹에 이미 조정돼 있던 반지름 값을 잃지 않는다
    // (슬롯 구성이 바뀌면서 Awaken→Unlock으로 역할이 옮겨갔다).
    [FormerlySerializedAs("_radiusAwaken")]
    [SerializeField] private float _radiusUnlock = 250f;

    [Tooltip("새끼용 갈래의 첫 칸(랭크 1) 반지름.")]
    [SerializeField] private float _radiusKinFirst = 350f;
    [Tooltip("새끼용 갈래에서 다음 강화 단계로 나갈 때 더하는 반지름.")]
    [SerializeField] private float _radiusKinStep = 150f;
    [Tooltip("새끼용 두 갈래를 속성 기준선 양옆으로 벌리는 각도. 속성 간격(72도)의 절반을 넘기면 옆 속성의 새끼용과 붙는다.")]
    [SerializeField] private float _kinAngleOffset = 28f;

    [Tooltip("어미용 갈래(액티브·패시브)의 첫 칸 반지름.")]
    [SerializeField] private float _radiusBranchFirst = 440f;
    [Tooltip("어미용 갈래에서 다음 강화 단계로 나갈 때 더하는 반지름. 슬롯이 바뀌든 랭크가 오르든 한 칸이다.")]
    [SerializeField] private float _radiusBranchStep = 160f;
    [Tooltip("액티브 갈래와 패시브 갈래를 속성 기준선 양옆으로 벌리는 각도.")]
    [SerializeField] private float _branchAngleOffset = 11f;

    [SerializeField] private float _radiusUltimate = 1080f;

    [Header("Initial Zoom")]
    [Tooltip("창을 열었을 때의 시작 배율. '트리 전체가 뷰포트에 들어가는 배율'에 이 값을 곱한다. " +
        "1이면 전체가 한 화면에 들어오고, 크게 잡으면 확대된 상태로 시작한다(바깥 링은 휠·드래그로 본다). " +
        "결과는 원본 크기(1배)를 넘지 않는다.")]
    [Min(MIN_INITIAL_ZOOM_MULTIPLIER)]
    [SerializeField] private float _initialZoomMultiplier = DEFAULT_INITIAL_ZOOM_MULTIPLIER;

    [Header("Colors")]
    // 속성 색은 DragonAttributePalette가 단일 출처다(창마다 따로 지정하면 값이 어긋난다).
    [Tooltip("아직 못 산 연결선의 바탕색. 여기에 속성 색을 옅게 섞어 갈래마다 색이 남게 한다.")]
    [SerializeField] private Color _edgeLockedColor = new Color(0.2f, 0.18f, 0.16f, 0.6f);

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    // 액티브·패시브 갈래를 이루는 슬롯 순서. 갈래 안에서는 이 순서대로, 슬롯 안에서는 랭크 순서대로
    // 바깥을 향해 한 칸씩 나간다.
    private static readonly DragonNodeKind[] ACTIVE_BRANCH_SLOTS =
        { DragonNodeKind.ActiveUp1, DragonNodeKind.ActiveUp2 };

    private static readonly DragonNodeKind[] PASSIVE_BRANCH_SLOTS =
        { DragonNodeKind.PassiveUp1, DragonNodeKind.PassiveUp2 };

    // 노드 하나당 뷰 하나. 랭크가 다르면 다른 노드이므로 다른 원이 된다.
    private readonly Dictionary<DragonSkillNodeData, UI_DragonSkillNode> _nodeViews = new();
    private readonly Dictionary<DragonSkillNodeData, Vector2> _nodePositions = new();
    private readonly List<EdgeView> _edgeViews = new();

    // 트리 중심에서 가장 먼 노드까지의 거리. Content 크기와 첫 배율을 여기서 끌어낸다 -
    // 반지름 값을 손볼 때마다 프리팹의 Content 크기를 같이 고치는 걸 잊지 않기 위해서다.
    private float _maxPlacedRadius;

    private bool _built;

    // 상세 패널이 보고 있는 노드.
    private DragonSkillNodeData _selectedNode;

    // 낮/밤 이벤트용. 구독과 해제가 반드시 같은 인스턴스를 보게 하려고 필드로 들고 있는다.
    private CycleManager _cycleManager;

    // 슬롯 종류 하나에 붙일 아이콘. 속성별로 갈리지 않는다 - 같은 슬롯은 5속성이 같은 그림을 쓰고,
    // 속성 구분은 이미 노드 색과 갈래 위치가 하고 있다.
    [Serializable]
    private struct SlotIcon
    {
        public DragonNodeKind Kind;
        public Sprite Icon;
    }

    private struct EdgeView
    {
        public UI_DragonSkillEdge View;

        /// <summary>이 선이 향하는(=이 선을 켜고 끄는) 노드.</summary>
        public DragonSkillNodeData DependentNode;

        public DragonType Attribute;
    }

    private void Awake()
    {
        if (_gameManager != null)
        {
            _cycleManager = _gameManager.CycleManager;
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
        }

        if (_gameManager != null && _gameManager.CurrentRun != null)
        {
            _gameManager.CurrentRun.OnInventoryChanged.AddListener(HandleInventoryChanged);
        }

        // 노드 상태는 자원 보유량과 낮/밤에도 걸리므로(코스트 부족 → 해금 버튼 비활성, 밤 → 해금 불가)
        // 창이 열려 있는 동안 그 변화도 반영해야 한다.
        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleCycleProgressed);
            _cycleManager.OnNightEnd.AddListener(HandleCycleProgressed);
        }

        BuildTreeIfNeeded();

        // 배율·위치 되돌리기는 열 때마다 한다 - 지난번에 한 갈래를 확대해 둔 채로 닫았으면
        // 다시 열었을 때 어느 속성을 보고 있는지조차 알 수 없는 화면에서 시작한다.
        FitContentToViewport();
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

        // 상세 팝업은 이 스크롤뷰가 아니라 Dragon_window 루트의 자식이라 어미용 탭이 꺼져도
        // 스스로 사라지지 않는다 - 새끼용 탭 위에 남지 않도록 여기서 닫는다.
        _selectedNode = null;
        _detailsPanel?.Clear();
    }

    /// <summary>
    /// 이 노드가 그려진 자리. 노드는 런타임 생성이라 <see cref="GuideAnchor"/>로 잡을 수 없어
    /// 안내가 창에 직접 물어본다(<see cref="UI_BuildModeWindow.TryGetSlotRect"/>와 같은 형태).
    ///
    /// 트리는 창을 처음 열 때 만들어지므로, 열기 전에 물으면 아직 없다 - 그때는 false다.
    ///
    /// <b>창을 닫은 뒤에도 false여야 한다.</b> 노드 뷰는 창이 닫혀도 _nodeViews에 그대로 남으므로,
    /// 있는지만 보면 화면에 없는 자리를 가리키게 된다. 그러면 안내는 대상이 살아 있다고 믿어
    /// 딤과 입력 차단을 유지하는데 오버레이는 보이지 않는 대상이라 연출을 감춘다 -
    /// 화면에 아무것도 없는 채로 단축키만 죽고, 빠져나갈 확인 버튼도 함께 숨는다.
    /// 활성 여부까지 보면 러너의 "대상이 사라졌으면 화면을 걷는다" 경로가 정상적으로 돈다.
    /// </summary>
    public bool TryGetNodeRect(DragonSkillNodeData node, out RectTransform nodeRect)
    {
        nodeRect = null;

        if (node == null || !_nodeViews.TryGetValue(node, out UI_DragonSkillNode view) ||
            view == null || !view.gameObject.activeInHierarchy)
        {
            return false;
        }

        nodeRect = view.transform as RectTransform;
        return nodeRect != null;
    }

    /// <summary>
    /// 떠 있는 상세 패널을 닫는다. 닫을 것이 있었으면 참을 돌려준다 -
    /// 용 창이 Esc를 창 닫기에 쓸지 이쪽에 쓸지 그 값으로 가른다
    /// (<see cref="UI_DragonWindow.OnCloseActionPerformed"/>).
    ///
    /// 상세 패널에는 닫기 버튼이 없고 트리 오른쪽을 덮는다. 연구 창과 같은 갇힘이라 같은 방식으로 푼다.
    /// </summary>
    public bool TryCloseDetailsPanel()
    {
        if (_detailsPanel == null || !_detailsPanel.IsOpen)
        {
            return false;
        }

        _selectedNode = null;
        _detailsPanel.Clear();
        return true;
    }

    private void BuildTreeIfNeeded()
    {
        if (_built || _dragonTreeManager == null || _dragonTreeManager.Tree == null ||
            _content == null || _nodePrefab == null)
        {
            return;
        }

        Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>> slotNodes = GroupNodesBySlot();

        for (int i = 0; i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            DragonType attribute = ATTRIBUTES_IN_ORDER[i];
            float baseAngle = DragonSkillTreeLayout.AttributeBaseAngle(i);

            PlaceAttributeLabel(attribute, baseAngle);
            PlaceAttributeBranches(slotNodes, attribute, baseAngle);
        }

        ResizeContentToTree();

        _built = true;
    }

    private Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>> GroupNodesBySlot()
    {
        var slotNodes = new Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>>();

        foreach (DragonSkillNodeData node in _dragonTreeManager.Tree.DragonNodes)
        {
            if (node == null)
            {
                continue;
            }

            var key = (node.Attribute, node.Kind);

            if (!slotNodes.TryGetValue(key, out List<DragonSkillNodeData> ranks))
            {
                ranks = new List<DragonSkillNodeData>();
                slotNodes[key] = ranks;
            }

            ranks.Add(node);
        }

        // 트리 에셋의 나열 순서를 믿지 않는다 - 안쪽에서 바깥쪽 순서가 랭크 순서여야 한다.
        foreach (List<DragonSkillNodeData> ranks in slotNodes.Values)
        {
            ranks.Sort((left, right) => left.Rank.CompareTo(right.Rank));
        }

        return slotNodes;
    }

    private void PlaceAttributeBranches(
        Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>> slotNodes,
        DragonType attribute,
        float baseAngle)
    {
        // 속성 기준선은 트리 중심이 아니라 속성 이름표 바로 바깥에서 시작한다 -
        // 중심에서 그으면 선이 이름표를 관통해 글자를 읽기 어렵다.
        Vector2 spokeStart = DragonSkillTreeLayout.PositionAt(
            baseAngle, _radiusAttributeLabel + ATTRIBUTE_LABEL_CLEARANCE);

        // 해금 노드가 이 속성의 뿌리다 - 나머지 갈래는 전부 여기서 뻗어 나간다.
        Vector2 rootPosition = PlaceChain(
            slotNodes, attribute, baseAngle, _radiusUnlock, 0f, spokeStart,
            DragonNodeKind.ActiveUnlock);

        PlaceChain(
            slotNodes, attribute, baseAngle - _kinAngleOffset,
            _radiusKinFirst, _radiusKinStep, rootPosition, DragonNodeKind.KinTower);

        PlaceChain(
            slotNodes, attribute, baseAngle + _kinAngleOffset,
            _radiusKinFirst, _radiusKinStep, rootPosition, DragonNodeKind.KinArea);

        PlaceChain(
            slotNodes, attribute, baseAngle - _branchAngleOffset,
            _radiusBranchFirst, _radiusBranchStep, rootPosition, ACTIVE_BRANCH_SLOTS);

        PlaceChain(
            slotNodes, attribute, baseAngle + _branchAngleOffset,
            _radiusBranchFirst, _radiusBranchStep, rootPosition, PASSIVE_BRANCH_SLOTS);

        PlaceChain(
            slotNodes, attribute, baseAngle, _radiusUltimate, 0f, rootPosition,
            DragonNodeKind.Ultimate);
    }

    /// <summary>
    /// 한 갈래를 안쪽에서 바깥쪽으로 심는다. 슬롯 안의 랭크도 같은 갈래의 다음 칸을 차지하므로,
    /// 강화 단계가 몇 개인지는 트리 에셋이 정하고 여기서는 세지 않는다.
    /// 노드를 놓으면서 <paramref name="edgeOrigin"/> → 첫 칸 → 다음 칸 순으로 연결선도 잇는다.
    /// </summary>
    /// <returns>마지막으로 놓인 노드의 좌표. 놓인 노드가 없으면 <paramref name="edgeOrigin"/>.</returns>
    private Vector2 PlaceChain(
        Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>> slotNodes,
        DragonType attribute,
        float angle,
        float firstRadius,
        float radiusStep,
        Vector2 edgeOrigin,
        params DragonNodeKind[] kinds)
    {
        Vector2 previousPosition = edgeOrigin;
        int step = 0;

        foreach (DragonNodeKind kind in kinds)
        {
            if (!slotNodes.TryGetValue((attribute, kind), out List<DragonSkillNodeData> ranks))
            {
                continue;
            }

            foreach (DragonSkillNodeData node in ranks)
            {
                float radius = firstRadius + radiusStep * step;
                Vector2 position = PlaceNode(node, angle, radius);
                CreateEdge(previousPosition, node, attribute);

                previousPosition = position;
                step++;
            }
        }

        return previousPosition;
    }

    // 속성 이름표는 창 프리팹에 5개를 심어 두는 대신 여기서 찍는다 - 속성이 늘거나
    // 각도 규칙이 바뀔 때 프리팹을 손대지 않아도 된다.
    private void PlaceAttributeLabel(DragonType attribute, float baseAngle)
    {
        if (_attributeLabelPrefab == null)
        {
            return;
        }

        TextMeshProUGUI label = Instantiate(_attributeLabelPrefab, _content);
        label.text = StringTable.GetString(DragonLocKeys.AttributeLocKey(attribute));
        label.color = ColorForAttribute(attribute);
        label.rectTransform.anchoredPosition = DragonSkillTreeLayout.PositionAt(baseAngle, _radiusAttributeLabel);
    }

    private Vector2 PlaceNode(DragonSkillNodeData node, float angle, float radius)
    {
        Vector2 position = DragonSkillTreeLayout.PositionAt(angle, radius);

        UI_DragonSkillNode view = Instantiate(_nodePrefab, _content);
        view.GetComponent<RectTransform>().anchoredPosition = position;

        // 종류별 크기 규격만 입힌다 - 라벨·뱃지 위치는 노드 프리팹에 잡아 둔 자리를 그대로 쓴다.
        view.ApplyLayout(node.Kind);

        _nodeViews[node] = view;
        _nodePositions[node] = position;
        _maxPlacedRadius = Mathf.Max(_maxPlacedRadius, radius);

        return position;
    }

    private void CreateEdge(Vector2 from, DragonSkillNodeData toNode, DragonType attribute)
    {
        if (_edgePrefab == null || !_nodePositions.TryGetValue(toNode, out Vector2 to))
        {
            return;
        }

        UI_DragonSkillEdge edge = Instantiate(_edgePrefab, _content);

        // 노드 원 아래로 - 선이 클릭을 가로채거나 위에 그려지지 않게 한다.
        edge.transform.SetAsFirstSibling();
        edge.SetEndpoints(from, to);

        _edgeViews.Add(new EdgeView { View = edge, DependentNode = toNode, Attribute = attribute });
    }

    // Content를 실제로 놓인 트리 크기에 맞춘다 - 프리팹에 박아 둔 크기보다 트리가 커지면
    // ScrollRect가 바깥 링까지 스크롤해 주지 않아 노드를 볼 수도 누를 수도 없게 된다.
    private void ResizeContentToTree()
    {
        float extent = _maxPlacedRadius + CONTENT_EDGE_MARGIN;
        float side = extent * DIAMETER_PER_RADIUS;
        _content.sizeDelta = new Vector2(side, side);
    }

    // 시작 배율을 정하고 중앙으로 되돌린다. 기준은 '트리 전체가 뷰포트에 들어가는 배율'이고,
    // 여기에 _initialZoomMultiplier를 곱해 확대된 상태로 열 수 있다 - 트리가 커서 전체를
    // 담으면 노드 글씨를 읽을 수 없기 때문이다. 이후 배율은 휠(UI_ScrollRectZoom)과 드래그가
    // 이어받으므로 여기서는 시작 상태만 정한다.
    private void FitContentToViewport()
    {
        if (_content == null || _content.parent is not RectTransform viewport)
        {
            return;
        }

        Vector2 size = _content.sizeDelta;
        Rect viewRect = viewport.rect;

        if (size.x <= 0f || size.y <= 0f || viewRect.width <= 0f || viewRect.height <= 0f)
        {
            return;
        }

        float fitScale = Mathf.Min(viewRect.width / size.x, viewRect.height / size.y);
        float scale = Mathf.Min(fitScale * _initialZoomMultiplier, MAX_INITIAL_ZOOM);
        _content.localScale = new Vector3(scale, scale, 1f);
        _content.anchoredPosition = Vector2.zero;
    }

    private void HandleNodeUnlocked(ProgressionNodeData node) => RefreshAll();
    private void HandleInventoryChanged() => RefreshAll();
    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();
    private void HandleCycleProgressed(int value) => RefreshAll();

    private void RefreshAll()
    {
        RefreshNodeViews();
        RefreshEdgeViews();
        RefreshDetailsPanel();
    }

    private void RefreshNodeViews()
    {
        if (!WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this))
        {
            return;
        }

        foreach (KeyValuePair<DragonSkillNodeData, UI_DragonSkillNode> entry in _nodeViews)
        {
            DragonSkillNodeData node = entry.Key;
            ProgressionNodeState state = _dragonTreeManager.GetNodeState(node);

            entry.Value.Bind(
                node,
                state,
                ColorForAttribute(node.Attribute),
                IconFor(node),
                BuildBadgeText(node, state),
                HandleNodeClicked);
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
            if (edge.View == null)
            {
                continue;
            }

            bool lit = _dragonTreeManager.IsUnlocked(edge.DependentNode.NodeId);
            edge.View.Bind(lit, ColorForAttribute(edge.Attribute), _edgeLockedColor);
        }
    }

    // 강화 단계가 여러 개인 슬롯은 같은 이름의 노드가 나란히 놓이므로, 몇 번째 단계인지를
    // 항상 뱃지로 보여준다(잠금 사유는 상세 패널이 설명한다).
    private string BuildBadgeText(DragonSkillNodeData node, ProgressionNodeState state)
    {
        if (node.MaxRank > 1)
        {
            return string.Format(StringTable.GetString(DragonLocKeys.RANK_BADGE), node.Rank, node.MaxRank);
        }

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
        _selectedNode = node;

        // 노드에 얹은 것과 같은 아이콘을 그대로 넘긴다 - 상세 패널이 규칙을 다시 구현하면
        // 슬롯 아이콘 표를 고칠 때 두 화면이 어긋난다.
        _detailsPanel?.Show(node, IconFor(node));
    }

    private void RefreshDetailsPanel()
    {
        if (_detailsPanel == null)
        {
            return;
        }

        // 사용자가 빈 공간을 눌러 패널을 닫았을 수도 있다 - 닫힌 패널에 Show를 부르면 되살아난다.
        if (!_detailsPanel.IsOpen)
        {
            _selectedNode = null;
            return;
        }

        _detailsPanel.Refresh();
    }

    // 슬롯별로 지정한 아이콘이 있으면 그것을, 없으면 기존 규칙(루트=어미용 액티브 스킬 아이콘,
    // 새끼용=알 아이콘)을 쓴다.
    private Sprite IconFor(DragonSkillNodeData node)
    {
        if (TryGetSlotIcon(node.Kind, out Sprite slotIcon))
        {
            return slotIcon;
        }

        if (node.Kind == DragonNodeKind.ActiveUnlock)
        {
            SkillSO skill = _dragonTreeManager?.GetActiveSkillOf(node.Attribute);
            return skill != null ? skill.Sprite : null;
        }

        if (_babyDragonCatalog == null || !DragonSkillNodeStyleTable.For(node.Kind).IsKin)
        {
            return null;
        }

        return _babyDragonCatalog.TryResolve(node.Attribute, out BabyDragonData data) ? data.EggSprite : null;
    }

    // 스프라이트를 넣지 않은 항목은 "지정하지 않음"으로 본다 - 표에 슬롯을 등록해 두고
    // 그림만 비워 둔 상태에서 기존 규칙이 죽어버리면 원인을 찾기 어렵다.
    private bool TryGetSlotIcon(DragonNodeKind kind, out Sprite icon)
    {
        icon = null;

        if (_slotIcons == null)
        {
            return false;
        }

        foreach (SlotIcon entry in _slotIcons)
        {
            if (entry.Kind == kind && entry.Icon != null)
            {
                icon = entry.Icon;
                return true;
            }
        }

        return false;
    }

    private Color ColorForAttribute(DragonType attribute) => DragonAttributePalette.ColorOf(attribute);
}
