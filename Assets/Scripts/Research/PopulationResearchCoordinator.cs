using UnityEngine;

public sealed class PopulationResearchCoordinator : MonoBehaviour
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private PopulationManager _populationManager;

    private void OnEnable()
    {
        if (_populationManager == null || _researchManager == null)
        {
            return;
        }

        _populationManager.CapacityModifierQuery = _researchManager;
        _researchManager.NodeCompleted.AddListener(HandleNodeCompleted);
    }

    private void OnDisable()
    {
        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleNodeCompleted);
        }

        if (_populationManager != null &&
            ReferenceEquals(_populationManager.CapacityModifierQuery, _researchManager))
        {
            _populationManager.CapacityModifierQuery = null;
        }
    }

    // 연구로 정원이 줄면 초과 배치 인구를 가용 인구로 되돌려야 한다.
    private void HandleNodeCompleted(ResearchNodeData node)
    {
        _populationManager.ReconcileAssignedPopulation();
    }
}
