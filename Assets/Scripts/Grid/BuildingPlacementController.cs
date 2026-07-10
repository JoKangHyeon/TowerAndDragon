using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingPlacementController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private BuildingCatalog _buildingCatalog;

    [SerializeField]
    private InputActionReference _placeAction;

    private Building _selectedBuilding;

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

        if (_buildingCatalog == null)
        {   Debug.LogWarning($"[BuildingPlacementController] BuildingCatalog 인스펙터 연결 필요");
            return;
        }

        if (_placeAction == null)
        {   Debug.LogWarning($"[BuildingPlacementController] PlaceAction 인스펙터 연결 필요");
            return;
        }
    }

    private void OnEnable()
    {
        if (_placeAction != null)
            _placeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_placeAction != null)
            _placeAction.action.Disable();
    }

    private void Update()
    {
        HandleSelectionInput();
        HandlePlacementInput();
    }


    // 건물 종류 UI 미정 -> 키보드로 건물 선택 => 추후 수정
    private void HandleSelectionInput()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SelectBuilding<Tower>();
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SelectBuilding<Building>();
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SelectBuilding<Factory>();
    }

    private void SelectBuilding<T>() where T : Building
    {
        Building prefab = _buildingCatalog.GetPrefab<T>();
        if (prefab == null)
            return;
        
        _selectedBuilding = prefab;
        _mouseSelectController.SetSelectedBuilding(prefab);
        Debug.Log($"[BuildingPlacementController] 선택된 건물: {prefab.name}");
    }

    private void HandlePlacementInput()
    {
        if (_placeAction == null || !_placeAction.action.WasPerformedThisFrame())
            return;

        if (_selectedBuilding == null || !_mouseSelectController.CanConstruct)
            return;
        
        Debug.Log($"[BuildingPlacementController] 건설 위치: {_mouseSelectController.CurrentAnchor}");
        _gridMap.ConstructBuilding(_selectedBuilding, _mouseSelectController.CurrentAnchor);
    }

}
