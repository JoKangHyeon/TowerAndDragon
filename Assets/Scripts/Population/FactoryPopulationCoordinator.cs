using UnityEngine;

/// <summary>
/// GridMap의 건물 생명주기 알림을 받아 생산시설(Factory) 인구 할당의 생성과 제거를 연결한다.
/// GridMap과 PopulationManager가 서로를 직접 참조하지 않도록 중간에서 조정한다.
/// </summary>
public class FactoryPopulationCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;

    private void OnEnable()
    {
        if (_gridMap == null)
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
        if (!(building is Factory factory))
        {
            return;
        }

        FactoryPopulation factoryPopulation = factory.GetComponent<FactoryPopulation>();
        if (factoryPopulation == null)
        {
            Debug.LogError(
                "[FactoryPopulationCoordinator] 생산시설에 FactoryPopulation 컴포넌트가 없습니다.",
                factory);
            return;
        }

        if (factoryPopulation.IsInitialized)
        {
            return;
        }

        if (_populationManager == null || !factoryPopulation.Initialize(_populationManager))
        {
            Debug.LogWarning(
                "[FactoryPopulationCoordinator] 생산시설 인구 할당 생성에 실패했습니다.",
                factory);
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (!(building is Factory factory))
        {
            return;
        }

        FactoryPopulation factoryPopulation = factory.GetComponent<FactoryPopulation>();
        if (factoryPopulation == null || !factoryPopulation.IsInitialized)
        {
            return;
        }

        if (!factoryPopulation.Release())
        {
            Debug.LogWarning(
                "[FactoryPopulationCoordinator] 생산시설 인구 할당 제거에 실패했습니다.",
                factory);
        }
    }
}
