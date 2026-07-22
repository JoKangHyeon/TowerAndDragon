using UnityEngine;
using UnityEngine.InputSystem;

// 생산시설 인구 배치 디버그 테스트 도구. 빌드 모드에서 생산시설을 클릭해 선택하면
// 1번 키로 인구 1명을 배치하고, 2번 키로 1명을 회수한다.
// 마지막으로 선택한 생산시설을 유지해 OnGUI 클릭이 맵 선택을 해제해도 테스트를 계속할 수 있다.
public class FactoryPopulationDebugGUI : MonoBehaviour
{
    private const int POPULATION_STEP = 1;
    private const float WINDOW_WIDTH = 320f;
    private const float WINDOW_HEIGHT = 220f;
    private const float WINDOW_MARGIN = 20f;

    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;

    private Factory _selectedFactory;
    private FactoryPopulation _selectedFactoryPopulation;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private void Update()
    {
        if (_buildingPlacementController == null)
        {
            return;
        }

        Building selectedBuilding = _buildingPlacementController.SelectedBuilding;
        if (selectedBuilding is Factory selectedFactory)
        {
            _selectedFactory = selectedFactory;
            _selectedFactoryPopulation = selectedFactory.GetComponent<FactoryPopulation>();
        }

        HandleKeyInput();
    }

    private void HandleKeyInput()
    {
        if (_selectedFactoryPopulation == null || !IsDay || !_selectedFactoryPopulation.IsInitialized || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            _selectedFactoryPopulation.TryAssign(POPULATION_STEP);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            _selectedFactoryPopulation.TryUnassign(POPULATION_STEP);
        }
    }

    private void OnGUI()
    {
        if (_selectedFactoryPopulation == null || _selectedFactory == null)
        {
            return;
        }

        Rect windowRect = new Rect(
            Screen.width - WINDOW_WIDTH - WINDOW_MARGIN,
            WINDOW_MARGIN,
            WINDOW_WIDTH,
            WINDOW_HEIGHT);

        GUILayout.BeginArea(windowRect, GUI.skin.window);
        DrawInfo();
        GUILayout.EndArea();
    }

    private const float PERCENT_MULTIPLIER = 100f;

    private void DrawInfo()
    {
        PopulationState state = _populationManager != null ? _populationManager.CurrentState : default;
        int staffingPercent = Mathf.RoundToInt(_selectedFactoryPopulation.StaffingRatio * PERCENT_MULTIPLIER);

        GUILayout.Label("Factory Population Test (1: Assign, 2: Unassign)");
        GUILayout.Label($"Target: {_selectedFactory.name}");
        GUILayout.Label($"Total {state.MaxPopulation} / Assigned {state.AssignedPopulation} / Available {state.AvailablePopulation}");
        GUILayout.Label($"Factory {_selectedFactoryPopulation.AssignedPopulation} / {_selectedFactoryPopulation.Capacity} (Staffing {staffingPercent}%)");
        GUILayout.Label(IsDay ? "Day: Editable" : "Night: Locked");
    }
}
