using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Canvas와 무관하게 선택한 타워/생산시설의 인구 배치 상태를 확인하는 IMGUI 디버그 도구다.
/// 마지막으로 선택한 타워 또는 생산시설을 유지해 IMGUI 클릭이 맵 선택을 해제해도 테스트를 계속할 수 있다.
/// </summary>
public class TowerPopulationDebugGUI : MonoBehaviour
{
    private enum SelectionKind
    {
        None,
        Tower,
        Factory,
    }

    private const int POPULATION_STEP = 1;
    private const float PERCENT_MULTIPLIER = 100f;
    private const float WINDOW_WIDTH = 320f;
    private const float WINDOW_HEIGHT = 250f;
    private const float WINDOW_MARGIN = 20f;

    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;

    private SelectionKind _selectionKind;
    private TowerPopulation _selectedTowerPopulation;
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
        if (selectedBuilding is Tower selectedTower)
        {
            _selectedTowerPopulation = selectedTower.GetComponent<TowerPopulation>();
            _selectionKind = SelectionKind.Tower;
        }
        else if (selectedBuilding is Factory selectedFactory)
        {
            _selectedFactoryPopulation = selectedFactory.GetComponent<FactoryPopulation>();
            _selectionKind = SelectionKind.Factory;
        }

        HandleKeyInput();
    }

    private void HandleKeyInput()
    {
        if (!IsDay || Keyboard.current == null)
        {
            return;
        }

        if (_selectionKind == SelectionKind.Tower &&
            _selectedTowerPopulation != null &&
            _selectedTowerPopulation.IsInitialized)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                _selectedTowerPopulation.TryAssign(POPULATION_STEP);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                _selectedTowerPopulation.TryUnassign(POPULATION_STEP);
            }
        }
        else if (_selectionKind == SelectionKind.Factory &&
            _selectedFactoryPopulation != null &&
            _selectedFactoryPopulation.IsInitialized)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                _selectedFactoryPopulation.TryAssign(POPULATION_STEP);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                _selectedFactoryPopulation.TryUnassign(POPULATION_STEP);
            }
        }
    }

    private void OnGUI()
    {
        if (_selectionKind == SelectionKind.Tower && _selectedTowerPopulation != null)
        {
            DrawWindow(DrawTowerInformation, DrawTowerControls);
        }
        else if (_selectionKind == SelectionKind.Factory && _selectedFactoryPopulation != null)
        {
            DrawWindow(DrawFactoryInformation, DrawFactoryControls);
        }
    }

    private void DrawWindow(System.Action drawInformation, System.Action drawControls)
    {
        Rect windowRect = new Rect(
            Screen.width - WINDOW_WIDTH - WINDOW_MARGIN,
            WINDOW_MARGIN,
            WINDOW_WIDTH,
            WINDOW_HEIGHT);

        GUILayout.BeginArea(windowRect, GUI.skin.window);
        drawInformation();
        drawControls();
        GUILayout.EndArea();
    }

    private void DrawTowerInformation()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;

        int staffingPercent = Mathf.RoundToInt(
            _selectedTowerPopulation.StaffingRatio * PERCENT_MULTIPLIER);

        GUILayout.Label("타워 인구 테스트 (1: 배치, 2: 회수)");
        GUILayout.Label($"대상: {_selectedTowerPopulation.name}");
        GUILayout.Label(
            $"전체 {state.MaxPopulation} / 할당 {state.AssignedPopulation} / 가용 {state.AvailablePopulation}");
        GUILayout.Label(
            $"타워 {_selectedTowerPopulation.AssignedPopulation} / {_selectedTowerPopulation.Capacity}");
        GUILayout.Label($"충원율: {staffingPercent}%");
        GUILayout.Label(IsDay ? "낮: 변경 가능" : "밤: 변경 불가");
        GUILayout.Space(WINDOW_MARGIN);
    }

    private void DrawTowerControls()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;

        bool canEdit = IsDay && _selectedTowerPopulation.IsInitialized;
        bool previousEnabled = GUI.enabled;

        GUI.enabled =
            canEdit &&
            _selectedTowerPopulation.AvailableCapacity >= POPULATION_STEP &&
            state.AvailablePopulation >= POPULATION_STEP;

        if (GUILayout.Button("+1 배치"))
        {
            _selectedTowerPopulation.TryAssign(POPULATION_STEP);
        }

        GUI.enabled =
            canEdit &&
            _selectedTowerPopulation.AssignedPopulation >= POPULATION_STEP;

        if (GUILayout.Button("-1 회수"))
        {
            _selectedTowerPopulation.TryUnassign(POPULATION_STEP);
        }

        if (GUILayout.Button("전체 회수"))
        {
            int assignedPopulation = _selectedTowerPopulation.AssignedPopulation;
            _selectedTowerPopulation.TryUnassign(assignedPopulation);
        }

        GUI.enabled = previousEnabled;
    }

    private void DrawFactoryInformation()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;

        int staffingPercent = Mathf.RoundToInt(
            _selectedFactoryPopulation.StaffingRatio * PERCENT_MULTIPLIER);

        GUILayout.Label("생산시설 인구 테스트 (1: 배치, 2: 회수)");
        GUILayout.Label($"대상: {_selectedFactoryPopulation.name}");
        GUILayout.Label(
            $"전체 {state.MaxPopulation} / 할당 {state.AssignedPopulation} / 가용 {state.AvailablePopulation}");
        GUILayout.Label(
            $"생산시설 {_selectedFactoryPopulation.AssignedPopulation} / {_selectedFactoryPopulation.Capacity}");
        GUILayout.Label($"충원율: {staffingPercent}%");
        GUILayout.Label(IsDay ? "낮: 변경 가능" : "밤: 변경 불가");
        GUILayout.Space(WINDOW_MARGIN);
    }

    private void DrawFactoryControls()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;

        bool canEdit = IsDay && _selectedFactoryPopulation.IsInitialized;
        bool previousEnabled = GUI.enabled;

        GUI.enabled =
            canEdit &&
            _selectedFactoryPopulation.AvailableCapacity >= POPULATION_STEP &&
            state.AvailablePopulation >= POPULATION_STEP;

        if (GUILayout.Button("+1 배치"))
        {
            _selectedFactoryPopulation.TryAssign(POPULATION_STEP);
        }

        GUI.enabled =
            canEdit &&
            _selectedFactoryPopulation.AssignedPopulation >= POPULATION_STEP;

        if (GUILayout.Button("-1 회수"))
        {
            _selectedFactoryPopulation.TryUnassign(POPULATION_STEP);
        }

        if (GUILayout.Button("전체 회수"))
        {
            int assignedPopulation = _selectedFactoryPopulation.AssignedPopulation;
            _selectedFactoryPopulation.TryUnassign(assignedPopulation);
        }

        GUI.enabled = previousEnabled;
    }
}
