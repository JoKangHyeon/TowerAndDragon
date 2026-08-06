using UnityEngine;

/// <summary>
/// GridMap의 건물 생명주기 알림을 받아 타워 인구 할당의 생성과 제거를 연결한다.
/// GridMap과 PopulationManager가 서로를 직접 참조하지 않도록 중간에서 조정한다.
/// </summary>
public class TowerPopulationCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;

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
        if (!(building is Tower tower))
        {
            return;
        }

        if (!tower.RequiresPopulation)
        {
            return;
        }

        if (!WiringGuard.RequireComponent(tower, out TowerPopulation towerPopulation, tower))
        {
            return;
        }

        if (towerPopulation.IsInitialized)
        {
            return;
        }

        if (!WiringGuard.Require(_populationManager, nameof(_populationManager), this))
        {
            return;
        }

        if (!towerPopulation.Initialize(_populationManager))
        {
            Debug.LogWarning(
                "[TowerPopulationCoordinator] 타워 인구 할당 생성에 실패했습니다.",
                tower);
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (!(building is Tower tower))
        {
            return;
        }

        if (!tower.RequiresPopulation)
        {
            return;
        }

        TowerPopulation towerPopulation = tower.GetComponent<TowerPopulation>();
        if (towerPopulation == null || !towerPopulation.IsInitialized)
        {
            return;
        }

        if (!towerPopulation.Release())
        {
            Debug.LogWarning(
                "[TowerPopulationCoordinator] 타워 인구 할당 제거에 실패했습니다.",
                tower);
        }
    }
}
