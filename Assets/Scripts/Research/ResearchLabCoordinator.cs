using UnityEngine;

public sealed class ResearchLabCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResearchManager _researchManager;

    private void OnEnable()
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
    }

    private void OnDisable()
    {
        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
    }

    private void HandleBuildingAdded(Building building)
    {
        if (building is not ResearchLab lab)
        {
            return;
        }

        if (!WiringGuard.Require(_researchManager, nameof(_researchManager), this))
        {
            return;
        }

        ResearchLabPopulation population = lab.Population;
        bool initialized = population != null &&
            population.Initialize(_populationManager, _cycleManager);

        if (!initialized || !_researchManager.RegisterLab(lab))
        {
            Debug.LogWarning(
                "[ResearchLabCoordinator] 연구소 연결에 실패했습니다.",
                lab);
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is not ResearchLab lab)
        {
            return;
        }

        _researchManager?.UnregisterLab(lab);

        if (lab.Population != null && lab.Population.IsInitialized)
        {
            lab.Population.Release();
        }
    }
}
