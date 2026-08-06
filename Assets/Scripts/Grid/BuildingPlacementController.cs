using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildingPlacementController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private InputActionReference _placeAction;
    [SerializeField]
    private InputActionReference _cancelMoveAction;

    [SerializeField]
    private InputActionReference _rotateAction;

    [SerializeField]
    private ResourceManager _resourceManager;

    [SerializeField]
    private CycleManager _cycleManager;

    [Tooltip("타워 선택 시 공격 사거리를 타원으로 표시할 인디케이터.")]
    [SerializeField]
    private RangeIndicator _rangeIndicator;

    [Tooltip("오라 타워 또는 새끼용 선택 시 버프 반경을 타원으로 표시할 인디케이터. 공격 사거리와 별개 원이라 서로 다른 색으로 구분해둘 것.")]
    [SerializeField]
    private RangeIndicator _buffRangeIndicator;

    [Tooltip("타워를 이만큼(초) 꾹 누르고 있으면 이동 모드로 진입한다.")]
    [SerializeField]
    private float _moveHoldDuration = 1f;

    // 건물 철거 시 건설 비용 중 돌려주는 비율 - 낮밤 사이클이 한 번도 돌지 않은 당일 철거는 전액, 그 외엔 일부만 환급.
    private const float DEMOLISH_REFUND_RATIO_SAME_DAY = 1f;
    private const float DEMOLISH_REFUND_RATIO_LATE = 0.7f;

    private Building _selectedBuilding;
    private Vector3Int? _selectedExistingBuildingCoord;
    private Vector3Int? _moveSourceCoord;
    private bool _isOccupiedOverlayVisible;

    private float _holdTimer;
    private Vector3Int? _holdCoord;

    // 클릭으로 선택이 확정될 때마다 발화한다(선택 해제면 null). 같은 건물을 다시 눌러도 발화하므로
    // SelectedBuilding 폴링과 달리 "재클릭"을 놓치지 않는다.
    // 필드 초기화 시점에 생성하므로 구독자의 Awake/OnEnable 순서와 무관하게 안전하다.
    public UnityEvent<Building> BuildingSelected = new();

    public bool IsMoving => _moveSourceCoord.HasValue;

    // 건설/철거 공통 낮 판정 - CycleManager가 배선되지 않은 씬은 무제한 허용한다
    // (BabyDragonTower.IsMoveable과 같은 fail-open 관례).
    public bool IsDayForBuildActions =>
        _cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    // 철거 버튼 interactable 판정에 쓴다 - IsRemoveable(건물 종류) AND 낮(시점) 둘 다 만족해야 한다.
    public bool CanRemoveNow(Building building) =>
        building != null && building.IsRemoveable && IsDayForBuildActions;

    // 이동 버튼 interactable 판정 및 실제 이동 진입 판정에 쓴다 - IsMoveable(건물 종류 + 이동 예산) AND 낮(시점).
    public bool CanMoveNow(Building building) =>
        building != null && building.IsMoveable && IsDayForBuildActions;

    // 점령 모드 등 다른 모드가 켜져 있을 때 이 컨트롤러의 클릭 처리를 막는다.
    // (컴포넌트를 비활성화하면 공유 입력 액션까지 Disable되므로, 입력만 선택적으로 억제한다.)
    public bool InputSuppressed { get; set; }

    public Building SelectedBuilding
    {
        get
        {
            if (_selectedExistingBuildingCoord.HasValue)
                return _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);

            if (_moveSourceCoord.HasValue)
                return _gridMap.GetBuildingAt(_moveSourceCoord.Value);

            return null;
        }
    }

    // _placeAction("Confirm")은 ConquestModeController와 공유하는 액션이라 GlobalInputBootstrap이
    // 게임 시작 시 한 번만 Enable한다 - 여기서 다시 Enable/Disable하면 그 컨트롤러까지 영향을 준다.
    // _cancelMoveAction/_rotateAction은 이 컨트롤러 전용이라 여기서 직접 관리한다.
    private void OnEnable()
    {
        if (_cancelMoveAction != null)
            _cancelMoveAction.action.Enable();

        if (_rotateAction != null)
            _rotateAction.action.Enable();
    }

    private void Awake()
    {
        if (_gridMap == null)
        {   Debug.LogWarning($"[BuildingPlacementController] GridMap 인스펙터 연결 필요");
            return;
        }
        if (_mouseSelectController == null)
        {   Debug.LogWarning($"[BuildingPlacementController] MouseSelectController 인스펙터 연결 필요");
            return;
        }

        if (_placeAction == null)
        {   Debug.LogWarning($"[BuildingPlacementController] PlaceAction 인스펙터 연결 필요");
            return;
        }

        if (_cancelMoveAction == null)
        {   Debug.LogWarning($"[BuildingPlacementController] CancelMoveAction 인스펙터 연결 필요");
            return;
        }

        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    private void HandleCellChanged(GridCell cell) => RefreshOccupiedOverlay();

    private void Update()
    {
        RefreshSelectedRangeIndicator();

        // 다른 모드(점령 등)가 클릭을 점유 중이면 건물 배치/선택 입력을 처리하지 않는다.
        if (InputSuppressed)
            return;

        HandlePlacementInput();
        HandleBuildCancelInput();
        HandleCancelInput();
        HandleLongPressMove();
        HandleRotateInput();
    }

    // 슬롯을 골라 배치 미리보기 중일 때 우클릭하면 배치를 취소한다(선택 해제 + 미리보기 종료).
    private void HandleBuildCancelInput()
    {
        if (_selectedBuilding == null)
            return;

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            CancelBuildMode();
    }

    // 배치/이동 미리보기 중일 때만 회전을 적용한다 - 이동 버튼을 누르지 않고 건물만 선택한 상태에서는
    // 회전이 적용되지 않는다(제자리 회전은 지원하지 않음, 회전하려면 이동 모드로 들어가야 함).
    private void HandleRotateInput()
    {
        if (_rotateAction == null || !_rotateAction.action.WasPerformedThisFrame())
            return;

        if (_selectedBuilding != null || _moveSourceCoord.HasValue)
            _mouseSelectController.RotatePreview();
    }

    public void SelectBuilding(Building prefab)
    {
        if (prefab == null)
            return;

        _selectedBuilding = prefab;
        Deselect();
        CancelMove();
        _mouseSelectController.BeginPlacementPreview(prefab);
        _mouseSelectController.SetPlacementActive(true);
        Debug.Log($"[BuildingPlacementController] 선택된 건물: {prefab.name}");
    }

    // BabyDragonTower처럼 프리팹 자체엔 스프라이트가 없고 배치 시점에야 데이터로 정해지는
    // 건물의 고스트 미리보기 색을 지정할 때 쓴다. SelectBuilding 호출 직후에 불러야 한다 -
    // SelectBuilding이 내부적으로 새 대상을 잡으며 이전 오버라이드를 해제하기 때문이다.
    public void SetGhostSpriteOverride(Sprite sprite) => _mouseSelectController.SetGhostSpriteOverride(sprite);

    public void CancelBuildMode()
    {
        _selectedBuilding = null;
        _mouseSelectController.SetPlacementActive(false);
    }

    public void RemoveSelectedBuilding()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
            return;

        if (!IsDayForBuildActions)
            return;

        Building building = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
        bool removed = _gridMap.RemoveBuilding(_selectedExistingBuildingCoord.Value);

        if (building != null)
        {
            building.SetHighlighted(false, default);

            if (removed)
                RefundBuildCost(building);
        }

        _selectedExistingBuildingCoord = null;
        _mouseSelectController.ClearHighlights();
        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();
    }

    // 건설 비용을 환급한다 - 낮밤 사이클이 한 번도 돌지 않은 당일 건설/철거는 전액, 그 외엔 DEMOLISH_REFUND_RATIO_LATE만큼.
    private void RefundBuildCost(Building building)
    {
        if (_resourceManager == null)
            return;

        bool isSameDay = _cycleManager != null && building.ConstructedCycle == _cycleManager.CurrentCycleNumber;
        float refundRatio = isSameDay ? DEMOLISH_REFUND_RATIO_SAME_DAY : DEMOLISH_REFUND_RATIO_LATE;

        IReadOnlyList<ResourceAmount> cost = building.BuildCost;
        var refund = new ResourceAmount[cost.Count];
        for (int i = 0; i < cost.Count; i++)
        {
            refund[i] = new ResourceAmount
            {
                Type = cost[i].Type,
                Amount = Mathf.RoundToInt(cost[i].Amount * refundRatio),
            };
        }

        _resourceManager.Add(refund);
    }

    public void EnterMoveMode()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
            return;

        Building building = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
        if (building == null)
            return;

        if (!CanMoveNow(building))
            return;

        CancelBuildMode();
        _moveSourceCoord = _selectedExistingBuildingCoord;
        _selectedExistingBuildingCoord = null;

        _mouseSelectController.BeginRepositionPreview(building);
        _mouseSelectController.SetPlacementActive(true);
    }

    public void CancelMove()
    {
        if (!_moveSourceCoord.HasValue)
            return;

        Building building = _gridMap.GetBuildingAt(_moveSourceCoord.Value);
        if (building != null)
            building.SetHighlighted(false, default);

        _moveSourceCoord = null;
        _mouseSelectController.SetPlacementActive(false);
        _mouseSelectController.ClearHighlights();
    }

    public void Deselect()
    {
        if (_selectedExistingBuildingCoord.HasValue)
        {
            Building previous = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
            if (previous != null)
                previous.SetHighlighted(false, default);
        }

        _selectedExistingBuildingCoord = null;
        _mouseSelectController.ClearHighlights();
        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();
    }

    // _cancelMoveAction(우클릭)으로 새 건물 배치 미리보기, 기존 건물 이동 미리보기, 기존 건물 선택 하이라이트를 모두 취소한다.
    private void HandleCancelInput()
    {
        if (_cancelMoveAction == null || !_cancelMoveAction.action.WasPerformedThisFrame())
            return;

        if (_selectedBuilding != null)
            CancelBuildMode();
        else if (_moveSourceCoord.HasValue)
            CancelMove();
        else if (_selectedExistingBuildingCoord.HasValue)
            Deselect();
    }

    // 타워 위에서 클릭을 일정 시간 유지하면(롱프레스) 이동 모드로 진입한다.
    // (짧게 누르고 떼면 기존 클릭-선택 동작으로 처리되므로 서로 방해하지 않는다.)
    private void HandleLongPressMove()
    {
        // 이미 배치/이동 중이면 롱프레스를 추적하지 않는다.
        if (_selectedBuilding != null || _moveSourceCoord.HasValue || _placeAction == null)
        {
            _holdCoord = null;
            return;
        }

        if (_placeAction.action.WasReleasedThisFrame())
        {
            _holdCoord = null;
            return;
        }

        // 누르기 시작: 포인터 아래의 이동 가능한 건물을 홀드 대상으로 잡는다.
        if (_placeAction.action.WasPressedThisFrame())
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3Int cell = _mouseSelectController.GetHoveredCell();
            Building building = _gridMap.GetBuildingAt(cell);

            if (!overUI && building != null && CanMoveNow(building))
            {
                _holdCoord = cell;
                _holdTimer = 0f;
            }
            else
            {
                _holdCoord = null;
            }

            return;
        }

        if (!_placeAction.action.IsPressed() || !_holdCoord.HasValue)
            return;

        // 누르는 동안 포인터가 같은 건물 위에 있어야 홀드가 유지된다.
        Building holdBuilding = _gridMap.GetBuildingAt(_holdCoord.Value);
        Building currentBuilding = _gridMap.GetBuildingAt(_mouseSelectController.GetHoveredCell());
        if (holdBuilding == null || currentBuilding != holdBuilding)
        {
            _holdCoord = null;
            return;
        }

        // 입력 홀드 타이머는 게임 속도(일시정지/배속)와 무관하게 항상 같은 체감으로 동작해야 한다.
        _holdTimer += Time.unscaledDeltaTime;
        if (_holdTimer >= _moveHoldDuration)
        {
            // 홀드 완료 → 해당 건물을 선택하고 이동 모드로 진입한다.
            SelectExistingBuildingAt(_holdCoord.Value);
            EnterMoveMode();
            _holdCoord = null;
        }
    }

    private void HandlePlacementInput()
    {
        if (_placeAction == null || !_placeAction.action.WasPerformedThisFrame())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        ConfirmAtPointer();
    }

    public void ConfirmAtPointer()
    {
        if (_selectedBuilding != null)
        {
            TryConstructAt(_mouseSelectController.CurrentAnchor);
            return;
        }

        if (_moveSourceCoord.HasValue)
        {
            Building building = _gridMap.GetBuildingAt(_moveSourceCoord.Value);
            if (building != null)
                TryMoveSelectedTo(_mouseSelectController.GetHoveredAnchor(_mouseSelectController.CurrentFootprintShape));

            return;
        }

        SelectExistingBuildingAt(ResolveClickedCell());
    }

    // 성은 그리드 정중앙에 3x3만 점유하는데(Castle.RegisterCenterFootprint) 스프라이트는 그보다
    // 훨씬 높게 그려져 있어, 탑 몸통을 눌러도 셀 판정으로는 빈 땅이 나온다. 그 경우에 한해 성
    // 스프라이트 안인지 한 번 더 보고 성의 셀로 돌린다.
    // 셀에 건물이 있으면 그대로 두므로, 성 앞을 가리는 타워를 못 고르게 되는 일은 없다.
    private Vector3Int ResolveClickedCell()
    {
        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();

        if (_gridMap.GetBuildingAt(hoveredCell) != null)
            return hoveredCell;

        Castle castle = _gridMap.FindBuilding<Castle>(out Vector3Int castleCoord);

        if (castle == null || !castle.ContainsWorldPoint(_mouseSelectController.GetPointerWorldPoint()))
            return hoveredCell;

        return castleCoord;
    }

    public bool TryConstructAt(Vector3Int anchor)
    {
        if (_selectedBuilding == null)
            return false;

        if (_selectedBuilding is ResearchLab && _gridMap.HasBuilding<ResearchLab>())
            return false;

        // 건설은 낮에만 가능하다(기획 변경 - 새끼용 밤 배치 금지 요청을 계기로 전체 건물로 확장).
        if (!IsDayForBuildActions)
            return false;

        if (!_gridMap.CanConstructBuildingFootprint(anchor, _mouseSelectController.CurrentFootprintShape, _selectedBuilding, null))
            return false;

        IReadOnlyList<ResourceAmount> cost = _selectedBuilding.BuildCost;
        if (_resourceManager != null && !_resourceManager.CanAfford(cost))
            return false;

        Debug.Log($"[BuildingPlacementController] 건설 위치: {anchor}");
        _gridMap.ConstructBuilding(_selectedBuilding, anchor, _mouseSelectController.PreviewRotationSteps);

        if (_cycleManager != null)
            _gridMap.GetBuildingAt(anchor)?.SetConstructedCycle(_cycleManager.CurrentCycleNumber);

        if (_resourceManager != null)
            _resourceManager.Spend(cost);

        CancelBuildMode();
        return true;
    }

    // 건물 종류별 건설 비용 데이터를 조회 - UI_BuildingSlot.ResolveName과 동일한 타입 분기 패턴.
    // 아직 비용이 정의되지 않은 건물(예: Tower)은 빈 배열을 돌려줘 비용 없이 취급된다.
    private static IReadOnlyList<ResourceAmount> ResolveBuildCost(Building building)
    {
        if (building is Factory factory && factory.Data != null)
            return factory.Data.BuildCost;

        if (building is ResearchLab researchLab && researchLab.Data != null)
            return researchLab.Data.BuildCost;

        return System.Array.Empty<ResourceAmount>();
    }

    public bool TryMoveSelectedTo(Vector3Int anchor)
    {
        if (!_moveSourceCoord.HasValue)
            return false;

        Vector3Int prevCoord = _moveSourceCoord.Value;
        Building building = _gridMap.GetBuildingAt(prevCoord);
        if (building == null)
            return false;

        if (!_gridMap.MoveBuilding(prevCoord, anchor, _mouseSelectController.PreviewRotationSteps))
            return false;

        building.NotifyMoved();
        building.SetHighlighted(false, default);
        _moveSourceCoord = null;
        _mouseSelectController.SetPlacementActive(false);
        _mouseSelectController.ClearHighlights();
        return true;
    }

    public void SelectExistingBuildingAt(Vector3Int coord)
    {
        Building building = _gridMap.GetBuildingAt(coord);

        CancelBuildMode();
        CancelMove();
        Deselect();

        if (building != null)
        {
            _selectedExistingBuildingCoord = coord;
            _mouseSelectController.HighlightSelection(_gridMap.GetOccupiedCoords(coord));
            building.SetHighlighted(true, _mouseSelectController.SelectionHighlightColor);
            ShowRangeIndicatorFor(building);
        }

        // 이미 선택된 건물을 다시 눌러도 매번 발화한다. SelectedBuilding은 파생 getter라
        // 폴링으로는 재클릭을 관측할 수 없다 - 위에서 Deselect() 후 같은 프레임에 다시 선택되므로
        // 구독자 입장에선 값이 바뀐 적이 없는 것으로 보인다.
        // 선택 해제(building == null)도 알려야 하므로 early return 하지 않는다.
        BuildingSelected.Invoke(building);
    }

    private void ShowRangeIndicatorFor(Building building)
    {
        ShowAttackRangeIndicatorFor(building);
        ShowBuffRangeIndicatorFor(building);
    }

    // 선택한 건물이 공격 가능한 타워면 실제 판정(TowerAttack.IsWithinAttackRange)과 같은 타원으로 사거리를 표시한다.
    private void ShowAttackRangeIndicatorFor(Building building)
    {
        if (_rangeIndicator == null ||
            !(building is Tower tower) ||
            tower.Data == null ||
            !tower.Data.CanAttack ||
            tower.Attack == null)
        {
            _rangeIndicator?.Hide();
            return;
        }

        _rangeIndicator.SetCenter(tower.transform.position);
        _rangeIndicator.Show(tower.Attack.EffectiveRange, tower.Attack.EffectiveRange * IsometricMath.RADIUS_Y_RATIO);
    }

    // 오라 타워는 현재 유효 반경을, 기존 새끼용은 BabyDragonBuffSystem과 같은 BuffRadius를 표시한다.
    private void ShowBuffRangeIndicatorFor(Building building)
    {
        if (_buffRangeIndicator == null)
        {
            return;
        }

        if (building is Tower tower &&
            tower.Data is ITowerAuraDataProvider provider &&
            provider.HasTowerAura)
        {
            if (TowerAuraSystem.TryGetActiveAura(
                tower,
                out _,
                out float auraRadius))
            {
                ShowBuffRange(tower.transform.position, auraRadius);
            }
            else
            {
                _buffRangeIndicator.Hide();
            }

            return;
        }

        if (building is BabyDragonTower babyDragon &&
            babyDragon.DragonData != null &&
            babyDragon.DragonData.BuffRadius > 0f)
        {
            ShowBuffRange(
                babyDragon.transform.position,
                babyDragon.DragonData.BuffRadius);
            return;
        }

        _buffRangeIndicator.Hide();
    }

    private void RefreshSelectedRangeIndicator()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
        {
            return;
        }

        Building building =
            _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);

        if (building != null)
        {
            ShowRangeIndicatorFor(building);
        }
    }

    private void ShowBuffRange(Vector3 center, float radiusX)
    {
        float radiusY =
            radiusX * IsometricMath.RADIUS_Y_RATIO;

        _buffRangeIndicator.SetCenter(center);
        _buffRangeIndicator.Show(radiusX, radiusY);
    }

    public void CancelAll()
    {
        CancelBuildMode();
        CancelMove();
        Deselect();
    }

    // 건설 모드 진입 시 이미 배치된 건물의 타일을 표시 - 어떤 땅이 비어있는지 시각적으로 확인 가능
    public void ShowOccupiedTiles()
    {
        _isOccupiedOverlayVisible = true;
        RefreshOccupiedOverlay();
    }

    public void HideOccupiedTiles()
    {
        _isOccupiedOverlayVisible = false;
        _mouseSelectController.ClearOccupiedOverlay();
    }

    // 건설 모드가 열려있는 동안 건물 배치/철거/이동에 맞춰 표시된 타일을 최신 상태로 갱신
    private void RefreshOccupiedOverlay()
    {
        if (!_isOccupiedOverlayVisible)
            return;

        _mouseSelectController.ShowOccupiedOverlay(_gridMap.GetAllOccupiedCoords());
    }

}
