using UnityEngine;

/// <summary>
/// GridMap의 건물 생명주기 알림을 받아 생산시설(Factory)에 ResourceManager/CycleManager를 연결한다.
/// Factory 프리팹은 씬 오브젝트를 직접 참조할 수 없으므로, 건설 직후 이 코디네이터가 주입한다.
/// </summary>
public class FactoryResourceCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;

    private void OnEnable()
    {
        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded += HandleBuildingAdded;
    }

    private void OnDisable()
    {
        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded -= HandleBuildingAdded;
    }

    private void HandleBuildingAdded(Building building)
    {
        if (!(building is Factory factory))
        {
            return;
        }

        if (factory.IsInitialized)
        {
            return;
        }

        if (_resourceManager == null || _cycleManager == null || !factory.Initialize(_resourceManager, _cycleManager))
        {
            Debug.LogWarning(
                "[FactoryResourceCoordinator] 생산시설 자원 연결에 실패했습니다.",
                factory);
        }
    }
}
