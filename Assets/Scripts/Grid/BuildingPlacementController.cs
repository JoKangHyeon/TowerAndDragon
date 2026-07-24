using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
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

    [Tooltip("타워를 이만큼(초) 꾹 누르고 있으면 이동 모드로 진입한다.")]
    [SerializeField]
    private float _moveHoldDuration = 2f;

    // 건물 철거 시 건설 비용 중 돌려주는 비율 - 낮밤 사이클이 한 번도 돌지 않은 당일 철거는 전액, 그 외엔 일부만 환급.
    private const float DEMOLISH_REFUND_RATIO_SAME_DAY = 1f;
    private const float DEMOLISH_REFUND_RATIO_LATE = 0.7f;

    private Building _selectedBuilding;
    private Vector3Int? _selectedExistingBuildingCoord;
    private Vector3Int? _moveSourceCoord;
    private bool _isOccupiedOverlayVisible;

    private float _holdTimer;
    private Vector3Int? _holdCoord;

    public bool IsMoving => _moveSourceCoord.HasValue;

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
        // 다른 모드(점령 등)가 클릭을 점유 중이면 건물 배치/선택 입력을 처리하지 않는다.
        if (InputSuppressed)
            return;

        HandlePlacementInput();
        HandleCancelInput();
        HandleLongPressMove();
        HandleRotateInput();
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

    public void CancelBuildMode()
    {
        _selectedBuilding = null;
        _mouseSelectController.SetPlacementActive(false);
    }

    public void RemoveSelectedBuilding()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
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

        if (!building.IsMoveable)
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
    }

    // _cancelMoveAction(우클릭)으로 새 건물 배치 미리보기와 기존 건물 이동 미리보기를 모두 취소한다.
    private void HandleCancelInput()
    {
        if (_cancelMoveAction == null || !_cancelMoveAction.action.WasPerformedThisFrame())
            return;

        if (_selectedBuilding != null)
            CancelBuildMode();
        else if (_moveSourceCoord.HasValue)
            CancelMove();
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

            if (!overUI && building != null && building.IsMoveable)
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

        _holdTimer += Time.deltaTime;
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

        SelectExistingBuildingAt(_mouseSelectController.GetHoveredCell());
    }

    public bool TryConstructAt(Vector3Int anchor)
    {
        if (_selectedBuilding == null)
            return false;

        if (_selectedBuilding is ResearchLab && _gridMap.HasBuilding<ResearchLab>())
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

        if (building == null)
            return;

        _selectedExistingBuildingCoord = coord;
        _mouseSelectController.HighlightSelection(_gridMap.GetOccupiedCoords(coord));
        building.SetHighlighted(true, _mouseSelectController.SelectionHighlightColor);
        ShowRangeIndicatorFor(building);
    }

    // 선택한 건물이 공격 가능한 타워면 실제 판정(TowerAttack.IsWithinAttackRange)과 같은 타원으로 사거리를 표시한다.
    private void ShowRangeIndicatorFor(Building building)
    {
        if (_rangeIndicator == null || !(building is Tower tower) || !tower.Data.CanAttack)
            return;

        _rangeIndicator.SetCenter(tower.transform.position);
        _rangeIndicator.Show(tower.Data.Attack.Range, tower.Data.Attack.Range * IsometricMath.RADIUS_Y_RATIO);
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
