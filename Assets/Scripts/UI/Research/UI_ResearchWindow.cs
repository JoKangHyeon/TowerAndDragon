using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 연구 창 - ResearchTree.asset의 노드 전체(3갈래 × 5티어)를 한 화면에 격자로 배치한다.
// Docs/Sangwook/연구트리_시각화.html이 기준이지만, 그쪽의 "갈래 탭 전환"과 달리
// 세 갈래를 열로 나란히 세워 전체 연구를 한 번에 보여준다.
// blocker 루트(전체화면 dim Image)에 부착되어 바깥 클릭 닫기와
// IExclusiveMode(UIManager.OpenExclusive) 조정을 겸한다 - UI_DragonSkillWindow와 같은 구조.
// 연구 가능 여부·완료 규칙은 여기서 재구현하지 않는다 - 전부 ResearchManager에 위임.
public class UI_ResearchWindow : MonoBehaviour, IExclusiveMode
{
    private const float EDGE_REACHABLE_TINT_RATIO = 0.55f;

    [Header("Dependencies")]
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private UIManager _uiManager;

    [Tooltip("주기 변경 시 티어 잠금 표시를 갱신하기 위한 참조. 없어도 창은 동작한다.")]
    [WiringOptional]
    [SerializeField] private WaveCycleProgression _cycleProgression;

    [Header("Layout Targets")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private UI_ResearchNode _nodePrefab;
    [SerializeField] private RectTransform _edgePrefab;
    [Tooltip("갈래 헤더·티어 라벨 공용 텍스트 프리팹.")]
    [SerializeField] private TextMeshProUGUI _labelPrefab;
    [SerializeField] private UI_ResearchDetailsPanel _detailsPanel;
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _researchPointsText;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _blockerButton;

    [Header("Grid (Docs/Sangwook/연구트리_시각화.html 기준)")]
    [SerializeField] private float _columnWidth = 500f;
    [SerializeField] private float _rowHeight = 104f;
    [SerializeField] private float _nodeSpacing = 166f;
    [Header("우측 여백(UI 겹침 해소)")]
    [SerializeField] private float _columnSpacingWidth = 500f;
    [Tooltip("가장 왼쪽 갈래 열 중심에서 티어 라벨까지의 거리.")]
    [SerializeField] private float _tierLabelOffsetX = 300f;
    [Tooltip("티어 번호와 캡션을 행 중심에서 위아래로 벌리는 거리.")]
    [SerializeField] private float _tierLabelOffsetY = 16f;
    [Tooltip("가장 위 티어 행 중심에서 갈래 헤더까지의 거리.")]
    [SerializeField] private float _branchHeaderOffsetY = 84f;
    [Tooltip("트리를 감싼 Content 여백. 스크롤 영역이 트리에 꼭 맞게 잡히도록 쓴다.")]
    [SerializeField] private Vector2 _contentPadding = new Vector2(80f, 80f);

    [Header("Colors (ResearchBranch 선언 순: Tower, Production, Convenience)")]
    [SerializeField]
    private Color[] _branchColors =
    {
        new Color(0.302f, 0.882f, 0.816f), // Tower - 시각화 HTML의 --line-active
        new Color(0.498f, 0.820f, 0.310f), // Production
        new Color(0.902f, 0.698f, 0.353f), // Convenience - 시각화 HTML의 --gold
    };

    [SerializeField] private Color _edgeLockedColor = new Color(0.169f, 0.184f, 0.220f, 0.8f);
    [SerializeField] private Color _tierLabelUnlockedColor = new Color(0.788f, 0.800f, 0.827f);
    [SerializeField] private Color _tierLabelLockedColor = new Color(0.482f, 0.506f, 0.557f, 0.6f);

    [Header("Keys")]
    [SerializeField] private InputActionReference _closeAction;

    private static readonly ResearchBranch[] BRANCHES_IN_ORDER =
        (ResearchBranch[])Enum.GetValues(typeof(ResearchBranch));

    private readonly Dictionary<string, ResearchNodeData> _nodeLookup = new();
    private readonly Dictionary<string, UI_ResearchNode> _nodeViews = new();
    private readonly Dictionary<string, Vector2> _nodePositions = new();
    private readonly Dictionary<int, List<TextMeshProUGUI>> _tierLabels = new();
    private readonly List<EdgeView> _edgeViews = new();

    private bool _built;
    private bool _isOpen;

    private struct EdgeView
    {
        public RectTransform Transform;
        public string PrerequisiteNodeId;
        public string DependentNodeId;
        public ResearchBranch Branch;
    }

    private void Awake()
    {
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
            _headerText.text = StringTable.GetString(ResearchLocKeys.WINDOW_HEADER);
        }

        if (_detailsPanel != null)
        {
            _detailsPanel.Construct(_researchManager, _resourceManager, RefreshAll);
        }
    }

    private void OnEnable()
    {
        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleNodeCompleted);
            _researchManager.ResearchPointsChanged.AddListener(HandleResearchPointsChanged);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleCycleProgressed);
            _cycleManager.OnNightEnd.AddListener(HandleCycleProgressed);
        }

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleStarted.AddListener(HandleCycleProgressed);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleNodeCompleted);
            _researchManager.ResearchPointsChanged.RemoveListener(HandleResearchPointsChanged);
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

        if (_cycleProgression != null)
        {
            _cycleProgression.CycleStarted.RemoveListener(HandleCycleProgressed);
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        if (_uiManager != null && !_uiManager.CanCloseExclusive(this))
        {
            return;
        }

        Close();
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        gameObject.SetActive(true);
        BuildTreeIfNeeded();
        ResetView();
        RefreshAll();
    }

    // 창을 열 때마다 확대·이동 상태를 되돌려 항상 트리 전체가 보이게 한다.
    // (휠 확대는 UI_DragonSkillTreeZoom이 Content localScale을 직접 바꾸므로 여기서 되돌린다)
    private void ResetView()
    {
        if (!WiringGuard.Require(_content, nameof(_content), this))
        {
            return;
        }

        _content.localScale = Vector3.one;
        var spancing = new Vector2(_columnSpacingWidth, 0f);
        _content.anchoredPosition = spancing / 2f;
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

    // 연구소 창·HUD의 연구 버튼이 호출하는 진입점.
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
        if (_built || _researchManager == null || _researchManager.Tree == null ||
            _content == null || _nodePrefab == null)
        {
            return;
        }

        // (갈래, 티어) 칸별로 노드를 모은다. 트리 에셋의 순서를 그대로 유지해
        // 로드맵 표와 같은 좌우 순서로 놓이게 한다.
        var byCell = new Dictionary<ResearchBranch, Dictionary<int, List<ResearchNodeData>>>();

        foreach (ResearchNodeData node in _researchManager.Tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            _nodeLookup[node.NodeId] = node;

            if (!byCell.TryGetValue(node.Branch, out Dictionary<int, List<ResearchNodeData>> byTier))
            {
                byTier = new Dictionary<int, List<ResearchNodeData>>();
                byCell[node.Branch] = byTier;
            }

            if (!byTier.TryGetValue(node.Tier, out List<ResearchNodeData> cellNodes))
            {
                cellNodes = new List<ResearchNodeData>();
                byTier[node.Tier] = cellNodes;
            }

            cellNodes.Add(node);
        }

        int branchCount = BRANCHES_IN_ORDER.Length;
        int tierCount = ResearchTierRules.MaxTier;

        PlaceTierLabels(branchCount, tierCount);

        for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            ResearchBranch branch = BRANCHES_IN_ORDER[branchIndex];

            PlaceBranchHeader(branchIndex, branchCount, tierCount, branch);

            if (!byCell.TryGetValue(branch, out Dictionary<int, List<ResearchNodeData>> byTier))
            {
                continue;
            }

            for (int tierIndex = 0; tierIndex < tierCount; tierIndex++)
            {
                int tier = tierIndex + ResearchTierRules.FIRST_TIER;

                if (!byTier.TryGetValue(tier, out List<ResearchNodeData> cellNodes))
                {
                    continue;
                }

                for (int i = 0; i < cellNodes.Count; i++)
                {
                    PlaceNode(cellNodes[i], branchIndex, branchCount, tierIndex, tierCount, i, cellNodes.Count);
                }
            }
        }

        // 연결선을 만들기 전에 트리를 Content 중앙으로 맞춘다.
        // (선은 노드 좌표에서 유도되므로 좌표 보정이 끝난 뒤에 만들어야 한다)
        CenterTreeInContent();

        BuildEdges();

        _built = true;
    }

    // 노드·라벨을 감싸는 최소 사각형을 구해 트리를 Content 원점에 맞추고,
    // Content 크기를 그 사각형에 맞춘다. ScrollRect가 트리를 임의로 밀어내지 않게 하는 목적이다.
    private void CenterTreeInContent()
    {
        if (_content == null || _content.childCount == 0)
        {
            return;
        }

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var spancing = new Vector2(_columnSpacingWidth, 0f);

        foreach (RectTransform child in _content)
        {
            Vector2 half = child.rect.size * 0.5f;
            min = Vector2.Min(min, child.anchoredPosition - half);
            max = Vector2.Max(max, child.anchoredPosition + half);
        }

        Vector2 center = (min + max + spancing) * 0.5f;

        foreach (RectTransform child in _content)
        {
            child.anchoredPosition -= center;
        }

        // 연결선이 참조할 좌표도 같이 보정한다.
        foreach (string nodeId in new List<string>(_nodePositions.Keys))
        {
            _nodePositions[nodeId] -= center;
        }
        max.x += _columnSpacingWidth;

        _content.anchorMin = new Vector2(0.5f, 0.5f);
        _content.anchorMax = new Vector2(0.5f, 0.5f);
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.sizeDelta = (max - min) + _contentPadding;

        Canvas.ForceUpdateCanvases();
        _content.anchoredPosition = spancing/2f;
    }

    private void PlaceNode(
        ResearchNodeData node,
        int branchIndex,
        int branchCount,
        int tierIndex,
        int tierCount,
        int indexInCell,
        int countInCell)
    {
        Vector2 position = ResearchTreeLayout.NodePosition(
            branchIndex, branchCount, _columnWidth,
            tierIndex, tierCount, _rowHeight,
            indexInCell, countInCell, _nodeSpacing);

        _nodePositions[node.NodeId] = position;

        UI_ResearchNode view = Instantiate(_nodePrefab, _content);
        view.GetComponent<RectTransform>().anchoredPosition = position;
        _nodeViews[node.NodeId] = view;
    }

    private void PlaceBranchHeader(int branchIndex, int branchCount, int tierCount, ResearchBranch branch)
    {
        TextMeshProUGUI header = CreateLabel();

        if (header == null)
        {
            return;
        }

        header.text = StringTable.GetString(ResearchLocKeys.BranchLocKey(branch));
        header.color = ColorForBranch(branch);
        header.rectTransform.anchoredPosition = new Vector2(
            ResearchTreeLayout.ColumnCenterX(branchIndex, branchCount, _columnWidth),
            ResearchTreeLayout.RowCenterY(0, tierCount, _rowHeight) + _branchHeaderOffsetY);
    }

    // 티어 행마다 번호 라벨과 해금 시점 캡션을 왼쪽에 세운다.
    // 두 줄을 한 텍스트에 넣지 않고 라벨 2개로 나눠, 줄바꿈 문자를 코드에 두지 않는다.
    private void PlaceTierLabels(int branchCount, int tierCount)
    {
        float labelX =
            ResearchTreeLayout.ColumnCenterX(0, branchCount, _columnWidth) - _tierLabelOffsetX;

        for (int tierIndex = 0; tierIndex < tierCount; tierIndex++)
        {
            int tier = tierIndex + ResearchTierRules.FIRST_TIER;
            float rowY = ResearchTreeLayout.RowCenterY(tierIndex, tierCount, _rowHeight);

            TextMeshProUGUI numberLabel = CreateLabel();
            TextMeshProUGUI captionLabel = CreateLabel();

            if (numberLabel == null || captionLabel == null)
            {
                return;
            }

            numberLabel.text = string.Format(
                StringTable.GetString(ResearchLocKeys.TIER_LABEL), tier);
            numberLabel.rectTransform.anchoredPosition =
                new Vector2(labelX, rowY + _tierLabelOffsetY);

            captionLabel.text =
                StringTable.GetString(ResearchLocKeys.TierCaptionLocKey(tier));
            captionLabel.rectTransform.anchoredPosition =
                new Vector2(labelX, rowY - _tierLabelOffsetY);

            _tierLabels[tier] = new List<TextMeshProUGUI> { numberLabel, captionLabel };
        }
    }

    private TextMeshProUGUI CreateLabel()
    {
        return _labelPrefab != null ? Instantiate(_labelPrefab, _content) : null;
    }

    // 연결선은 노드의 Prerequisites에서 그대로 유도한다(용 스킬트리처럼 위상을 코드에 박지 않는다).
    private void BuildEdges()
    {
        foreach (KeyValuePair<string, ResearchNodeData> entry in _nodeLookup)
        {
            ResearchNodeData node = entry.Value;

            if (!_nodePositions.TryGetValue(node.NodeId, out Vector2 to))
            {
                continue;
            }

            foreach (ResearchNodeData prerequisite in node.Prerequisites)
            {
                if (prerequisite == null ||
                    !_nodePositions.TryGetValue(prerequisite.NodeId, out Vector2 from))
                {
                    continue;
                }

                CreateEdge(from, to, prerequisite.NodeId, node.NodeId, node.Branch);
            }
        }
    }

    private void CreateEdge(
        Vector2 from,
        Vector2 to,
        string prerequisiteNodeId,
        string dependentNodeId,
        ResearchBranch branch)
    {
        if (!WiringGuard.Require(_edgePrefab, nameof(_edgePrefab), this))
        {
            return;
        }

        RectTransform edge = Instantiate(_edgePrefab, _content);
        edge.SetAsFirstSibling(); // 노드 아래로 - 선이 클릭을 가로채거나 위에 그려지지 않게 한다
        edge.anchoredPosition = from;

        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        edge.localRotation = Quaternion.Euler(0f, 0f, -angle);

        Vector2 size = edge.sizeDelta;
        size.y = delta.magnitude;
        edge.sizeDelta = size;

        _edgeViews.Add(new EdgeView
        {
            Transform = edge,
            PrerequisiteNodeId = prerequisiteNodeId,
            DependentNodeId = dependentNodeId,
            Branch = branch,
        });
    }

    private void HandleNodeCompleted(ResearchNodeData node) => RefreshAll();
    private void HandleResearchPointsChanged(int researchPoints) => RefreshAll();
    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();
    private void HandleCycleProgressed(int value) => RefreshAll();

    private void RefreshAll()
    {
        RefreshResearchPoints();
        RefreshNodeViews();
        RefreshEdgeViews();
        RefreshTierLabels();
        _detailsPanel?.Refresh();
    }

    private void RefreshResearchPoints()
    {
        if (_researchPointsText == null || _researchManager == null)
        {
            return;
        }

        _researchPointsText.text = string.Format(
            StringTable.GetString(ResearchLocKeys.RP_AMOUNT),
            _researchManager.ResearchPoints);
    }

    private void RefreshNodeViews()
    {
        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        foreach (KeyValuePair<string, UI_ResearchNode> entry in _nodeViews)
        {
            if (!_nodeLookup.TryGetValue(entry.Key, out ResearchNodeData node))
            {
                continue;
            }

            ResearchNodeState state = _researchManager.GetNodeState(node);
            entry.Value.Bind(node, state, ColorForBranch(node.Branch), HandleNodeClicked);
        }
    }

    // 선행이 끝난 선은 은은하게, 대상 노드까지 완료된 선은 갈래 색으로 꽉 채워
    // 어디까지 진행됐는지 한눈에 보이게 한다.
    private void RefreshEdgeViews()
    {
        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        foreach (EdgeView edge in _edgeViews)
        {
            if (edge.Transform == null)
            {
                continue;
            }

            var image = edge.Transform.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            Color branchColor = ColorForBranch(edge.Branch);

            if (_researchManager.IsCompleted(edge.DependentNodeId))
            {
                image.color = branchColor;
            }
            else if (_researchManager.IsCompleted(edge.PrerequisiteNodeId))
            {
                image.color = Color.Lerp(_edgeLockedColor, branchColor, EDGE_REACHABLE_TINT_RATIO);
            }
            else
            {
                image.color = _edgeLockedColor;
            }
        }
    }

    private void RefreshTierLabels()
    {
        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        int currentCycle = _researchManager.CurrentCycleNumber;

        foreach (KeyValuePair<int, List<TextMeshProUGUI>> entry in _tierLabels)
        {
            bool unlocked = ResearchTierRules.IsTierUnlocked(entry.Key, currentCycle);
            Color color = unlocked ? _tierLabelUnlockedColor : _tierLabelLockedColor;

            foreach (TextMeshProUGUI label in entry.Value)
            {
                if (label != null)
                {
                    label.color = color;
                }
            }
        }
    }

    private void HandleNodeClicked(ResearchNodeData node)
    {
        _detailsPanel?.Show(node);
    }

    private Color ColorForBranch(ResearchBranch branch)
    {
        int index = Array.IndexOf(BRANCHES_IN_ORDER, branch);
        return index >= 0 && index < _branchColors.Length ? _branchColors[index] : Color.white;
    }
}
