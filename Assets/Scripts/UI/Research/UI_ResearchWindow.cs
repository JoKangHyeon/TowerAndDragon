using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

    // 같은 티어(같은 행) 판정 여유. 같은 행이면 옆면끼리 잇는다.
    private const float SAME_ROW_EPSILON = 1f;

    // 베지어 제어점을 양끝에서 얼마나 밀지 - 두 노드 사이 거리에 대한 비율이다.
    // 블랙보드의 controlPointOffset(거리의 절반, 최소 50)과 같은 계산이다.
    private const float CURVE_CONTROL_RATIO = 0.5f;
    private const float MIN_CURVE_CONTROL = 50f;

    // 곡선 하나를 몇 조각으로 자를지. 16이면 눈으로 각이 보이지 않는다.
    private const int CURVE_SEGMENTS = 16;

    // 창을 열 때의 배율. 블랙보드처럼 1:1로 열고 나머지는 패닝·휠에 맡긴다.
    // 직렬화 필드로 두지 않는다: 필드를 추가한 시점의 기본값이 프리팹에 박혀 버려
    // 코드에서 값을 바꿔도 반영되지 않는 함정이 있다(실측 - 0.6으로 고쳐도 0.75가 그대로 떴다).
    private const float INITIAL_SCALE = 1f;

    // 블랙보드 --bg-color(#1e1e1e). 트리 뒤에 깔아 창 패널색과 분리한다.
    private static readonly Color CANVAS_COLOR = new Color(0.118f, 0.118f, 0.118f, 1f);

    // 블랙보드 .tier-label(2rem bold rgba(255,255,255,.1)) - 티어를 크게 옅게 깔아 둔다.
    private const float TIER_WATERMARK_FONT_SIZE = 64f;
    private static readonly Color TIER_WATERMARK_COLOR = new Color(1f, 1f, 1f, 0.10f);

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
    [Tooltip("갈래 헤더·티어 라벨 공용 글자표 프리팹. 반드시 루트(UI_ResearchLabel)를 물린다 - " +
        "안쪽 글자를 물리면 배경 같은 장식이 복제에서 빠진다.")]
    [SerializeField] private UI_ResearchLabel _labelPrefab;
    [SerializeField] private UI_ResearchDetailsPanel _detailsPanel;
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _researchPointsText;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _blockerButton;

    [Tooltip("창 안쪽 빈 곳 클릭. 상세 패널만 닫는다 - 창 자체는 ExitButton과 바깥 클릭으로 닫는다. " +
        "노드·버튼은 각자 클릭을 처리하므로 여기까지 올라오지 않는다.")]
    [SerializeField] private Button _windowBackgroundButton;

    // 간격은 "선이 지날 길"을 남기는 값이다. 좁히면 ㄱ자 연결선이 노드에 붙어
    // 어느 선이 어느 노드로 들어가는지 다시 안 보이게 된다(노드는 220×110).
    [Header("Grid (Docs/Sangwook/연구트리_시각화.html 기준)")]
    [Tooltip("갈래 열의 최소 폭. 실제 폭은 그 갈래에서 가장 붐비는 티어의 노드 수로 정해진다.")]
    [SerializeField] private float _minBranchWidth = 760f;
    [Tooltip("갈래 열 사이 여백.")]
    [SerializeField] private float _branchGap = 90f;
    [SerializeField] private float _rowHeight = 275f;
    [SerializeField] private float _nodeSpacing = 270f;


    [Header("연결선")]
    [SerializeField] private float _edgeThickness = 4f;

    [Tooltip("티어 행 사이 구분선. 블랙보드의 주기 구분선을 가로로 돌린 것.")]
    [SerializeField] private float _tierSeparatorThickness = 2f;
    [SerializeField] private Color _tierSeparatorColor = new Color(1f, 1f, 1f, 0.10f);
    [Header("우측 여백(UI 겹침 해소)")]
    [SerializeField] private float _columnSpacingWidth = 500f;
    [Tooltip("가장 왼쪽 갈래 열 중심에서 티어 라벨까지의 거리.")]
    [SerializeField] private float _tierLabelOffsetX = 300f;
    [Tooltip("티어 번호와 캡션을 행 중심에서 위아래로 벌리는 거리.")]
    [SerializeField] private float _tierLabelOffsetY = 16f;
    [Tooltip("가장 아래 티어 행 중심에서 갈래 헤더까지의 거리.")]
    [SerializeField] private float _branchHeaderOffsetY = 84f;
    [Tooltip("트리를 감싼 Content 여백. 스크롤 영역이 트리에 꼭 맞게 잡히도록 쓴다.")]
    [SerializeField] private Vector2 _contentPadding = new Vector2(80f, 80f);

    // 기획 블랙보드(Docs/연구트리_블랙보드.html)의 --branch-* 팔레트를 그대로 쓴다.
    [Header("Colors (ResearchBranch 선언 순: Tower, Production, Convenience)")]
    [SerializeField]
    private Color[] _branchColors =
    {
        new Color(1.000f, 0.420f, 0.420f), // Tower       - #ff6b6b
        new Color(0.306f, 0.804f, 0.769f), // Production  - #4ecdc4
        new Color(1.000f, 0.902f, 0.427f), // Convenience - #ffe66d
    };

    // 잠긴 선도 배경과 확실히 구분돼야 선행 관계가 읽힌다 - 블랙보드의 회색 점선(#666)에 맞춘 밝기다.
    [SerializeField] private Color _edgeLockedColor = new Color(0.40f, 0.40f, 0.40f, 1f);
    [SerializeField] private Color _tierLabelUnlockedColor = new Color(0.788f, 0.800f, 0.827f);
    [SerializeField] private Color _tierLabelLockedColor = new Color(0.482f, 0.506f, 0.557f, 0.6f);

    [Header("Keys")]
    [SerializeField] private InputActionReference _closeAction;

    private static readonly ResearchBranch[] BRANCHES_IN_ORDER =
        (ResearchBranch[])Enum.GetValues(typeof(ResearchBranch));

    private readonly Dictionary<string, ResearchNodeData> _nodeLookup = new();
    private readonly Dictionary<string, UI_ResearchNode> _nodeViews = new();
    private readonly Dictionary<string, Vector2> _nodePositions = new();
    private readonly Dictionary<int, List<UI_ResearchLabel>> _tierLabels = new();

    // 갈래 이름표는 트리를 만들 때 한 번만 놓이므로, 언어가 바뀌면 다시 칠할 수 있게 들고 있는다.
    private readonly Dictionary<ResearchBranch, UI_ResearchLabel> _branchHeaders = new();
    private readonly List<EdgeView> _edgeViews = new();

    // 곡선 점을 담아 넘기는 재사용 버퍼(선마다 새 리스트를 만들지 않는다).
    private readonly List<Vector2> _curvePointBuffer = new();

    // 갈래별 열 폭·중심. 갈래마다 "가장 붐비는 티어"에 맞춰 폭이 달라진다 -
    // 고정 폭을 쓰면 한 칸에 넣을 수 있는 노드 수가 그 폭에 갇힌다.
    private readonly List<float> _branchWidths = new();
    private readonly List<float> _branchCenters = new();

    // 노드 규격은 전부 프리팹에서 읽는다 - 코드에 크기를 박아 두면 프리팹 레이아웃을 바꿔도
    // 열 폭과 연결선이 따라오지 않는다(실측 - 카드를 220×130에서 100×190으로 바꿨더니
    // 선이 카드 밖 허공에서 끊겼다).
    private Vector2 _nodeSize;

    // 연결선이 붙는 자리(프리팹의 슬롯). 카드가 세로로 길어 카드 중심과 슬롯 중심이 어긋난다.
    private Vector2 _nodeEdgeOffset;
    private Vector2 _nodeEdgeHalfSize;

    private bool _built;
    private bool _isOpen;

    private struct EdgeView
    {
        public UI_ResearchEdge View;
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

        if (_windowBackgroundButton != null)
        {
            _windowBackgroundButton.onClick.AddListener(CloseDetailsPanel);
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

        // 구독 직후 현재 언어로 한 번 반영한다 - 창이 닫혀 있는 동안 바뀐 언어를 놓치지 않는다
        // (LocalizedText와 같은 방식).
        StringTable.OnLanguageChanged += ApplyLocalizedTexts;
        ApplyLocalizedTexts();
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

        StringTable.OnLanguageChanged -= ApplyLocalizedTexts;
    }

    /// <summary>
    /// 한 번 써 놓고 마는 글자들을 현재 언어로 다시 칠한다.
    ///
    /// 노드 카드는 갱신마다 <see cref="UI_ResearchNode.Bind"/>가 다시 읽어 언어를 저절로 따라가지만,
    /// 갈래 이름표·티어 라벨은 트리를 만들 때 한 번만 놓이므로(<see cref="_built"/> 가드)
    /// 처음 만들어진 시점의 언어에 굳는다. 이 메서드가 그 차이를 메운다.
    /// </summary>
    private void ApplyLocalizedTexts()
    {
        if (_headerText != null)
        {
            _headerText.text = StringTable.GetString(ResearchLocKeys.WINDOW_HEADER);
        }

        foreach (KeyValuePair<ResearchBranch, UI_ResearchLabel> entry in _branchHeaders)
        {
            if (entry.Value != null)
            {
                entry.Value.Text.text =
                    StringTable.GetString(ResearchLocKeys.BranchLocKey(entry.Key));
            }
        }

        foreach (KeyValuePair<int, List<UI_ResearchLabel>> entry in _tierLabels)
        {
            ApplyTierLabelTexts(entry.Key, entry.Value);
        }

        _detailsPanel?.ApplyLocalizedTexts();
    }

    // 0번은 티어 번호 워터마크, 1번은 해금 시점 캡션이다(PlaceTierLabels가 그 순서로 담는다).
    private void ApplyTierLabelTexts(int tier, List<UI_ResearchLabel> labels)
    {
        if (labels.Count > 0 && labels[0] != null)
        {
            labels[0].Text.text = string.Format(
                StringTable.GetString(ResearchLocKeys.TIER_LABEL), tier);
        }

        if (labels.Count > 1 && labels[1] != null)
        {
            labels[1].Text.text =
                StringTable.GetString(ResearchLocKeys.TierCaptionLocKey(tier));
        }
    }

    /// <summary>
    /// Esc는 <b>안쪽부터</b> 닫는다 - 상세 패널이 떠 있으면 그것만 닫고 창은 남긴다.
    ///
    /// 상세 패널에는 닫기 버튼이 없고 트리의 오른쪽을 덮는다. 그래서 한 번 열면 그 아래 노드를
    /// 고를 수 없었고, <b>맨 오른쪽 노드는 처음 클릭으로 열지 않으면 열 방법이 아예 없었다.</b>
    /// 그렇다고 창까지 함께 닫으면 노드를 하나 볼 때마다 창을 다시 열어야 한다.
    ///
    /// 창 닫기 관문(CanCloseExclusive)은 패널만 닫는 경로에서는 묻지 않는다 - 창은 그대로 남으므로
    /// 안내가 "이 창에서 무언가 하라"고 시키는 중이어도 그 유도가 깨지지 않는다.
    /// </summary>
    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        if (_detailsPanel != null && _detailsPanel.IsShown)
        {
            _detailsPanel.Clear();
            return;
        }

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

    // 창을 열 때마다 확대·이동 상태를 되돌린다.
    // (휠 확대는 UI_DragonSkillTreeZoom이 Content localScale을 직접 바꾸므로 여기서 되돌린다)
    //
    // 전체를 한 화면에 우겨넣어 봤더니 배율이 0.56까지 떨어져 카드 글씨를 못 읽었다(실측).
    // 블랙보드도 5000px 캔버스를 1:1로 열고 패닝하는 방식이므로 같게 맞춘다 - 배율 1로 열고
    // 트리의 왼쪽 아래(T1·타워)부터 보여준 뒤, 전체 조망은 휠 축소(하한 0.4)에 맡긴다.
    private void ResetView()
    {
        if (!WiringGuard.Require(_content, nameof(_content), this))
        {
            return;
        }

        ApplyInitialView();

        // 창을 연 첫 프레임에는 뷰포트 RectTransform이 아직 레이아웃 전이라 크기가 실제와 다르다
        // (실측: 1721 자리에 2036이 잡혀 배율이 0.63 대신 0.75로 나왔다).
        // CLAUDE.md의 "한 프레임 지연" 규칙대로 레이아웃이 끝난 뒤 한 번 더 잡는다.
        ApplyInitialViewNextFrameAsync().Forget();
    }

    private async UniTaskVoid ApplyInitialViewNextFrameAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());

        if (_isOpen)
        {
            ApplyInitialView();
        }
    }

    private void ApplyInitialView()
    {
        _content.localScale = new Vector3(INITIAL_SCALE, INITIAL_SCALE, 1f);
        _content.anchoredPosition = ResolveInitialPosition(INITIAL_SCALE);
    }

    // 뷰포트를 넘치는 만큼은 왼쪽 아래로 밀어 T1부터 보이게 한다(티어 1이 트리의 맨 아래다).
    private Vector2 ResolveInitialPosition(float scale)
    {
        var spacing = new Vector2(_columnSpacingWidth, 0f);

        if (!TryGetViewportSize(out Vector2 viewSize))
        {
            return spacing / 2f;
        }

        Vector2 scaled = _content.sizeDelta * scale;
        var overflow = new Vector2(
            Mathf.Max(0f, scaled.x - viewSize.x),
            Mathf.Max(0f, scaled.y - viewSize.y));

        return spacing / 2f + new Vector2(overflow.x, overflow.y) * 0.5f;
    }

    private bool TryGetViewportSize(out Vector2 size)
    {
        size = Vector2.zero;

        if (_content.parent is not RectTransform viewport)
        {
            return false;
        }

        Rect viewRect = viewport.rect;
        Vector2 contentSize = _content.sizeDelta;

        if (viewRect.width <= 0f || viewRect.height <= 0f ||
            contentSize.x <= 0f || contentSize.y <= 0f)
        {
            return false;
        }

        size = viewRect.size;
        return true;
    }

    // 창 안쪽 빈 곳 클릭 - 상세 패널만 닫는다.
    // 상세 패널은 닫기 버튼이 없고 트리의 오른쪽을 덮으므로, 그 아래 노드를 고르려면
    // 패널을 물릴 방법이 필요하다(Esc 말고 마우스로도).
    private void CloseDetailsPanel()
    {
        _detailsPanel?.Clear();
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;

        // 창이 통째로 꺼지므로 연출을 돌리지 않는다 - 연출 중에 오브젝트가 꺼지면
        // 패널이 줄어든 스케일 그대로 남는다.
        _detailsPanel?.Clear(animate: false);
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

        // 열 폭 계산이 노드 크기를 쓰므로 무엇보다 먼저 잡는다.
        // 루트 앵커가 점(0.5, 0.5)이라 프리팹 에셋에서도 rect가 그대로 나온다.
        _nodeSize = ((RectTransform)_nodePrefab.transform).rect.size;

        // (갈래, 티어) 칸별로 노드를 모은다. 트리 에셋의 순서를 그대로 유지해
        // 로드맵 표와 같은 좌우 순서로 놓이게 한다.
        var byCell = new Dictionary<ResearchBranch, Dictionary<int, List<ResearchNodeData>>>();

        foreach (ResearchNodeData node in _researchManager.Tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            // 중복 id는 ResearchManager.CacheNodes가 에러로 신고하고 **첫** 노드만 등록한다.
            // 여기서 마지막 노드로 덮어쓰면 UI가 매니저에 등록되지 않은 인스턴스를 바인딩해
            // GetNodeState가 영구 Invalid를 돌려준다(증상과 로그가 이어지지 않는다).
            // 같은 "첫 노드 유지" 규칙을 쓰고, 버린 노드는 그리지도 않는다.
            if (!_nodeLookup.TryAdd(node.NodeId, node))
            {
                continue;
            }

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

        ResolveBranchBands(byCell, branchCount, tierCount);

        PlaceTierLabels(tierCount);
        PlaceTierSeparators(tierCount);

        for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            ResearchBranch branch = BRANCHES_IN_ORDER[branchIndex];

            PlaceBranchHeader(branchIndex, tierCount, branch);

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
                    PlaceNode(cellNodes[i], branchIndex, tierIndex, tierCount, i, cellNodes.Count);
                }
            }
        }

        // 연결선을 만들기 전에 트리를 Content 중앙으로 맞춘다.
        // (선은 노드 좌표에서 유도되므로 좌표 보정이 끝난 뒤에 만들어야 한다)
        CenterTreeInContent();

        ResolveNodeEdgeAnchor();
        BuildEdges();
        PlaceCanvasBackground();

        _built = true;

        // 방금 만든 이름표·티어 라벨에 글자를 채운다.
        ApplyLocalizedTexts();
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

    // 갈래마다 "가장 붐비는 티어"에 맞춰 열 폭을 정하고 왼쪽부터 이어 붙인다.
    // 이렇게 해야 한 칸에 넣을 수 있는 노드 수에 상한이 생기지 않는다 -
    // 고정 폭이던 동안은 3개를 넘기면 옆 갈래를 침범했다.
    private void ResolveBranchBands(
        Dictionary<ResearchBranch, Dictionary<int, List<ResearchNodeData>>> byCell,
        int branchCount,
        int tierCount)
    {
        _branchWidths.Clear();

        for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            int busiest = 0;

            if (byCell.TryGetValue(BRANCHES_IN_ORDER[branchIndex],
                out Dictionary<int, List<ResearchNodeData>> byTier))
            {
                for (int tierIndex = 0; tierIndex < tierCount; tierIndex++)
                {
                    int tier = tierIndex + ResearchTierRules.FIRST_TIER;

                    if (byTier.TryGetValue(tier, out List<ResearchNodeData> cellNodes))
                    {
                        busiest = Mathf.Max(busiest, cellNodes.Count);
                    }
                }
            }

            _branchWidths.Add(ResearchTreeLayout.BranchWidth(
                busiest, _nodeSize.x, _nodeSpacing, _minBranchWidth));
        }

        ResearchTreeLayout.ResolveBranchCenters(_branchWidths, _branchGap, _branchCenters);
    }

    private void PlaceNode(
        ResearchNodeData node,
        int branchIndex,
        int tierIndex,
        int tierCount,
        int indexInCell,
        int countInCell)
    {
        Vector2 position = ResearchTreeLayout.NodePosition(
            _branchCenters[branchIndex],
            tierIndex, tierCount, _rowHeight,
            indexInCell, countInCell, _nodeSpacing);

        _nodePositions[node.NodeId] = position;

        UI_ResearchNode view = Instantiate(_nodePrefab, _content);
        view.GetComponent<RectTransform>().anchoredPosition = position;
        _nodeViews[node.NodeId] = view;
    }

    private void PlaceBranchHeader(int branchIndex, int tierCount, ResearchBranch branch)
    {
        UI_ResearchLabel header = CreateLabel();

        if (header == null)
        {
            return;
        }

        _branchHeaders[branch] = header;

        // 글자는 ApplyLocalizedTexts가 채운다 - 언어가 바뀔 때 다시 칠하는 곳과 한 군데로 모은다.
        header.Text.color = ColorForBranch(branch);
        header.SetBackgroundVisible(true);

        // 갈래 이름표는 트리 밑동에 붙는다 - 트리가 아래에서 위로 자라므로
        // 갈래 이름 → T1 → ... 순으로 읽히는 자리가 여기다.
        header.Rect.anchoredPosition = new Vector2(
            _branchCenters[branchIndex],
            ResearchTreeLayout.BottomRowCenterY(tierCount, _rowHeight) - _branchHeaderOffsetY);
    }

    // 티어 행마다 번호 라벨과 해금 시점 캡션을 왼쪽에 세운다.
    // 두 줄을 한 텍스트에 넣지 않고 라벨 2개로 나눠, 줄바꿈 문자를 코드에 두지 않는다.
    private void PlaceTierLabels(int tierCount)
    {
        // 첫 갈래의 왼쪽 변에서 더 왼쪽으로 - 노드에 가려지지 않게 폭까지 감안한다.
        float labelX = TreeLeftEdge() - _tierLabelOffsetX;

        for (int tierIndex = 0; tierIndex < tierCount; tierIndex++)
        {
            int tier = tierIndex + ResearchTierRules.FIRST_TIER;
            float rowY = ResearchTreeLayout.RowCenterY(tierIndex, tierCount, _rowHeight);

            UI_ResearchLabel numberLabel = CreateLabel();
            UI_ResearchLabel captionLabel = CreateLabel();

            if (numberLabel == null || captionLabel == null)
            {
                return;
            }

            // 티어 라벨은 배경 없이 글자만 깐다 - 배경판을 두면 워터마크가 배경 위의
            // 이름표처럼 보여 "뒤에 깔린 큰 글자"라는 성격이 사라진다.
            numberLabel.SetBackgroundVisible(false);
            captionLabel.SetBackgroundVisible(false);

            // 티어 번호는 블랙보드의 .tier-label처럼 크고 옅게 깐다(워터마크).
            // 캡션만 평소 크기로 그 아래에 둔다. 글자는 ApplyLocalizedTexts가 채운다.
            numberLabel.Text.fontSize = TIER_WATERMARK_FONT_SIZE;
            numberLabel.Text.fontStyle = FontStyles.Bold;
            numberLabel.Rect.sizeDelta = new Vector2(
                numberLabel.Rect.sizeDelta.x, TIER_WATERMARK_FONT_SIZE * 1.2f);
            numberLabel.Rect.anchoredPosition =
                new Vector2(labelX, rowY + _tierLabelOffsetY);

            captionLabel.Rect.anchoredPosition =
                new Vector2(labelX, rowY - TIER_WATERMARK_FONT_SIZE * 0.55f - _tierLabelOffsetY);

            _tierLabels[tier] = new List<UI_ResearchLabel> { numberLabel, captionLabel };
        }
    }

    private float TreeLeftEdge() =>
        _branchCenters.Count > 0 ? _branchCenters[0] - _branchWidths[0] * 0.5f : 0f;

    private float TreeRightEdge() =>
        _branchCenters.Count > 0
            ? _branchCenters[_branchCenters.Count - 1] + _branchWidths[_branchWidths.Count - 1] * 0.5f
            : 0f;

    private UI_ResearchLabel CreateLabel()
    {
        return _labelPrefab != null ? Instantiate(_labelPrefab, _content) : null;
    }

    // 블랙보드의 캔버스 색(#1e1e1e)을 트리 뒤에 깐다. 창 패널색 위에 그대로 두면
    // 카드와 배경의 대비가 블랙보드와 달라져 같은 팔레트로도 다르게 보인다.
    // Content 크기가 확정된 뒤(CenterTreeInContent 이후) 불러야 크기를 맞출 수 있다.
    private void PlaceCanvasBackground()
    {
        var backgroundObject = new GameObject(
            "CanvasBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)backgroundObject.transform;
        rect.SetParent(_content, false);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling(); // 연결선·노드보다 뒤로

        var image = backgroundObject.GetComponent<Image>();
        image.color = CANVAS_COLOR;
        image.raycastTarget = false;
    }

    // 티어 행 사이에 옅은 구분선을 깐다. 블랙보드의 주기 구분선을 가로로 돌린 것으로,
    // 어느 노드가 같은 티어인지 선 없이도 읽히게 하는 역할이다.
    // 연결선 프리팹을 재사용하지만 _edgeViews에 넣지 않으므로 상태에 따라 색이 변하지 않는다.
    private void PlaceTierSeparators(int tierCount)
    {
        if (_edgePrefab == null)
        {
            return;
        }

        float left = TreeLeftEdge();
        float right = TreeRightEdge();

        // 행과 행 사이에만 긋는다 - 트리 바깥까지 그으면 갈래 이름표와 겹친다.
        for (int tierIndex = 1; tierIndex < tierCount; tierIndex++)
        {
            float y = ResearchTreeLayout.RowBoundaryY(tierIndex, tierCount, _rowHeight);

            RectTransform line = Instantiate(_edgePrefab, _content);
            line.SetAsFirstSibling();
            line.localRotation = Quaternion.identity;
            line.sizeDelta = new Vector2(right - left, _tierSeparatorThickness);
            line.anchoredPosition = new Vector2((left + right) * 0.5f, y);

            var image = line.GetComponent<Image>();
            if (image != null)
            {
                // 블랙보드의 `border-left: 1px dashed` 를 그대로 옮긴다. 실선으로 두면
                // 연결선보다 눈에 띄어 트리가 표처럼 보인다.
                image.sprite = ResearchTreeSprites.DashSprite;
                image.type = Image.Type.Tiled;
                image.color = _tierSeparatorColor;
            }
        }
    }

    // 연결선이 붙는 자리를 프리팹에서 잰다. 노드는 전부 같은 프리팹이라 하나만 재면 된다.
    //
    // 슬롯의 자리는 프리팹에 직렬화된 값이 아니라 노드 루트의 VerticalLayoutGroup이 정하므로,
    // 인스턴스를 하나 잡아 레이아웃을 강제로 돌린 뒤에 읽는다.
    private void ResolveNodeEdgeAnchor()
    {
        // 카드 전체를 기본값으로 둔다 - 슬롯을 못 찾아도 선이 노드 중심에서 튀어나오진 않는다.
        _nodeEdgeOffset = Vector2.zero;
        _nodeEdgeHalfSize = _nodeSize * 0.5f;

        foreach (UI_ResearchNode view in _nodeViews.Values)
        {
            if (view == null)
            {
                continue;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.transform);

            Rect anchor = view.ResolveEdgeAnchorRect();
            _nodeEdgeOffset = anchor.center;
            _nodeEdgeHalfSize = anchor.size * 0.5f;
            return;
        }
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

                // 카드 중심이 아니라 슬롯 중심끼리 잇는다.
                CreateEdge(
                    from + _nodeEdgeOffset, to + _nodeEdgeOffset,
                    prerequisite.NodeId, node.NodeId, node.Branch);
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
        BuildCurvePoints(from, to);

        var curveObject = new GameObject("EdgeCurve", typeof(RectTransform), typeof(CanvasRenderer), typeof(UI_ResearchEdge));
        var rect = (RectTransform)curveObject.transform;
        rect.SetParent(_content, false);

        // 점을 Content 좌표로 바로 넘기므로 이 RectTransform은 Content 중심에 크기 0으로 둔다.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.SetAsFirstSibling(); // 노드 아래로 - 선이 클릭을 가로채거나 위에 그려지지 않게 한다

        var edge = curveObject.GetComponent<UI_ResearchEdge>();
        edge.SetCurve(
            _curvePointBuffer, _edgeThickness,
            ResearchTreeSprites.DashTexture, ResearchTreeSprites.DashPeriod);

        _edgeViews.Add(new EdgeView
        {
            View = edge,
            PrerequisiteNodeId = prerequisiteNodeId,
            DependentNodeId = dependentNodeId,
            Branch = branch,
        });
    }

    // 3차 베지어를 잘라 점 목록으로 만든다. 블랙보드가 SVG로 그리던 곡선과 같은 식이며,
    // 그쪽은 가로 흐름이라 제어점을 x로 밀지만 이 창은 세로 흐름이라 y로 민다.
    private void BuildCurvePoints(Vector2 from, Vector2 to)
    {
        _curvePointBuffer.Clear();

        Vector2 start;
        Vector2 end;
        Vector2 startControl;
        Vector2 endControl;

        if (Mathf.Abs(from.y - to.y) <= SAME_ROW_EPSILON)
        {
            // 같은 티어끼리는 옆면에서 나가 옆면으로 들어간다.
            float direction = Mathf.Sign(to.x - from.x);
            start = new Vector2(from.x + direction * _nodeEdgeHalfSize.x, from.y);
            end = new Vector2(to.x - direction * _nodeEdgeHalfSize.x, to.y);

            float reach = Mathf.Max(Mathf.Abs(end.x - start.x) * CURVE_CONTROL_RATIO, MIN_CURVE_CONTROL);
            startControl = start + new Vector2(direction * reach, 0f);
            endControl = end - new Vector2(direction * reach, 0f);
        }
        else
        {
            // 다음 티어로 넘어가는 선은 선행 노드의 "다음 티어 쪽 변"에서 나가 반대 변으로 들어간다.
            // 방향을 부호로 유도하므로 티어가 위로 쌓이든 아래로 쌓이든 선이 카드를 뚫지 않는다.
            float direction = Mathf.Sign(to.y - from.y);
            start = new Vector2(from.x, from.y + direction * _nodeEdgeHalfSize.y);
            end = new Vector2(to.x, to.y - direction * _nodeEdgeHalfSize.y);

            float reach = Mathf.Max(Mathf.Abs(end.y - start.y) * CURVE_CONTROL_RATIO, MIN_CURVE_CONTROL);
            startControl = start + new Vector2(0f, direction * reach);
            endControl = end - new Vector2(0f, direction * reach);
        }

        for (int i = 0; i <= CURVE_SEGMENTS; i++)
        {
            float t = (float)i / CURVE_SEGMENTS;
            _curvePointBuffer.Add(EvaluateCubic(start, startControl, endControl, end, t));
        }
    }

    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float inverse = 1f - t;
        float a = inverse * inverse * inverse;
        float b = 3f * inverse * inverse * t;
        float c = 3f * inverse * t * t;
        float d = t * t * t;

        return a * p0 + b * p1 + c * p2 + d * p3;
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

    // 대상 노드까지 완료된 선은 갈래 색 실선으로 차오르고, 아직인 선은 점선 바탕만 남는다.
    // 그 바탕도 선행이 끝났으면 은은하게 물들여, 다음에 갈 수 있는 길이 어디인지 보이게 한다.
    private void RefreshEdgeViews()
    {
        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        foreach (EdgeView edge in _edgeViews)
        {
            if (edge.View == null)
            {
                continue;
            }

            Color branchColor = ColorForBranch(edge.Branch);
            Color backgroundColor = _researchManager.IsCompleted(edge.PrerequisiteNodeId)
                ? Color.Lerp(_edgeLockedColor, branchColor, EDGE_REACHABLE_TINT_RATIO)
                : _edgeLockedColor;

            edge.View.Bind(
                _researchManager.IsCompleted(edge.DependentNodeId), branchColor, backgroundColor);
        }
    }

    private void RefreshTierLabels()
    {
        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        int currentCycle = _researchManager.CurrentCycleNumber;

        foreach (KeyValuePair<int, List<UI_ResearchLabel>> entry in _tierLabels)
        {
            bool unlocked = ResearchTierRules.IsTierUnlocked(entry.Key, currentCycle);
            Color color = unlocked ? _tierLabelUnlockedColor : _tierLabelLockedColor;

            for (int i = 0; i < entry.Value.Count; i++)
            {
                UI_ResearchLabel label = entry.Value[i];

                if (label == null)
                {
                    continue;
                }

                // 0번은 대형 워터마크(티어 번호)라 항상 옅게 두고, 잠긴 티어만 더 옅게 한다.
                // 여기까지 해금 색을 입히면 블랙보드의 "배경에 깔린 큰 글자" 느낌이 사라진다.
                if (i == 0)
                {
                    Color watermark = TIER_WATERMARK_COLOR;
                    watermark.a *= unlocked ? 1f : 0.5f;
                    label.Text.color = watermark;
                    continue;
                }

                label.Text.color = color;
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
