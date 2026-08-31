using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections.Generic;

public class MouseSelectController : MonoBehaviour
{
    private const float PULSE_HALF_RANGE = 0.5f;
    private const float FULL_TURN_RADIANS = Mathf.PI * 2f;

    // 맥동 그룹이 없다는 뜻 - HighlightCellGroups의 공개 경로(인구 배치 모드 등)가 쓴다.
    private const int PULSING_GROUP_NONE = 0;

    [FormerlySerializedAs("_spriteRenderer")]
    [SerializeField]
    private SpriteRenderer _selectionHighlightRenderer;

    [SerializeField]
    private SpriteRenderer _occupiedHighlightRenderer;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private SpriteRenderer _ghostRenderer;

    [SerializeField]
    private float _ghostAlpha = 0.5f;

    [SerializeField]
    private Color _ghostBlockedTint = new Color(1f, 0.4f, 0.4f);

    [SerializeField]
    private Color _selectionHighlightColor = Color.yellow;

    [SerializeField]
    private Color _occupiedOverlayColor = new Color(1f, 0f, 0f, 0.35f);

    [Tooltip("튜토리얼이 \"여기에 지어라\"로 짚어주는 칸의 색. 배치 가능(초록)·불가(빨강)·선택(노랑) 어디와도 " +
        "겹치지 않아야 안내로 읽힌다. 지형에 가장 덜 묻히는 시안 계열을 쓴다.")]
    [SerializeField]
    private Color _placementGuideColor = new Color(0f, 0.9f, 1f);

    [Tooltip("안내 칸이 맥동하는 속도(초당 왕복 횟수).")]
    [SerializeField]
    private float _guidePulseSpeed = 1.5f;

    [Tooltip("맥동이 가장 옅어진 시점의 안내 칸 알파. 하이라이트 스프라이트 자체가 이미 반투명하므로" +
        "(테두리 0.78 · 채움 0.27) 여기에 곱해진다 - 낮게 잡으면 맥동이 아니라 깜빡 꺼지는 것처럼 보인다.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float _guidePulseMinAlpha = 0.7f;

    [Tooltip("맥동이 가장 진해진 시점의 안내 칸 알파.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float _guidePulseMaxAlpha = 1f;

    [Tooltip("안내 칸에만 쓸 스프라이트. 기본 하이라이트는 채움 알파가 0.27이라 색을 아무리 진하게 잡아도 " +
        "그 값이 천장이 된다 - 모양이 같고 알파만 불투명한 판을 따로 둔다. 비우면 기본 하이라이트를 그대로 쓴다.")]
    [WiringOptional]
    [SerializeField]
    private Sprite _placementGuideSprite;

    [Tooltip("타워 배치/이동 미리보기 중 공격 사거리를 타원으로 표시할 인디케이터.")]
    [SerializeField]
    private RangeIndicator _rangeIndicator;

    [Tooltip("설치 미리보기 사거리에 현재 연구 등의 배율을 반영할 합성기. 비어 있으면 씬에서 자동으로 찾는다.")]
    [WiringOptional]
    [SerializeField]
    private TowerStatMultiplierComposite _towerStatMultiplierComposite;

    [Tooltip("오라 타워 또는 새끼용 배치/이동 미리보기 중 버프 반경을 타원으로 표시할 인디케이터. 공격 사거리와 별개 원이라 서로 다른 색으로 구분해둘 것.")]
    [SerializeField]
    private RangeIndicator _buffRangeIndicator;

    [Tooltip("새끼용 배치 미리보기의 버프 반경에 해금 보너스를 반영하는 계산기. 비어 있으면 씬에서 자동으로 찾고, " +
        "그것도 없으면 새끼용 데이터의 기본 반경을 그대로 쓴다.")]
    [WiringOptional]
    [SerializeField]
    private BabyDragonBuffSystem _babyDragonBuffSystem;

    private Camera _cam;
    private bool _hasResolvedTowerStatMultiplierComposite;
    private ComponentPool<SpriteRenderer> _selectionHighlightPool;
    private ComponentPool<SpriteRenderer> _occupiedHighlightPool;
    private FootprintShape _baseFootprintShape = new FootprintShape(new bool[1, 1] { { true } });
    private FootprintShape _footprintShape = new FootprintShape(new bool[1, 1] { { true } });
    private int _previewRotationSteps;
    // 회전 짝홀 어긋남을 보정하는 지면 중앙 성분 - GridMap.ApplyPlacement가 실제 배치 때 더하는
    // ComputeRotationCompensation과 같은 값이라, 판정 표시(DrawRangeIndicator)는 이 성분만 쓴다.
    private Vector3 _ghostGroundCompensation;
    // 프리팹 루트 localPosition(+ 회전별 미세조정) - 스프라이트를 지면 위로 띄워 보이는 표시 전용
    // 성분. 고스트 렌더러(DrawGhost)만 이 값을 더한다.
    private Vector3 _ghostVisualOffset;
    private Sprite _ghostSpriteOverride;
    // 풀을 한 번이라도 만들어 봤는지. 템플릿 렌더러가 없어 풀이 null로 남는 씬에서 매 프레임
    // 재시도하지 않게 한다. 도메인 리로드로 이 값도 함께 초기화되므로 복구 목적은 그대로 지켜진다.
    private bool _hasBuiltPools;

    private bool _isPlacementActive;
    private bool _isRepositionPreview;
    private Vector3Int? _lastDrawnAnchor;
    private Building _selectedBuildingRef; // 재배치 중이면 실제 인스턴스 - 자기 자신과 겹치는 위치도 유효하게 판정하기 위함
    private BuildingPlacementController _buildingPlacementController;
    private readonly List<(List<Vector3Int> Coords, Color Color)> _placementHighlightGroups = new();
    // 맥동시킬 안내 칸의 렌더러. 풀의 Get()이 호출마다 SetActive(true)를 부르므로 매 프레임 다시 칠하지 않고
    // 그릴 때 담아둔 참조의 색만 훑어 고친다(ConqueredChunkBorderRenderer.Update와 같은 관례).
    private readonly List<SpriteRenderer> _pulsingGuideHighlights = new();

    // 선택 하이라이트 풀이 원래 쓰는 스프라이트. 안내 칸에 다른 판을 끼웠다가 되돌릴 자리가 필요하다 -
    // 풀은 인덱스를 돌려 쓰므로 되돌리지 않으면 다음에 그 자리를 받은 평범한 칸이 안내 판을 그대로 쓴다.
    //
    // 런타임에 스스로 채우지만 [SerializeField]인 이유: ComponentPool이 seedInstance로
    // _selectionHighlightRenderer <b>자기 자신</b>을 0번 슬롯에 쓰므로, 안내 칸이 0번을 잡으면
    // 템플릿의 스프라이트가 안내 판으로 바뀐다. 이 값이 직렬화되지 않으면 플레이 중 스크립트
    // 리컴파일로 초기화된 뒤 EnsureRuntimeState가 그 바뀐 템플릿에서 "원본"을 다시 잡아,
    // 그 판 이후 모든 하이라이트(초록·빨강·노랑)가 불투명 판으로 그려진다.
    // 직렬화 필드는 도메인 리로드를 넘겨 살아남으므로 그 경로가 막힌다.
    //
    // 인스펙터에서는 숨긴다 - 손으로 넣은 값이 들어오면 EnsureRuntimeState가 템플릿에서 다시 잡은 뒤에도
    // 그 값이 하이라이트 전체의 스프라이트가 되어, 템플릿 스프라이트를 교체해도 옛 판이 계속 그려진다.
    // 채우는 주체는 언제나 EnsureRuntimeState 한 곳뿐이어야 한다.
    [HideInInspector]
    [WiringOptional]
    [SerializeField]
    private Sprite _defaultHighlightSprite;

    public Vector3Int CurrentAnchor { get; private set; }
    public bool CanConstruct { get; private set; }
    public Color SelectionHighlightColor => _selectionHighlightColor;

    // GridMap.CellSurfaceYOffset으로 위임 - 셀 하이라이트·청크 경계 등 여러 오버레이가
    // GetYOffsetOrZero를 통해 공유하는 값이라, 여기서 값을 새로 갖지 않고 단일 소유자(GridMap)를 그대로 읽는다.
    public float YOffset => _gridMap != null ? _gridMap.CellSurfaceYOffset : 0f;

    // 미리보기 중인(아직 확정 안 된) 회전 스텝 - 배치/이동 확정 시 BuildingPlacementController가 그대로 GridMap에 넘긴다.
    public int PreviewRotationSteps => _previewRotationSteps;

    // 현재 회전이 반영된 풋프린트 모양 - 확정 시에도 미리보기와 동일한 모양을 쓰기 위해 공개.
    public FootprintShape CurrentFootprintShape => _footprintShape;

    // 미리보기 중인 건물이 실제로 놓일 지면 중앙 좌표. GridMap.ApplyPlacement가 배치 시 쓰는 식과
    // 반드시 같아야 위치로 판정하는 것들(지형 페널티 완화 반경 등)이 미리보기와 실제 사이에서 어긋나지 않는다.
    public Vector3 PreviewGroundWorldPosition => GetPreviewGroundPosition(CurrentAnchor);

    // 참조가 비어 있어도(=null) 안전하게 0을 반환 - Y 오프셋을 쓰는 다른 오버레이 스크립트들이 공용으로 사용.
    public static float GetYOffsetOrZero(MouseSelectController mouseSelectController) =>
        mouseSelectController != null ? mouseSelectController.YOffset : 0f;

    public void SetBuildingPlacementController(BuildingPlacementController controller) =>
        _buildingPlacementController = controller;

    public void ClearBuildingPlacementController(BuildingPlacementController controller)
    {
        if (ReferenceEquals(_buildingPlacementController, controller))
            _buildingPlacementController = null;
    }

    private void Awake()
    {
        EnsureRuntimeState();
        Deactivate();
    }

    private void OnEnable()
    {
        EnsureRuntimeState();
    }

    private void OnDisable()
    {
        _pulsingGuideHighlights.Clear();
        _selectionHighlightPool?.DeactivateAll();
        _occupiedHighlightPool?.DeactivateAll();

        if (_ghostRenderer != null)
            _ghostRenderer.gameObject.SetActive(false);

        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();
    }

    // 플레이 중 스크립트 리컴파일로 비직렬화 런타임 필드가 초기화돼도 다시 사용할 수 있게 복구한다.
    // 풀 목록만 유실되고 기존 복제 렌더러가 씬에 남았을 수 있으므로 재생성 전에 고아 복제본도 정리한다.
    private void EnsureRuntimeState()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (!_hasResolvedTowerStatMultiplierComposite)
        {
            if (_towerStatMultiplierComposite == null)
                _towerStatMultiplierComposite = FindFirstObjectByType<TowerStatMultiplierComposite>();

            _hasResolvedTowerStatMultiplierComposite = true;
        }

        if (_selectionHighlightPool != null && _occupiedHighlightPool != null)
            return;

        // 템플릿 렌더러가 비어 있으면 CreatePool이 null을 돌려주어 위 조기 반환이 영원히 성립하지 않는다.
        // 그 상태로 두면 아래 정리·생성이 호출될 때마다(툴팁 호버 판정은 매 프레임 부른다) 다시 돈다.
        if (_hasBuiltPools)
            return;

        _hasBuiltPools = true;

        RemoveOrphanedPoolObjects();

        // 템플릿이 안내 판을 들고 있으면 잡지 않는다 - 리컴파일 복구 경로에서는 풀 0번(=템플릿)이
        // 마지막으로 그린 안내 칸의 스프라이트를 그대로 들고 있다. 그때는 이미 직렬화된 값이 살아 있다.
        //
        // 안내 판이 배선되지 않은 씬에서는 이 판단을 건너뛴다 - 스프라이트를 바꿔 끼우는 일 자체가 없어
        // 템플릿이 안내 판을 들고 있을 수가 없는데, null끼리 맞아떨어져 "안내 판을 들고 있다"로 읽히면
        // 원본을 영영 못 잡는다.
        bool templateHoldsGuideSprite =
            _placementGuideSprite != null &&
            _selectionHighlightRenderer != null &&
            ReferenceEquals(_selectionHighlightRenderer.sprite, _placementGuideSprite);

        // 비어 있을 때만이 아니라 풀을 지을 때마다 다시 잡는다(위 조기 반환 탓에 도메인 리로드당 한 번).
        // "비었을 때만"으로 두면 직렬화된 옛 값이 영영 남아, 템플릿의 스프라이트를 교체해도
        // 모든 하이라이트가 예전 판으로 그려진다.
        if (_selectionHighlightRenderer != null && !templateHoldsGuideSprite)
        {
            _defaultHighlightSprite = _selectionHighlightRenderer.sprite;
        }

        _selectionHighlightPool = CreatePool(_selectionHighlightRenderer, true);

        _occupiedHighlightPool = _occupiedHighlightRenderer != null
            ? CreatePool(_occupiedHighlightRenderer, true)
            : CreatePool(_selectionHighlightRenderer, false);

        _selectionHighlightPool?.DeactivateAll();
        _occupiedHighlightPool?.DeactivateAll();
    }

    private ComponentPool<SpriteRenderer> CreatePool(SpriteRenderer renderer, bool useSeedInstance)
    {
        if (renderer == null)
            return null;

        return new ComponentPool<SpriteRenderer>(
            renderer,
            renderer.transform.parent,
            useSeedInstance ? renderer : null);
    }

    // 도메인 리로드로 풀 목록만 유실된 경우, 등록된 템플릿/고스트가 아닌 런타임 복제 렌더러를 정리한다.
    // 이름에 의존하지 않고 각 템플릿의 부모 아래만 검사하므로 전용 Highlight 부모 구조에서도 그대로 동작한다.
    private void RemoveOrphanedPoolObjects()
    {
        var poolParents = new HashSet<Transform>();
        AddPoolParent(poolParents, _selectionHighlightRenderer);
        AddPoolParent(poolParents, _occupiedHighlightRenderer);

        foreach (Transform poolParent in poolParents)
        {
            for (int i = poolParent.childCount - 1; i >= 0; i--)
            {
                Transform child = poolParent.GetChild(i);
                if (!child.TryGetComponent(out SpriteRenderer renderer) || IsRegisteredRenderer(renderer))
                    continue;

                child.gameObject.SetActive(false);

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void AddPoolParent(HashSet<Transform> poolParents, SpriteRenderer renderer)
    {
        if (renderer != null && renderer.transform.parent != null)
            poolParents.Add(renderer.transform.parent);
    }

    private bool IsRegisteredRenderer(SpriteRenderer renderer) =>
        renderer == _selectionHighlightRenderer ||
        renderer == _occupiedHighlightRenderer ||
        renderer == _ghostRenderer;

    public void SetPlacementActive(bool isActive)
    {
        EnsureRuntimeState();
        _isPlacementActive = isActive;

        if (_isPlacementActive)
            _lastDrawnAnchor = null;
        else
            Deactivate();
    }

    // 하이라이트를 그릴 때(HighlightCells 등) GridMap.GetCellSurfaceWorld로 타일 표면까지 띄우므로,
    // 역방향인 여기서도 같은 양(YOffset)을 빼서 "하이라이트가 그려진 자리"를 기준으로 셀을 찾는다.
    // 셀 판정은 ConvertWorldToGrid가 아니라 PickCellAtWorldPoint로 해야 단차가 높은 지형에서도
    // 화면에 보이는 타일과 클릭 지점이 일치한다.
    // 포인터의 월드 좌표(Y 오프셋 미적용). 그리드 셀이 아니라 실제로 그려진 위치와 비교해야 하는
    // 판정(예: 스프라이트 bounds 히트테스트)에 쓴다.
    public Vector3 GetPointerWorldPoint()
    {
        EnsureRuntimeState();
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;

        return worldPos;
    }

    /// <summary>
    /// 포인터 위치를 안전하게 구한다. 카메라나 마우스가 없으면 false를 돌려준다 -
    /// 매 프레임 도는 호출부(툴팁 호버 판정)가 씬 전환·카메라 교체 순간에 예외로 죽지 않게 한다.
    /// 실패를 좌표로 뭉개지 않으므로 <see cref="GetPointerWorldPoint"/>를 쓰는 클릭 판정과 달리
    /// "원점을 가리켰다"로 오해될 여지가 없다.
    /// </summary>
    public bool TryGetPointerWorldPoint(out Vector3 worldPoint)
    {
        EnsureRuntimeState();

        if (_cam == null || Mouse.current == null)
        {
            worldPoint = default;
            return false;
        }

        worldPoint = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPoint.z = 0f;

        return true;
    }

    /// <summary>
    /// 커서가 얹힌 셀을 안전하게 구한다. <see cref="GetHoveredCell"/>의 예외 없는 판.
    /// </summary>
    public bool TryGetHoveredCell(out Vector3Int cell)
    {
        if (!TryGetPointerWorldPoint(out Vector3 worldPos))
        {
            cell = default;
            return false;
        }

        worldPos.y -= YOffset;
        cell = _gridMap.PickCellAtWorldPoint(worldPos);

        return true;
    }

    public Vector3Int GetHoveredCell()
    {
        Vector3 worldPos = GetPointerWorldPoint();
        worldPos.y -= YOffset;

        return _gridMap.PickCellAtWorldPoint(worldPos);
    }

    public Vector3Int GetHoveredAnchor(FootprintShape shape) => GetFootprintAnchor(GetHoveredCell(), shape);

    private void Update()
    {
        if (!_isPlacementActive)
            return;

        EnsureRuntimeState();
        Vector3Int hoveredCell = GetHoveredCell();
        Vector3Int anchor = GetFootprintAnchor(hoveredCell, _footprintShape);

        // 커서가 같은 칸에 머무는 프레임은 다시 그리지 않는다 - 풀의 Get()이 호출마다 SetActive(true)를
        // 부르므로 매 프레임 재구성은 낭비다. 맥동은 이미 그려둔 렌더러의 색만 건드리므로 재구성 없이 이어진다.
        if (!_lastDrawnAnchor.HasValue || anchor != _lastDrawnAnchor.Value)
        {
            _lastDrawnAnchor = anchor;

            List<Vector3Int> footprint = _gridMap.GetFootprintCoords(anchor, _footprintShape);
            bool canConstruct =
                _gridMap.CanConstructBuildingFootprint(footprint, _selectedBuildingRef, _selectedBuildingRef) &&
                (_buildingPlacementController == null || _isRepositionPreview ||
                 _buildingPlacementController.CanPlaceBuildingAt(_selectedBuildingRef, anchor));

            CurrentAnchor = anchor;
            CanConstruct = canConstruct;

            DrawPlacementHighlights(footprint, canConstruct);
            DrawGhost(anchor, canConstruct);
            DrawRangeIndicator(anchor);
        }

        PulseGuideHighlights();
    }

    /// <summary>
    /// 안내 칸의 알파만 사인파로 오르내린다. 색을 바꾸는 것만으로는 정지한 칸이 지형에 묻혀
    /// "여기에 지어라"가 한눈에 읽히지 않는다 - 움직임이 시선을 끄는 몫을 맡는다.
    /// </summary>
    private void PulseGuideHighlights()
    {
        if (_pulsingGuideHighlights.Count == 0)
            return;

        float pulse = (Mathf.Sin(Time.unscaledTime * _guidePulseSpeed * FULL_TURN_RADIANS) + 1f) * PULSE_HALF_RANGE;
        float alpha = Mathf.Lerp(_guidePulseMinAlpha, _guidePulseMaxAlpha, pulse);

        foreach (SpriteRenderer highlight in _pulsingGuideHighlights)
        {
            Color color = highlight.color;
            color.a = alpha;
            highlight.color = color;
        }
    }

    public void BeginPlacementPreview(Building prefab)
    {
        _isRepositionPreview = false;
        SetPreviewTarget(prefab, 0);
    }

    public void BeginRepositionPreview(Building building)
    {
        _isRepositionPreview = true;
        SetPreviewTarget(building, building.RotationSteps);
    }

    private void SetPreviewTarget(Building building, int initialRotationSteps)
    {
        // 새 배치/이동을 시작할 때마다 이전 대상용으로 걸려 있던 강제 스프라이트를 해제한다 -
        // 그대로 두면 예를 들어 새끼용 배치 취소 후 일반 건물을 선택했을 때 새끼용 색이 남는다.
        _ghostSpriteOverride = null;
        _selectedBuildingRef = building;
        _baseFootprintShape = building.BaseFootprintShape;
        _previewRotationSteps = initialRotationSteps;
        _footprintShape = _baseFootprintShape.Rotated(_previewRotationSteps);
        _lastDrawnAnchor = null;

        RefreshGhostVisual();
    }

    // BabyDragonTower처럼 프리팹 자체엔 스프라이트가 없고 배치 시점에야 데이터로 정해지는
    // 건물의 고스트를 위해, 외부에서 강제로 스프라이트를 지정할 수 있게 한다.
    // BeginPlacementPreview/BeginRepositionPreview로 새 대상을 잡을 때 자동으로 해제된다.
    public void SetGhostSpriteOverride(Sprite sprite)
    {
        _ghostSpriteOverride = sprite;
        RefreshGhostVisual();
    }

    // 배치/이동 미리보기 중 "R" 입력 시 호출 - 다음 회전 스텝으로 넘기고 footprint/고스트를 다시 계산한다.
    public void RotatePreview()
    {
        if (_selectedBuildingRef == null)
            return;

        _previewRotationSteps = (_previewRotationSteps + 1) % FootprintShape.ROTATION_STEP_COUNT;
        _footprintShape = _baseFootprintShape.Rotated(_previewRotationSteps);
        _lastDrawnAnchor = null;

        RefreshGhostVisual();
    }

    private void RefreshGhostVisual()
    {
        _ghostGroundCompensation = _gridMap != null
            ? _gridMap.ComputeRotationCompensation(_baseFootprintShape, _previewRotationSteps)
            : Vector3.zero;
        _ghostVisualOffset = _selectedBuildingRef.ComputePlacementOffset(_previewRotationSteps);

        if (_ghostRenderer == null)
            return;

        Sprite ghostSprite = _ghostSpriteOverride;
        if (ghostSprite == null)
        {
            ghostSprite = _selectedBuildingRef.ResolveRotationSprite(_previewRotationSteps);
        }
        if (ghostSprite == null)
        {
            SpriteRenderer prefabRenderer = _selectedBuildingRef.GetComponent<SpriteRenderer>();
            ghostSprite = prefabRenderer != null ? prefabRenderer.sprite : null;
        }

        _ghostRenderer.sprite = ghostSprite;
        _ghostRenderer.transform.localScale = Vector3.Scale(
            _selectedBuildingRef.BaseLocalScale,
            _selectedBuildingRef.ResolveRotationScale(_previewRotationSteps));
        _ghostRenderer.gameObject.SetActive(ghostSprite != null);
    }

    private Vector3Int GetFootprintAnchor(Vector3Int hoveredCell, FootprintShape shape) =>
        hoveredCell - shape.CenterOffset;

    // 셀 색상은 모든 건물이 같은 2색 체계다 - 초록은 배치 가능, 빨강은 배치 불가.
    // 자원 노드 없음·봉인 영역 아님처럼 건물 종류 고유의 사유도 따로 주황으로 구분하지 않는다.
    // 주황은 빨강과 비슷해 눈에 띄지 않는 데다 색만으로는 사유를 읽어낼 수 없어 오히려 모호했다 -
    // 사유는 경고 토스트(UI_WarningWindow)가 대신 알린다.
    private void DrawFootprint(List<Vector3Int> footprint, bool canConstruct) =>
        HighlightCells(footprint, canConstruct ? Color.green : Color.red);

    private void DrawPlacementHighlights(List<Vector3Int> footprint, bool canConstruct)
    {
        if (_buildingPlacementController == null || _isRepositionPreview ||
            !_buildingPlacementController.TryGetPlacementGuideAnchors(
                _selectedBuildingRef,
                out IReadOnlyList<Vector3Int> guideAnchors))
        {
            DrawFootprint(footprint, canConstruct);
            return;
        }

        _placementHighlightGroups.Clear();

        foreach (Vector3Int guideAnchor in guideAnchors)
        {
            if (guideAnchor == CurrentAnchor)
                continue;

            List<Vector3Int> guideFootprint = _gridMap.GetFootprintCoords(guideAnchor, _footprintShape);
            if (_gridMap.CanConstructBuildingFootprint(
                    guideFootprint,
                    _selectedBuildingRef,
                    _selectedBuildingRef))
            {
                _placementHighlightGroups.Add((guideFootprint, _placementGuideColor));
            }
        }

        // 안내 칸을 초록으로 칠하지 않는 이유: 커서 밑 "지금 놓을 수 있다"와 같은 색이면 둘이 구별되지 않아
        // 어디가 안내인지 읽히지 않는다. 시안 = "여기에 지어라", 초록 = "지금 놓인다"로 역할을 나눈다.
        // 안내 칸을 먼저 담고 커서 풋프린트를 마지막에 붙이므로, 앞선 묶음들만 맥동 대상이 된다.
        int guideGroupCount = _placementHighlightGroups.Count;

        _placementHighlightGroups.Add((footprint, canConstruct ? Color.green : Color.red));
        HighlightCellGroups(_placementHighlightGroups, guideGroupCount);
    }

    public void HighlightCells(List<Vector3Int> coords, Color color)
    {
        EnsureRuntimeState();
        HighlightCells(coords, color, _selectionHighlightPool);
    }

    /// <summary>
    /// 안내 판을 끼웠을지 모르는 렌더러를 원래 판으로 되돌린다.
    ///
    /// 원본을 못 잡았으면 <b>아무것도 하지 않는다</b> - null을 그대로 넣으면 그 칸이 통째로 사라진다.
    /// 이 경로는 건설 미리보기뿐 아니라 인구 배치·워커 모드의 그룹 하이라이트도 함께 쓰므로,
    /// 안내 판 배선이 어긋난 씬 하나 때문에 공용 하이라이트가 전부 안 보이는 일이 없어야 한다.
    /// </summary>
    private void RestoreDefaultSprite(SpriteRenderer highlight)
    {
        if (_defaultHighlightSprite != null)
        {
            highlight.sprite = _defaultHighlightSprite;
        }
    }

    private void HighlightCells(List<Vector3Int> coords, Color color, ComponentPool<SpriteRenderer> pool)
    {
        bool isSelectionPool = ReferenceEquals(pool, _selectionHighlightPool);

        // 선택 풀을 통째로 다시 칠하면 안내 칸이 쓰던 렌더러도 다른 뜻으로 넘어간다 - 캐시를 비우지 않으면
        // 안내가 끝난 뒤에도 평범한 풋프린트 칸이 계속 깜빡인다. 점유 표시 풀은 별개라 건드리지 않는다.
        if (isSelectionPool)
        {
            _pulsingGuideHighlights.Clear();
        }

        for (int i = 0; i < coords.Count; i++)
        {
            SpriteRenderer highlight = pool.Get(i);
            highlight.transform.position = _gridMap.GetCellSurfaceWorld(coords[i]);
            highlight.color = color;

            if (isSelectionPool)
            {
                RestoreDefaultSprite(highlight);
            }
        }

        pool.DeactivateFrom(coords.Count);
    }

    // 색상이 서로 다른 여러 좌표 묶음을 한 번에 칠한다(인구 배치 모드의 상태별 색 등).
    // 선택 하이라이트 풀을 함께 쓴다 - 이 API를 쓰는 모드는 IExclusiveMode라 건설 모드와 동시에
    // 활성화되지 않고, 정리도 같은 ClearHighlights()를 거치므로 서로를 지울 일이 없다.
    public void HighlightCellGroups(IReadOnlyList<(List<Vector3Int> Coords, Color Color)> groups) =>
        HighlightCellGroups(groups, PULSING_GROUP_NONE);

    // 앞선 pulsingGroupCount개 묶음에 쓰인 렌더러는 따로 모아둔다 - Update가 그 렌더러들의 알파만
    // 매 프레임 고쳐 맥동시킨다. 나머지 묶음은 오늘과 똑같이 한 번 칠하고 끝난다.
    private void HighlightCellGroups(
        IReadOnlyList<(List<Vector3Int> Coords, Color Color)> groups,
        int pulsingGroupCount)
    {
        EnsureRuntimeState();
        _pulsingGuideHighlights.Clear();

        int index = 0;
        int groupIndex = 0;

        foreach (var group in groups)
        {
            foreach (Vector3Int coord in group.Coords)
            {
                bool isGuideCell = groupIndex < pulsingGroupCount;

                SpriteRenderer highlight = _selectionHighlightPool.Get(index);
                highlight.transform.position = _gridMap.GetCellSurfaceWorld(coord);
                highlight.color = group.Color;

                if (isGuideCell && _placementGuideSprite != null)
                {
                    highlight.sprite = _placementGuideSprite;
                }
                else
                {
                    RestoreDefaultSprite(highlight);
                }

                if (isGuideCell)
                {
                    _pulsingGuideHighlights.Add(highlight);
                }

                index++;
            }

            groupIndex++;
        }

        _selectionHighlightPool.DeactivateFrom(index);
    }

    public void HighlightSelection(List<Vector3Int> coords) => HighlightCells(coords, _selectionHighlightColor);

    // 건설 모드에서 이미 건물이 배치된 타일을 표시 - 어떤 땅이 비어있는지 한눈에 파악 가능
    public void ShowOccupiedOverlay(List<Vector3Int> coords)
    {
        EnsureRuntimeState();
        HighlightCells(coords, _occupiedOverlayColor, _occupiedHighlightPool);
    }

    public void ClearOccupiedOverlay()
    {
        EnsureRuntimeState();
        _occupiedHighlightPool?.DeactivateAll();
    }

    public void ClearHighlights()
    {
        EnsureRuntimeState();
        _pulsingGuideHighlights.Clear();
        _selectionHighlightPool?.DeactivateAll();
    }

    private void DrawGhost(Vector3Int anchor, bool canConstruct)
    {
        if (_ghostRenderer == null || !_ghostRenderer.gameObject.activeSelf)
            return;

        _ghostRenderer.transform.position = GetPreviewWorldPosition(anchor);
        _ghostRenderer.sortingOrder = IsometricMath.ComputeDepthSortOrder(anchor);

        Color color = canConstruct ? Color.white : _ghostBlockedTint;
        color.a = _ghostAlpha;
        _ghostRenderer.color = color;
    }

    private void DrawRangeIndicator(Vector3Int anchor)
    {
        // 판정 기준점(타일 표면 중앙)으로 그린다 - 시각 오프셋(_ghostVisualOffset)까지 더하면
        // 새끼용처럼 스프라이트가 위로 뜬 건물의 사거리 원이 몸통 쪽으로 밀려 표시=판정 규약이 깨진다.
        Vector3 center = GetPreviewGroundPosition(anchor);

        DrawAttackRangeIndicator(center);
        DrawBuffRangeIndicator(center);
    }

    // 셀 중앙 평면 - 고스트 스프라이트가 프리팹 오프셋을 더할 기준(회전 짝홀 보정만 반영).
    private Vector3 GetPreviewCellCenterPosition(Vector3Int anchor) =>
        _gridMap != null
            ? _gridMap.GetFootprintCenterWorld(anchor, _footprintShape) + _ghostGroundCompensation
            : Vector3.zero;

    // 타일 표면 평면 - 실제 배치 후 Building.GroundWorldPosition과 같은 값(GridMap.ApplyPlacement 참고).
    // 사거리·버프 반경 표시(DrawRangeIndicator)와 판정 위치 조회(PreviewGroundWorldPosition)가 쓴다.
    private Vector3 GetPreviewGroundPosition(Vector3Int anchor) =>
        _gridMap != null
            ? GetPreviewCellCenterPosition(anchor) + _gridMap.CellSurfaceOffset
            : Vector3.zero;

    // 고스트 스프라이트 표시 전용 - 셀 중앙에 프리팹 루트 localPosition(+회전별 미세조정)을 더한다.
    private Vector3 GetPreviewWorldPosition(Vector3Int anchor) =>
        GetPreviewCellCenterPosition(anchor) + _ghostVisualOffset;

    // 배치/이동 대상이 공격 가능한 타워일 때만, 실제 판정(TowerAttack.IsWithinAttackRange)과 같은
    // 타원으로 사거리를 표시한다 - 그 외 건물이거나 인디케이터가 연결 안 됐으면 숨긴다.
    private void DrawAttackRangeIndicator(Vector3 center)
    {
        if (_rangeIndicator == null)
            return;

        if (!(_selectedBuildingRef is Tower tower) ||
            tower.Data == null ||
            !tower.Data.CanAttack)
        {
            _rangeIndicator.Hide();
            return;
        }

        if (tower is BabyDragonTower babyDragon &&
            babyDragon.Mode != BabyDragonMode.Attack)
        {
            _rangeIndicator.Hide();
            return;
        }

        float radiusX = TowerAttack.CalculateEffectiveRange(
            tower.Data,
            _towerStatMultiplierComposite);
        float radiusY = radiusX * IsometricMath.RADIUS_Y_RATIO;

        _rangeIndicator.SetCenter(center);
        _rangeIndicator.Show(radiusX, radiusY);
    }

    // 일반 오라 타워와 버프 모드 새끼용의 현재 유효 반경을 표시한다.
    private void DrawBuffRangeIndicator(Vector3 center)
    {
        if (_buffRangeIndicator == null)
            return;

        if (_selectedBuildingRef is BabyDragonTower babyDragon)
        {
            if (babyDragon.Mode != BabyDragonMode.Buff ||
                babyDragon.DragonData == null)
            {
                _buffRangeIndicator.Hide();
                return;
            }

            _babyDragonBuffSystem ??=
                Object.FindFirstObjectByType<BabyDragonBuffSystem>();
            float radius = _babyDragonBuffSystem != null
                ? _babyDragonBuffSystem.GetEffectiveBuffRadius(babyDragon)
                : babyDragon.DragonData.BuffRadius;

            if (radius > 0f)
            {
                ShowBuffRange(center, radius);
            }
            else
            {
                _buffRangeIndicator.Hide();
            }

            return;
        }

        if (_selectedBuildingRef is Tower tower &&
            tower.Data is ITowerAuraDataProvider provider &&
            provider.HasTowerAura)
        {
            float auraRadius;
            bool hasAuraRange = _isRepositionPreview
                ? TowerAuraSystem.TryGetActiveAura(
                    tower,
                    out _,
                    out auraRadius)
                : TowerAuraSystem.TryGetPreviewRadius(
                    tower.Data,
                    out auraRadius);

            if (hasAuraRange)
            {
                ShowBuffRange(center, auraRadius);
            }
            else
            {
                _buffRangeIndicator.Hide();
            }

            return;
        }

        _buffRangeIndicator.Hide();
    }

    private void Deactivate()
    {
        if (_ghostRenderer != null)
            _ghostRenderer.gameObject.SetActive(false);

        ClearHighlights();
        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();
        _isRepositionPreview = false;
        CanConstruct = false;
    }

    private void ShowBuffRange(Vector3 center, float radiusX)
    {
        float radiusY =
            radiusX * IsometricMath.RADIUS_Y_RATIO;

        _buffRangeIndicator.SetCenter(center);
        _buffRangeIndicator.Show(radiusX, radiusY);
    }
}
