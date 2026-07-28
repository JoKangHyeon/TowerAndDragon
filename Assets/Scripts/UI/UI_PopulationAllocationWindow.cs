using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_PopulationAllocationWindow : MonoBehaviour
{
    private const int POPULATION_STEP = 1;
    private const string BUILDING_POPULATION_FORMAT_LOC_KEY =
        "population_allocation_building_population_format";
    private const string TOTAL_POPULATION_FORMAT_LOC_KEY =
        "population_allocation_total_population_format";
    private const string ASSIGN_ALL_LOC_KEY =
        "population_allocation_assign_all";
    private const string UNASSIGN_ALL_LOC_KEY =
        "population_allocation_unassign_all";

    [SerializeField] private GameObject _windowRoot;
    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;

    [SerializeField] private TMP_Text _buildingNameText;
    [SerializeField] private TMP_Text _buildingPopulationText;
    [SerializeField] private TMP_Text _totalPopulationText;
    [SerializeField] private TMP_Text _assignAllButtonText;
    [SerializeField] private TMP_Text _unassignAllButtonText;

    [SerializeField] private Button _assignButton;
    [SerializeField] private Button _unassignButton;
    [SerializeField] private Button _assignAllButton;
    [SerializeField] private Button _unassignAllButton;

    private Building _selectedBuilding;
    private IPopulationAllocationTarget _selectedTarget;

    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private void Awake()
    {
        AddButtonListeners();
        ApplyLocalizedLabels();

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(
                HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(
                HandleCycleChanged);
        }
    }

    private void OnDisable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(
                HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(
                HandleCycleChanged);
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void Update()
    {
        if (_buildingPlacementController == null)
        {
            return;
        }

        Building selectedBuilding =
            _buildingPlacementController.SelectedBuilding;

        if (_selectedBuilding == selectedBuilding)
        {
            return;
        }

        Bind(selectedBuilding);
    }

    private void Bind(Building building)
    {
        _selectedBuilding = building;
        _selectedTarget = building != null
            ? building.GetComponent<IPopulationAllocationTarget>()
            : null;

        bool hasTarget =
            _selectedTarget != null &&
            _selectedTarget.IsInitialized;

        if (_windowRoot != null)
        {
            _windowRoot.SetActive(hasTarget);
        }

        if (hasTarget)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_selectedBuilding == null ||
            _selectedTarget == null ||
            _populationManager == null)
        {
            return;
        }

        if (_buildingNameText != null)
        {
            _buildingNameText.text = ResolveBuildingName(_selectedBuilding);
        }

        if (_buildingPopulationText != null)
        {
            _buildingPopulationText.text = string.Format(
                StringTable.GetString(BUILDING_POPULATION_FORMAT_LOC_KEY),
                _selectedTarget.AssignedPopulation,
                _selectedTarget.Capacity);
        }

        if (_totalPopulationText != null)
        {
            _totalPopulationText.text = string.Format(
                StringTable.GetString(TOTAL_POPULATION_FORMAT_LOC_KEY),
                _populationManager.AvailablePopulation,
                _populationManager.MaxPopulation);
        }

        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        bool hasValidTarget =
            _selectedBuilding != null &&
            _selectedTarget != null &&
            _selectedTarget.IsInitialized;

        bool canEdit = hasValidTarget && IsDay;
        bool canAssign =
            canEdit &&
            _populationManager != null &&
            _populationManager.AvailablePopulation >= POPULATION_STEP &&
            _selectedTarget.AvailableCapacity >= POPULATION_STEP;
        bool canUnassign =
            canEdit &&
            _selectedTarget.AssignedPopulation >= POPULATION_STEP;

        SetInteractable(_assignButton, canAssign);
        SetInteractable(_assignAllButton, canAssign);
        SetInteractable(_unassignButton, canUnassign);
        SetInteractable(_unassignAllButton, canUnassign);
    }

    private void AssignOne()
    {
        if (CanEditTarget())
        {
            _selectedTarget.TryAssign(POPULATION_STEP);
        }
    }

    private void UnassignOne()
    {
        if (CanEditTarget())
        {
            _selectedTarget.TryUnassign(POPULATION_STEP);
        }
    }

    private void AssignAll()
    {
        if (!CanEditTarget() || _populationManager == null)
        {
            return;
        }

        int amount = Mathf.Min(
            _selectedTarget.AvailableCapacity,
            _populationManager.AvailablePopulation);

        if (amount > 0)
        {
            _selectedTarget.TryAssign(amount);
        }
    }

    private void UnassignAll()
    {
        if (!CanEditTarget())
        {
            return;
        }

        int amount = _selectedTarget.AssignedPopulation;
        if (amount > 0)
        {
            _selectedTarget.TryUnassign(amount);
        }
    }

    private bool CanEditTarget()
    {
        return _selectedBuilding != null &&
            _selectedTarget != null &&
            _selectedTarget.IsInitialized &&
            IsDay;
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        Refresh();
    }

    private void HandleCycleChanged(CycleManager.CycleState state)
    {
        RefreshButtonState();
    }

    private void AddButtonListeners()
    {
        _assignButton?.onClick.AddListener(AssignOne);
        _unassignButton?.onClick.AddListener(UnassignOne);
        _assignAllButton?.onClick.AddListener(AssignAll);
        _unassignAllButton?.onClick.AddListener(UnassignAll);
    }

    private void RemoveButtonListeners()
    {
        _assignButton?.onClick.RemoveListener(AssignOne);
        _unassignButton?.onClick.RemoveListener(UnassignOne);
        _assignAllButton?.onClick.RemoveListener(AssignAll);
        _unassignAllButton?.onClick.RemoveListener(UnassignAll);
    }

    private void ApplyLocalizedLabels()
    {
        if (_assignAllButtonText != null)
        {
            _assignAllButtonText.text =
                StringTable.GetString(ASSIGN_ALL_LOC_KEY);
        }

        if (_unassignAllButtonText != null)
        {
            _unassignAllButtonText.text =
                StringTable.GetString(UNASSIGN_ALL_LOC_KEY);
        }
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static string ResolveBuildingName(Building building)
    {
        if (building is Tower tower && tower.Data != null)
        {
            return StringTable.GetString(tower.Data.NameLocKey);
        }

        if (building is Factory factory && factory.Data != null)
        {
            return StringTable.GetString(factory.Data.NameLocKey);
        }

        if (building is ResearchLab researchLab && researchLab.Data != null)
        {
            return StringTable.GetString(researchLab.Data.NameLocKey);
        }

        return string.Empty;
    }
}
