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

    [Tooltip("타워를 이만큼(초) 꾹 누르고 있으면 이동 모드로 진입한다.")]
    [SerializeField]
    private float _moveHoldDuration = 2f;

    private Building _selectedBuilding;
    private Vector3Int? _selectedExistingBuildingCoord;
    private Vector3Int? _moveSourceCoord;
    private bool _isOccupiedOverlayVisible;

    private float _holdTimer;
    private Vector3Int? _holdCoord;

    public bool IsMoving => _moveSourceCoord.HasValue;

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

        _gridMap.OnCellChanged += HandleCellChanged;
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged -= HandleCellChanged;
    }

    private void HandleCellChanged(GridCell cell) => RefreshOccupiedOverlay();

    private void OnEnable()
    {
        if (_placeAction != null)
            _placeAction.action.Enable();

        if (_cancelMoveAction != null)
            _cancelMoveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_placeAction != null)
            _placeAction.action.Disable();

        if (_cancelMoveAction != null)
            _cancelMoveAction.action.Disable();
    }

    private void Update()
    {
        HandlePlacementInput();
        HandleMoveCancelInput();
        HandleLongPressMove();
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

        _gridMap.RemoveBuilding(_selectedExistingBuildingCoord.Value);

        if (building != null)
            building.SetHighlighted(false, default);

        _selectedExistingBuildingCoord = null;
        _mouseSelectController.ClearHighlights();
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
    }

    private void HandleMoveCancelInput()
    {
        if (!_moveSourceCoord.HasValue)
            return;

        if (_cancelMoveAction != null && _cancelMoveAction.action.WasPerformedThisFrame())
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
                TryMoveSelectedTo(_mouseSelectController.GetHoveredAnchor(building.FootprintShape));

            return;
        }

        SelectExistingBuildingAt(_mouseSelectController.GetHoveredCell());
    }

    public bool TryConstructAt(Vector3Int anchor)
    {
        if (_selectedBuilding == null)
            return false;

        if (!_gridMap.CanConstructBuildingFootprint(anchor, _selectedBuilding.FootprintShape, _selectedBuilding, null))
            return false;

        Debug.Log($"[BuildingPlacementController] 건설 위치: {anchor}");
        _gridMap.ConstructBuilding(_selectedBuilding, anchor);
        CancelBuildMode();
        return true;
    }

    public bool TryMoveSelectedTo(Vector3Int anchor)
    {
        if (!_moveSourceCoord.HasValue)
            return false;

        Vector3Int prevCoord = _moveSourceCoord.Value;
        Building building = _gridMap.GetBuildingAt(prevCoord);
        if (building == null)
            return false;

        if (!_gridMap.MoveBuilding(prevCoord, anchor))
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
