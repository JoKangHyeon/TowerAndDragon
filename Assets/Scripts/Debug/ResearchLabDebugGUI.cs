using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] Canvas와 무관하게 선택한 연구소의 인구와 연구 상태를 조작하는 IMGUI 디버그 도구다.
/// 타워 인구 GUI와 마찬가지로 마지막으로 선택한 연구소를 유지한다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public sealed class ResearchLabDebugGUI : MonoBehaviour
{
#if UNITY_EDITOR
    private const int POPULATION_STEP = 1;
    private const float WINDOW_WIDTH = 430f;
    private const float WINDOW_HEIGHT = 570f;
    private const float WINDOW_MARGIN = 20f;
    private const float WINDOW_TOP = 290f;
    private const string COST_SEPARATOR = " / ";
    private const string UNKNOWN_RESOURCE_LOC_KEY = "research_unknown_resource";
    private const string RP_COST_LOC_KEY = "research_rp_cost";
    private const string RESOURCE_COST_LOC_KEY = "research_resource_cost";

    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ResourceManager _resourceManager;

    private ResearchLab _selectedLab;
    private ResearchBranch _selectedBranch = ResearchBranch.Production;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private void Update()
    {
        if (_buildingPlacementController == null)
        {
            return;
        }

        if (_buildingPlacementController.SelectedBuilding is ResearchLab selectedLab)
        {
            _selectedLab = selectedLab;
        }

        HandleCloseInput();
    }

    // ESC로 창을 닫는다. 맵의 연구소 선택이 남아 있으면 다음 프레임 Update에서 다시 열리므로
    // 건물 선택까지 함께 해제한다. (다시 열려면 연구소를 클릭)
    private void HandleCloseInput()
    {
        if (_selectedLab == null || Keyboard.current == null)
        {
            return;
        }

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        _selectedLab = null;
        _buildingPlacementController.Deselect();
    }

    private void OnGUI()
    {
        if (_selectedLab == null || _selectedLab.Population == null)
        {
            return;
        }

        Rect windowRect = new Rect(
            Screen.width - WINDOW_WIDTH - WINDOW_MARGIN,
            WINDOW_TOP,
            WINDOW_WIDTH,
            WINDOW_HEIGHT);

        GUILayout.BeginArea(windowRect, GUI.skin.window);
        DrawPopulationInformation();
        DrawPopulationControls();
        GUILayout.Space(WINDOW_MARGIN);
        DrawResearchTree();
        GUILayout.EndArea();
    }

    private void DrawPopulationInformation()
    {
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;
        ResearchLabPopulation labPopulation = _selectedLab.Population;

        GUILayout.Label("Research Lab Test (ESC: Close)");
        GUILayout.Label($"Target: {_selectedLab.name}");
        GUILayout.Label($"Research Points: {_researchManager.ResearchPoints}");
        GUILayout.Label(
            $"Total {state.MaxPopulation} / Assigned {state.AssignedPopulation} / Available {state.AvailablePopulation}");
        GUILayout.Label(
            $"Research Lab {labPopulation.AssignedPopulation} / {labPopulation.Capacity}");
        GUILayout.Label(IsDay ? "Day: Editable" : "Night: Locked");
        GUILayout.Space(WINDOW_MARGIN);
    }

    private void DrawPopulationControls()
    {
        ResearchLabPopulation labPopulation = _selectedLab.Population;
        PopulationState state = _populationManager != null
            ? _populationManager.CurrentState
            : default;
        bool previousEnabled = GUI.enabled;
        bool canEdit = IsDay && labPopulation.IsInitialized;

        GUI.enabled =
            canEdit &&
            labPopulation.AvailableCapacity >= POPULATION_STEP &&
            state.AvailablePopulation >= POPULATION_STEP;
        if (GUILayout.Button("+1 Assign"))
        {
            labPopulation.TryAssign(POPULATION_STEP);
        }

        GUI.enabled =
            canEdit &&
            labPopulation.AssignedPopulation >= POPULATION_STEP;
        if (GUILayout.Button("-1 Unassign"))
        {
            labPopulation.TryUnassign(POPULATION_STEP);
        }

        if (GUILayout.Button("Release All"))
        {
            labPopulation.TryUnassign(labPopulation.AssignedPopulation);
        }

        GUI.enabled = previousEnabled;
    }

    private void DrawResearchTree()
    {
        DrawBranchButtons();
        GUILayout.Space(WINDOW_MARGIN);

        bool hasNode = false;
        if (_researchManager != null && _researchManager.Tree != null)
        {
            foreach (ResearchNodeData node in _researchManager.Tree.Nodes)
            {
                if (node == null || node.Branch != _selectedBranch)
                {
                    continue;
                }

                DrawResearchNode(node);
                hasNode = true;
            }
        }

        if (!hasNode)
        {
            GUILayout.Label(StringTable.GetString("research_branch_empty"));
        }
    }

    private void DrawBranchButtons()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tower"))
        {
            _selectedBranch = ResearchBranch.Tower;
        }

        if (GUILayout.Button("Production"))
        {
            _selectedBranch = ResearchBranch.Production;
        }

        if (GUILayout.Button("Convenience"))
        {
            _selectedBranch = ResearchBranch.Convenience;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawResearchNode(ResearchNodeData node)
    {
        ResearchNodeState nodeState = _researchManager.GetNodeState(node);

        GUILayout.Label(StringTable.GetString(node.NameLocKey));
        GUILayout.Label(StringTable.GetString(node.DescriptionLocKey));
        GUILayout.Label(BuildCostText(node));
        GUILayout.Label($"State: {StringTable.GetString(GetStateLocKey(nodeState))}");

        bool previousEnabled = GUI.enabled;
        GUI.enabled = nodeState == ResearchNodeState.Available;
        if (GUILayout.Button("Research"))
        {
            _researchManager.TryResearch(node, out _);
        }
        GUI.enabled = previousEnabled;
    }

    private string BuildCostText(ResearchNodeData node)
    {
        var parts = new List<string>
        {
            string.Format(
                StringTable.GetString(RP_COST_LOC_KEY),
                node.ResearchPointCost),
        };

        foreach (ResourceAmount entry in node.ResourceCost)
        {
            string resourceName = StringTable.GetString(UNKNOWN_RESOURCE_LOC_KEY);
            if (_resourceManager != null &&
                _resourceManager.Catalog != null &&
                _resourceManager.Catalog.TryGet(entry.Type, out ResourceData resourceData))
            {
                resourceName = StringTable.GetString(resourceData.NameLocKey);
            }

            parts.Add(string.Format(
                StringTable.GetString(RESOURCE_COST_LOC_KEY),
                resourceName,
                entry.Amount));
        }

        return string.Join(COST_SEPARATOR, parts);
    }

    private static string GetStateLocKey(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => "research_state_completed",
            ResearchNodeState.UnavailablePhase => "research_state_day_only",
            ResearchNodeState.TierLocked => "research_state_tier_locked",
            ResearchNodeState.PrerequisiteLocked => "research_state_prerequisite_locked",
            ResearchNodeState.InsufficientResearchPoints => "research_state_insufficient_rp",
            ResearchNodeState.InsufficientResources => "research_state_insufficient_resources",
            ResearchNodeState.Available => "research_state_available",
            ResearchNodeState.LandmarkLocked => "research_state_landmark_locked",
            _ => "research_state_invalid",
        };
    }
#endif
}
