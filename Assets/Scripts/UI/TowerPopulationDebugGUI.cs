using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Canvas와 무관하게 현재 선택 상태에 맞는 디버그 정보를 보여주는 IMGUI 도구다.
/// 원래 타워/생산시설 인구용이었으나, 새끼용 알 지급/배치용(구 BabyDragonDebugGUI)을
/// 여기로 통합했다 - 연구소용은 ResearchLabDebugGUI로 별도 유지한다(통합하지 않음).
/// 창을 각자 따로 띄우면 선택이 바뀌어도 이전 창이 안 닫혀 서로 겹쳐 보이는 문제가
/// 있었기 때문에, 매 프레임 현재 선택 상태(_selectionKind)를 하나로 판정해 정확히
/// 창 하나만 그린다. 아무것도 선택 안 된 기본 상태에는 새끼용 알 창을 보여준다.
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

    [Header("Baby Dragon")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private BabyDragonPlacementCoordinator _placementCoordinator;

    private SelectionKind _selectionKind;
    private TowerPopulation _selectedTowerPopulation;
    private FactoryPopulation _selectedFactoryPopulation;
    private Vector2 _babyDragonScrollPosition;
    private bool _isVisible;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    // 매 프레임 현재 선택된 건물 타입 하나로 _selectionKind를 다시 판정한다 - 이전에 선택했던
    // 다른 타입의 창이 "기억"되어 계속 떠 있는 일이 없도록, 값을 유지하지 않고 항상 새로 계산한다.
    private void Update()
    {
        HandleVisibilityToggle();

        _selectionKind = ResolveSelectionKind();

        if (!_isVisible)
        {
            return;
        }

        HandleKeyInput();
    }

    // F1로 디버그 창 전체를 켜고 끈다(토글), ESC는 항상 닫기만 한다.
    private void HandleVisibilityToggle()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            _isVisible = !_isVisible;
        }

        if (_isVisible && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            _isVisible = false;
        }
    }

    private SelectionKind ResolveSelectionKind()
    {
        if (_buildingPlacementController == null)
        {
            return SelectionKind.None;
        }

        Building selectedBuilding = _buildingPlacementController.SelectedBuilding;

        if (selectedBuilding is Tower selectedTower)
        {
            _selectedTowerPopulation = selectedTower.GetComponent<TowerPopulation>();
            // 새끼용처럼 TowerPopulation이 없는 Tower 파생 건물은 이 창을 그릴 게 없으니
            // None으로 떨어뜨려 기본 창(새끼용 알)이 보이게 한다.
            return _selectedTowerPopulation != null ? SelectionKind.Tower : SelectionKind.None;
        }

        if (selectedBuilding is Factory selectedFactory)
        {
            _selectedFactoryPopulation = selectedFactory.GetComponent<FactoryPopulation>();
            return SelectionKind.Factory;
        }

        return SelectionKind.None;
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
        if (!_isVisible)
        {
            return;
        }

        switch (_selectionKind)
        {
            case SelectionKind.Tower:
                DrawWindow(DrawTowerInformation, DrawTowerControls);
                break;
            case SelectionKind.Factory:
                DrawWindow(DrawFactoryInformation, DrawFactoryControls);
                break;
            default:
                DrawBabyDragonWindow();
                break;
        }
    }

    // --- 타워 / 생산시설 인구 ---

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

    // --- 새끼용 알 (구 BabyDragonDebugGUI) ---

    // 타워/생산시설 인구 창과 같은 자리(우측 상단)·같은 크기를 재사용한다 - 둘 다 상호
    // 배타적으로 그려지므로(OnGUI의 switch) 자리를 공유해도 겹치지 않고, 화면 하단의
    // 점령 패널 등 다른 UI와도 겹치지 않는다.
    private void DrawBabyDragonWindow()
    {
        if (_gameManager == null || _eggInventorySystem == null)
        {
            return;
        }

        Rect windowRect = new Rect(
            Screen.width - WINDOW_WIDTH - WINDOW_MARGIN,
            WINDOW_MARGIN,
            WINDOW_WIDTH,
            WINDOW_HEIGHT);

        GUILayout.BeginArea(windowRect, GUI.skin.window);
        _babyDragonScrollPosition = GUILayout.BeginScrollView(_babyDragonScrollPosition);
        DrawEggGrantButtons();
        GUILayout.Space(WINDOW_MARGIN);
        DrawEggList();
        GUILayout.Space(WINDOW_MARGIN);
        DrawPlacementButtons();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawEggGrantButtons()
    {
        GUILayout.Label("새끼용 디버그 - 알 지급");

        foreach (DragonType dragonType in System.Enum.GetValues(typeof(DragonType)))
        {
            if (GUILayout.Button($"+ {dragonType} 알"))
            {
                _eggInventorySystem.GrantEgg(dragonType);
            }
        }
    }

    private void DrawEggList()
    {
        GUILayout.Label("보유 중인 알");

        List<DragonEgg> eggs = _gameManager.CurrentRun.DragonEggs;
        if (eggs.Count == 0)
        {
            GUILayout.Label("(없음)");
            return;
        }

        foreach (DragonEgg egg in eggs)
        {
            GUILayout.Label($"{egg.DragonType}: {DescribeEggProgress(egg)}");
        }
    }

    private string DescribeEggProgress(DragonEgg egg)
    {
        BabyDragonDataCatalog dataCatalog = _eggInventorySystem.DataCatalog;
        if (dataCatalog == null || !dataCatalog.TryResolve(egg.DragonType, out BabyDragonData data))
        {
            return $"{egg.FedDayCount}일째 (카탈로그 없음)";
        }

        return $"{egg.FedDayCount}/{data.DaysToHatch}일";
    }

    private void DrawPlacementButtons()
    {
        if (_placementCoordinator == null)
        {
            return;
        }

        GUILayout.Label("배치 시작 (보유 개체)");

        // BeginPlacement 확정이 같은 프레임의 다음 OnGUI 패스에서 리스트를 변경할 수 있으므로
        // (OnGUI는 프레임당 여러 번 호출됨) 스냅샷을 순회한다.
        foreach (BabyDragon babyDragon in _gameManager.CurrentRun.BabyDragons.ToArray())
        {
            if (babyDragon.IsInTower)
            {
                continue;
            }

            if (GUILayout.Button($"배치: {babyDragon.DragonType}"))
            {
                _placementCoordinator.BeginPlacement(babyDragon);
            }
        }
    }
}
