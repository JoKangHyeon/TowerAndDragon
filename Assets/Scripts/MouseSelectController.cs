using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections.Generic;

public class MouseSelectController : MonoBehaviour
{
    [FormerlySerializedAs("_spriteRenderer")]
    [SerializeField]
    private SpriteRenderer _selectionHighlightRenderer;

    [SerializeField]
    private SpriteRenderer _occupiedHighlightRenderer;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private float _yOffset = 0.7f;

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
    private Vector3 _ghostLocalOffset;
    private Sprite _ghostSpriteOverride;
    // 풀을 한 번이라도 만들어 봤는지. 템플릿 렌더러가 없어 풀이 null로 남는 씬에서 매 프레임
    // 재시도하지 않게 한다. 도메인 리로드로 이 값도 함께 초기화되므로 복구 목적은 그대로 지켜진다.
    private bool _hasBuiltPools;

    private bool _isPlacementActive;
    private bool _isRepositionPreview;
    private Vector3Int? _lastDrawnAnchor;
    private Building _selectedBuildingRef; // 재배치 중이면 실제 인스턴스 - 자기 자신과 겹치는 위치도 유효하게 판정하기 위함

    public Vector3Int CurrentAnchor { get; private set; }
    public bool CanConstruct { get; private set; }
    public Color SelectionHighlightColor => _selectionHighlightColor;
    public float YOffset => _yOffset;

    // 미리보기 중인(아직 확정 안 된) 회전 스텝 - 배치/이동 확정 시 BuildingPlacementController가 그대로 GridMap에 넘긴다.
    public int PreviewRotationSteps => _previewRotationSteps;

    // 현재 회전이 반영된 풋프린트 모양 - 확정 시에도 미리보기와 동일한 모양을 쓰기 위해 공개.
    public FootprintShape CurrentFootprintShape => _footprintShape;

    // 참조가 비어 있어도(=null) 안전하게 0을 반환 - Y 오프셋을 쓰는 다른 오버레이 스크립트들이 공용으로 사용.
    public static float GetYOffsetOrZero(MouseSelectController mouseSelectController) =>
        mouseSelectController != null ? mouseSelectController.YOffset : 0f;

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

    // 하이라이트를 그릴 때(HighlightCells 등) ConvertGridToWorld 결과에 _yOffset을 더하므로,
    // 역방향인 여기서도 같은 양을 빼서 "하이라이트가 그려진 자리"를 기준으로 셀을 찾는다.
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

        worldPos.y -= _yOffset;
        cell = _gridMap.PickCellAtWorldPoint(worldPos);

        return true;
    }

    public Vector3Int GetHoveredCell()
    {
        Vector3 worldPos = GetPointerWorldPoint();
        worldPos.y -= _yOffset;

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

        if (_lastDrawnAnchor.HasValue && anchor == _lastDrawnAnchor.Value)
            return;

        _lastDrawnAnchor = anchor;

        List<Vector3Int> footprint = _gridMap.GetFootprintCoords(anchor, _footprintShape);
        bool canConstruct = _gridMap.CanConstructBuildingFootprint(footprint, _selectedBuildingRef, _selectedBuildingRef);

        CurrentAnchor = anchor;
        CanConstruct = canConstruct;

        DrawFootprint(footprint, canConstruct);
        DrawGhost(anchor, canConstruct);
        DrawRangeIndicator(anchor);
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
        Vector3 compensation = _gridMap != null
            ? _gridMap.ComputeRotationCompensation(_baseFootprintShape, _previewRotationSteps)
            : Vector3.zero;
        _ghostLocalOffset = _selectedBuildingRef.ComputePlacementOffset(_previewRotationSteps) + compensation;

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

    public void HighlightCells(List<Vector3Int> coords, Color color)
    {
        EnsureRuntimeState();
        HighlightCells(coords, color, _selectionHighlightPool);
    }

    private void HighlightCells(List<Vector3Int> coords, Color color, ComponentPool<SpriteRenderer> pool)
    {
        for (int i = 0; i < coords.Count; i++)
        {
            SpriteRenderer highlight = pool.Get(i);
            Vector3 cellPos = _gridMap.ConvertGridToWorld(coords[i]);
            cellPos.y += _yOffset;
            highlight.transform.position = cellPos;
            highlight.color = color;
        }

        pool.DeactivateFrom(coords.Count);
    }

    // 색상이 서로 다른 여러 좌표 묶음을 한 번에 칠한다(인구 배치 모드의 상태별 색 등).
    // 선택 하이라이트 풀을 함께 쓴다 - 이 API를 쓰는 모드는 IExclusiveMode라 건설 모드와 동시에
    // 활성화되지 않고, 정리도 같은 ClearHighlights()를 거치므로 서로를 지울 일이 없다.
    public void HighlightCellGroups(IReadOnlyList<(List<Vector3Int> Coords, Color Color)> groups)
    {
        EnsureRuntimeState();
        int index = 0;

        foreach (var group in groups)
        {
            foreach (Vector3Int coord in group.Coords)
            {
                SpriteRenderer highlight = _selectionHighlightPool.Get(index);
                Vector3 cellPos = _gridMap.ConvertGridToWorld(coord);
                cellPos.y += _yOffset;
                highlight.transform.position = cellPos;
                highlight.color = group.Color;
                index++;
            }
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
        _selectionHighlightPool?.DeactivateAll();
    }

    private void DrawGhost(Vector3Int anchor, bool canConstruct)
    {
        if (_ghostRenderer == null || !_ghostRenderer.gameObject.activeSelf)
            return;

        _ghostRenderer.transform.position = _gridMap.GetFootprintCenterWorld(anchor, _footprintShape) + _ghostLocalOffset;
        _ghostRenderer.sortingOrder = IsometricMath.ComputeDepthSortOrder(anchor);

        Color color = canConstruct ? Color.white : _ghostBlockedTint;
        color.a = _ghostAlpha;
        _ghostRenderer.color = color;
    }

    private void DrawRangeIndicator(Vector3Int anchor)
    {
        Vector3 center = _gridMap.GetFootprintCenterWorld(anchor, _footprintShape) + _ghostLocalOffset;

        DrawAttackRangeIndicator(center);
        DrawBuffRangeIndicator(center);
    }

    // 배치/이동 대상이 공격 가능한 타워일 때만, 실제 판정(TowerAttack.IsWithinAttackRange)과 같은
    // 타원으로 사거리를 표시한다 - 그 외 건물이거나 인디케이터가 연결 안 됐으면 숨긴다.
    private void DrawAttackRangeIndicator(Vector3 center)
    {
        if (_rangeIndicator == null)
            return;

        if (!(_selectedBuildingRef is Tower tower) ||
            tower is BabyDragonTower ||
            tower.Data == null ||
            !tower.Data.CanAttack)
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

    // 오라 타워는 공용 오라 반경을, 기존 새끼용은 BabyDragonBuffSystem과 같은 BuffRadius를 표시한다.
    private void DrawBuffRangeIndicator(Vector3 center)
    {
        if (_buffRangeIndicator == null)
            return;

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

        if (_selectedBuildingRef is BabyDragonTower babyDragon &&
            babyDragon.DragonData != null)
        {
            _babyDragonBuffSystem ??= Object.FindFirstObjectByType<BabyDragonBuffSystem>();
            float radius = _babyDragonBuffSystem != null
                ? _babyDragonBuffSystem.GetEffectiveBuffRadius(babyDragon)
                : babyDragon.DragonData.BuffRadius;

            if (radius <= 0f)
            {
                _buffRangeIndicator.Hide();
                return;
            }

            ShowBuffRange(
                center,
                radius);
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
