using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canvas와 무관하게 새끼용 알 지급/배치를 조작하는 IMGUI 디버그 도구다(구 BabyDragonDebugGUI).
/// 원래 타워/생산시설 인구 조작도 여기 있었으나, 정식 건물 창(UI_PopulationAllocationWindow,
/// 이슈 #110)이 그 역할을 대체하면서 제거했다 - 연구소용은 ResearchLabDebugGUI로 별도 유지한다.
/// 클래스명이 내용과 어긋나지만 여러 씬과 프리팹이 이 스크립트를 참조하고 있어 이름은 유지한다.
/// </summary>
public class TowerPopulationDebugGUI : MonoBehaviour
{
    private const float WINDOW_WIDTH = 320f;
    private const float WINDOW_HEIGHT = 250f;
    private const float WINDOW_MARGIN = 20f;

    [Header("Baby Dragon")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private BabyDragonPlacementCoordinator _placementCoordinator;

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
    private void OnGUI()
    {
    private void OnGUI()
    {
        DrawBabyDragonWindow();
    }

    // 화면 우측 상단 - 화면 하단의 점령 패널 등 다른 UI와 겹치지 않는 자리다.
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
