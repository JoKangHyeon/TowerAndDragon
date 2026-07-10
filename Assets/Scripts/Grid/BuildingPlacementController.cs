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

    private Building _selectedBuilding;
    private Vector3Int? _selectedExistingBuildingCoord;
    private Vector3Int? _moveSourceCoord;

    public bool IsMoving => _moveSourceCoord.HasValue;

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
    }

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
    }

    public void SelectBuilding(Building prefab)
    {
        if (prefab == null)
            return;

        _selectedBuilding = prefab;
        DeselectExistingBuilding();
        CancelMove();
        _mouseSelectController.SetSelectedBuilding(prefab);
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

        _gridMap.RemoveBuilding(_selectedExistingBuildingCoord.Value);
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

        _moveSourceCoord = _selectedExistingBuildingCoord;
        _selectedExistingBuildingCoord = null;

        _mouseSelectController.SetSelectedBuilding(building);
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

    private void DeselectExistingBuilding()
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

    private void HandlePlacementInput()
    {
        if (_placeAction == null || !_placeAction.action.WasPerformedThisFrame())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (_selectedBuilding != null)
        {
            HandleConstructionClick();
            return;
        }

        if (_moveSourceCoord.HasValue)
        {
            HandleMoveTargetClick();
            return;
        }

        HandleExistingBuildingSelectClick();
    }

    private void HandleConstructionClick()
    {
        if (!_mouseSelectController.CanConstruct)
            return;

        Debug.Log($"[BuildingPlacementController] 건설 위치: {_mouseSelectController.CurrentAnchor}");
        _gridMap.ConstructBuilding(_selectedBuilding, _mouseSelectController.CurrentAnchor);
        CancelBuildMode();
    }

    private void HandleMoveTargetClick()
    {
        Vector3Int prevCoord = _moveSourceCoord.Value;
        Building building = _gridMap.GetBuildingAt(prevCoord);
        if (building == null)
            return;

        Vector3Int anchor = _mouseSelectController.GetHoveredAnchor(building.FootprintShape);

        if (!_gridMap.MoveBuilding(prevCoord, anchor))
            return; // 유효하지 않은 자리 - 이동 모드 유지, 다른 곳 다시 클릭 가능

        building.SetHighlighted(false, default);
        _moveSourceCoord = null;
        _mouseSelectController.SetPlacementActive(false);
        _mouseSelectController.ClearHighlights();
    }

    private void HandleExistingBuildingSelectClick()
    {
        Vector3Int coord = _mouseSelectController.GetHoveredCell();
        Building building = _gridMap.GetBuildingAt(coord);

        DeselectExistingBuilding();

        if (building == null)
            return;

        _selectedExistingBuildingCoord = coord;
        _mouseSelectController.HighlightSelection(_gridMap.GetOccupiedCoords(coord));
        building.SetHighlighted(true, _mouseSelectController.SelectionHighlightColor);
    }

}
