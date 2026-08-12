using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 용 스킬트리 - DragonSkillTree.asset을 순회해 방사형으로 런타임 배치하고, 클릭 시 상세 패널에
// 바인딩한다. 해금/속성 변경 규칙은 여기서 재구현하지 않는다 - 전부 DragonTreeManager에 위임.
//
// 랭크는 별도 노드로 체인돼 있지만(속성 5 × 슬롯 8 × 랭크 = 70개), 원을 70개 그리면 읽을 수 없어
// 뷰는 (속성, 슬롯) 단위로 하나만 만든다. 그 원이 대표하는 노드는 "다음에 살 랭크"이고,
// 뱃지에 Lv 1/2처럼 진행도를 표시한다.
//
// Dragon_window의 Panel_MotherDragon/Right_Scroll View_skillTree에 부착해 쓰는 '심는 패널'이다.
// 창 자체를 여닫는 건 UI_DragonWindow가 하고, 이 컴포넌트는 부모 패널이 켜질 때 도는
// OnEnable에서 트리를 빌드·갱신한다.
public class UI_DragonSkillWindow : MonoBehaviour
{
    // 속성 이름표를 피해 기준선을 시작할 여유. 이름표 높이의 절반보다 크게 잡는다.
    private const float ATTRIBUTE_LABEL_CLEARANCE = 34f;

    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("Layout Targets")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private UI_DragonSkillNode _nodePrefab;
    [SerializeField] private RectTransform _edgePrefab;
    [Tooltip("속성 이름표. 5속성만큼 런타임에 찍어 방사형 기준선 위에 놓는다.")]
    [SerializeField] private TextMeshProUGUI _attributeLabelPrefab;
    [SerializeField] private UI_DragonSkillDetailsPanel _detailsPanel;

    [Tooltip("새끼용 노드에 얹을 속성별 알 아이콘의 출처. 비워 두면 아이콘 없이 테두리로만 구분한다.")]
    [SerializeField] private BabyDragonDataCatalog _babyDragonCatalog;

    // 노드가 커지고 라벨이 노드 바깥으로 나오면서, 반지름은 "노드 지름 + 라벨"이 겹치지 않을 만큼
    // 벌려 둔다. 줄이려면 DragonSkillNodeStyleTable의 노드 지름도 같이 줄여야 한다.
    [Header("Radii")]
    [SerializeField] private float _radiusAttributeLabel = 135f;
    // 구 이름을 남겨 프리팹에 이미 조정돼 있던 반지름 값을 잃지 않는다
    // (슬롯 구성이 바뀌면서 Awaken→Unlock, Active/Enhance→Branch1/2로 역할이 옮겨갔다).
    [FormerlySerializedAs("_radiusAwaken")]
    [SerializeField] private float _radiusUnlock = 250f;
    [SerializeField] private float _radiusKin = 340f;
    [Tooltip("새끼용 두 갈래를 속성 기준선 양옆으로 벌리는 각도. 속성 간격(72도)의 절반을 넘기면 옆 속성의 새끼용과 붙는다.")]
    [SerializeField] private float _kinAngleOffset = 22f;
    [FormerlySerializedAs("_radiusActive")]
    [SerializeField] private float _radiusBranch1 = 490f;
    [FormerlySerializedAs("_radiusEnhance")]
    [SerializeField] private float _radiusBranch2 = 650f;
    [Tooltip("액티브 갈래와 패시브 갈래를 속성 기준선 양옆으로 벌리는 각도.")]
    [SerializeField] private float _branchAngleOffset = 15f;
    [SerializeField] private float _radiusUltimate = 770f;

    [Header("Colors")]
    // 속성 색은 DragonAttributePalette가 단일 출처다(창마다 따로 지정하면 값이 어긋난다).
    [Tooltip("아직 못 산 연결선의 바탕색. 여기에 속성 색을 옅게 섞어 갈래마다 색이 남게 한다.")]
    [SerializeField] private Color _edgeLockedColor = new Color(0.2f, 0.18f, 0.16f, 0.6f);

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    // 한 슬롯을 이루는 랭크 노드들(랭크 오름차순)과 그 슬롯의 뷰·좌표.
    private readonly Dictionary<(DragonType, DragonNodeKind), List<DragonSkillNodeData>> _slotNodes = new();
    private readonly Dictionary<(DragonType, DragonNodeKind), UI_DragonSkillNode> _slotViews = new();
    private readonly Dictionary<(DragonType, DragonNodeKind), Vector2> _slotPositions = new();
    private readonly List<EdgeView> _edgeViews = new();

    private bool _built;

    // 상세 패널이 보고 있는 슬롯. 랭크를 사면 대표 노드가 다음 랭크로 바뀌므로,
    // 노드가 아니라 슬롯을 기억해 두었다가 갱신 때 새 대표 노드로 다시 바인딩한다.
    private (DragonType, DragonNodeKind)? _selectedSlot;

    // 낮/밤 이벤트용. 구독과 해제가 반드시 같은 인스턴스를 보게 하려고 필드로 들고 있는다.
    private CycleManager _cycleManager;

    private struct EdgeView
    {
        public RectTransform Transform;
        public (DragonType, DragonNodeKind) DependentSlot;
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
        _selectedSlot = null;
        _detailsPanel?.Clear();
    }

    private void BuildTreeIfNeeded()
    {
        if (_built || _dragonTreeManager == null || _dragonTreeManager.Tree == null ||
            _content == null || _nodePrefab == null)
        {
            return;
        }

        foreach (DragonSkillNodeData node in _dragonTreeManager.Tree.DragonNodes)
        {
            if (node == null)
            {
                continue;
            }

            var key = (node.Attribute, node.Kind);

            if (!_slotNodes.TryGetValue(key, out List<DragonSkillNodeData> ranks))
            {
                ranks = new List<DragonSkillNodeData>();
                _slotNodes[key] = ranks;
                _slotViews[key] = Instantiate(_nodePrefab, _content);
            }

            ranks.Add(node);
        }

        // 트리 에셋의 나열 순서를 믿지 않는다 - 대표 노드 선택이 랭크 순서에 의존하기 때문이다.
        foreach (List<DragonSkillNodeData> ranks in _slotNodes.Values)
        {
            ranks.Sort((left, right) => left.Rank.CompareTo(right.Rank));
        }

        for (int i = 0; i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            DragonType attribute = ATTRIBUTES_IN_ORDER[i];
            float baseAngle = DragonSkillTreeLayout.AttributeBaseAngle(i);

            PlaceAttributeLabel(attribute, baseAngle);

            PlaceSlot(attribute, DragonNodeKind.ActiveUnlock, baseAngle, _radiusUnlock);
            PlaceSlot(attribute, DragonNodeKind.ActiveUp1, baseAngle - _branchAngleOffset, _radiusBranch1);
            PlaceSlot(attribute, DragonNodeKind.ActiveUp2, baseAngle - _branchAngleOffset, _radiusBranch2);
            PlaceSlot(attribute, DragonNodeKind.PassiveUp1, baseAngle + _branchAngleOffset, _radiusBranch1);
            PlaceSlot(attribute, DragonNodeKind.PassiveUp2, baseAngle + _branchAngleOffset, _radiusBranch2);
            PlaceSlot(attribute, DragonNodeKind.Ultimate, baseAngle, _radiusUltimate);
            PlaceSlot(attribute, DragonNodeKind.KinTower, baseAngle - _kinAngleOffset, _radiusKin);
            PlaceSlot(attribute, DragonNodeKind.KinArea, baseAngle + _kinAngleOffset, _radiusKin);

            BuildEdges(attribute, baseAngle);
        }

        _built = true;
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

    private void PlaceSlot(DragonType attribute, DragonNodeKind kind, float angle, float radius)
    {
        var key = (attribute, kind);

        if (!_slotViews.TryGetValue(key, out UI_DragonSkillNode view))
        {
            return;
        }

        Vector2 position = DragonSkillTreeLayout.PositionAt(angle, radius);
        _slotPositions[key] = position;
        view.GetComponent<RectTransform>().anchoredPosition = position;

        // 트리 중심이 (0,0)이라 좌표 자체가 "바깥쪽" 방향이다 - 노드가 라벨을 그 방향으로 밀어낸다.
        view.ApplyLayout(kind, position);
    }

    private void BuildEdges(DragonType attribute, float baseAngle)
    {
        // 속성 기준선은 트리 중심이 아니라 속성 이름표 바로 바깥에서 시작한다 -
        // 중심에서 그으면 선이 이름표를 관통해 글자를 읽기 어렵다.
        Vector2 spokeStart = DragonSkillTreeLayout.PositionAt(
            baseAngle, _radiusAttributeLabel + ATTRIBUTE_LABEL_CLEARANCE);
        CreateEdge(spokeStart, (attribute, DragonNodeKind.ActiveUnlock), attribute);

        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUnlock, DragonNodeKind.ActiveUp1);
        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUp1, DragonNodeKind.ActiveUp2);
        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUnlock, DragonNodeKind.PassiveUp1);
        CreateEdgeBetween(attribute, DragonNodeKind.PassiveUp1, DragonNodeKind.PassiveUp2);
        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUnlock, DragonNodeKind.Ultimate);
        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUnlock, DragonNodeKind.KinTower);
        CreateEdgeBetween(attribute, DragonNodeKind.ActiveUnlock, DragonNodeKind.KinArea);
    }

    private void CreateEdgeBetween(DragonType attribute, DragonNodeKind fromKind, DragonNodeKind toKind)
    {
        if (!_slotPositions.TryGetValue((attribute, fromKind), out Vector2 fromPosition))
        {
            return;
        }

        CreateEdge(fromPosition, (attribute, toKind), attribute);
    }

    private void CreateEdge(Vector2 from, (DragonType, DragonNodeKind) toSlot, DragonType attribute)
    {
        if (_edgePrefab == null || !_slotPositions.TryGetValue(toSlot, out Vector2 to))
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

        _edgeViews.Add(new EdgeView { Transform = edge, DependentSlot = toSlot, Attribute = attribute });
    }

    // 이 슬롯에서 다음에 구매할 노드. 전부 해금됐으면 최종 랭크를 돌려준다(완료 표시용).
    private DragonSkillNodeData ResolveCurrentNode((DragonType, DragonNodeKind) slot)
    {
        List<DragonSkillNodeData> ranks = _slotNodes[slot];

        foreach (DragonSkillNodeData node in ranks)
        {
            if (!_dragonTreeManager.IsUnlocked(node.NodeId))
            {
                return node;
            }
        }

        return ranks[ranks.Count - 1];
    }

    private int CountUnlockedRanks((DragonType, DragonNodeKind) slot)
    {
        int count = 0;

        foreach (DragonSkillNodeData node in _slotNodes[slot])
        {
            if (_dragonTreeManager.IsUnlocked(node.NodeId))
            {
                count++;
            }
        }

        return count;
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

        foreach (KeyValuePair<(DragonType, DragonNodeKind), UI_DragonSkillNode> entry in _slotViews)
        {
            DragonSkillNodeData node = ResolveCurrentNode(entry.Key);
            ProgressionNodeState state = _dragonTreeManager.GetNodeState(node);

            entry.Value.Bind(
                node,
                state,
                ColorForAttribute(node.Attribute),
                KinIconFor(node),
                BuildBadgeText(entry.Key, node, state),
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
            if (edge.Transform == null)
            {
                continue;
            }

            Image image = edge.Transform.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            // 랭크 하나라도 샀으면 그 슬롯으로 오는 선을 켠다 - 대표 노드는 "다음 랭크"라
            // 그것만 보면 1랭크를 산 슬롯의 선이 계속 꺼져 있다.
            bool lit = CountUnlockedRanks(edge.DependentSlot) > 0;
            image.color = DragonSkillNodePalette.EdgeColor(lit, ColorForAttribute(edge.Attribute), _edgeLockedColor);
        }
    }

    // 랭크가 여러 개인 슬롯은 진행도(Lv 1/2)를 우선 보여준다 - 잠금 사유보다 그쪽이 자주 필요하다.
    // 아직 한 랭크도 못 산 채 잠겨 있을 때만 잠금 사유를 띄운다.
    private string BuildBadgeText((DragonType, DragonNodeKind) slot, DragonSkillNodeData node, ProgressionNodeState state)
    {
        int unlockedRanks = CountUnlockedRanks(slot);

        if (node.MaxRank > 1 && unlockedRanks > 0)
        {
            return string.Format(StringTable.GetString(DragonLocKeys.RANK_BADGE), unlockedRanks, node.MaxRank);
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
        _selectedSlot = (node.Attribute, node.Kind);
        _detailsPanel?.Show(node);
    }

    // 랭크를 사고 나면 그 슬롯의 대표 노드가 다음 랭크로 바뀐다 - 상세 패널이 방금 산(이미 완료된)
    // 노드를 계속 보고 있으면 연속으로 다음 랭크를 살 수 없다.
    private void RefreshDetailsPanel()
    {
        if (_detailsPanel == null)
        {
            return;
        }

        // 사용자가 빈 공간을 눌러 패널을 닫았을 수도 있다 - 닫힌 패널에 Show를 부르면 되살아난다.
        if (!_detailsPanel.IsOpen)
        {
            _selectedSlot = null;
            return;
        }

        if (!_selectedSlot.HasValue || !_slotNodes.ContainsKey(_selectedSlot.Value))
        {
            _detailsPanel.Refresh();
            return;
        }

        _detailsPanel.Show(ResolveCurrentNode(_selectedSlot.Value));
    }

    // 새끼용 노드에 얹을 속성별 알 아이콘. 어미용 노드는 아이콘 없이 "속성 색으로 찬 원"으로
    // 구분되므로 null을 준다.
    private Sprite KinIconFor(DragonSkillNodeData node)
    {
        if (_babyDragonCatalog == null || !DragonSkillNodeStyleTable.For(node.Kind).IsKin)
        {
            return null;
        }

        return _babyDragonCatalog.TryResolve(node.Attribute, out BabyDragonData data) ? data.EggSprite : null;
    }

    private Color ColorForAttribute(DragonType attribute) => DragonAttributePalette.ColorOf(attribute);
}
