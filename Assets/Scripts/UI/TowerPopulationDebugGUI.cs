using UnityEngine;

/// <summary>
/// Canvas와 무관하게 선택한 타워의 인구 배치 상태를 확인하는 IMGUI 디버그 도구다.
/// 마지막으로 선택한 타워를 유지해 IMGUI 클릭이 맵 선택을 해제해도 테스트를 계속할 수 있다.
/// </summary>
public class TowerPopulationDebugGUI : MonoBehaviour
{
    private const int POPULATION_STEP = 1;
    private const float PERCENT_MULTIPLIER = 100f;
    private const float WINDOW_WIDTH = 320f;
    private const float WINDOW_HEIGHT = 250f;
    private const float WINDOW_MARGIN = 20f;

    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;

    private TowerPopulation _selectedTowerPopulation;

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
        }
    }

    private void OnGUI()
    {
        if (_selectedTowerPopulation == null)
        {
            return;
        }

        Rect windowRect = new Rect(
            Screen.width - WINDOW_WIDTH - WINDOW_MARGIN,
            WINDOW_MARGIN,
            WINDOW_WIDTH,
            WINDOW_HEIGHT);

        GUILayout.BeginArea(windowRect, GUI.skin.window);
        DrawPopulationInformation();
        DrawPopulationControls();
        GUILayout.EndArea();
    }

    private void DrawPopulationInformation()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;

        int staffingPercent = Mathf.RoundToInt(
            _selectedTowerPopulation.StaffingRatio * PERCENT_MULTIPLIER);

        GUILayout.Label("타워 인구 테스트");
        GUILayout.Label($"대상: {_selectedTowerPopulation.name}");
        GUILayout.Label(
            $"전체 {state.MaxPopulation} / 할당 {state.AssignedPopulation} / 가용 {state.AvailablePopulation}");
        GUILayout.Label(
            $"타워 {_selectedTowerPopulation.AssignedPopulation} / {_selectedTowerPopulation.Capacity}");
        GUILayout.Label($"충원율: {staffingPercent}%");
        GUILayout.Label(IsDay ? "낮: 변경 가능" : "밤: 변경 불가");
        GUILayout.Space(WINDOW_MARGIN);
    }

    private void DrawPopulationControls()
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
}
