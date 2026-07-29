using UnityEngine;

// 씬의 모든 타워에 배율 Composite와 피격 상태이상 조회원(용 스킬트리)을 주입한다.
// 신규 건설(GridMap.OnBuildingAdded)뿐 아니라 OnEnable 시점에 GridMap.Buildings를 1회 순회해
// 씬에 이미 배치된 타워도 소급 주입한다 - 그렇지 않으면 사전 배치 타워는 OnBuildingAdded가
// 호출되지 않아 배율 쿼리를 영구히 못 받는다(배율 1 고정, 기존 결함).
public sealed class TowerStatMultiplierCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private TowerStatMultiplierComposite _statComposite;
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    private void OnEnable()
    {
        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);

        foreach (Building building in _gridMap.Buildings)
        {
            InjectQueries(building);
        }
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
        InjectQueries(building);
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is Tower tower && tower.Attack != null)
        {
            tower.Attack.SetStatMultiplierQuery(null);
            tower.Attack.SetHitStatusQuery(null);
        }
    }

    private void InjectQueries(Building building)
    {
        if (!(building is Tower tower) || tower.Attack == null)
        {
            return;
        }

        tower.Attack.SetStatMultiplierQuery(_statComposite);
        tower.Attack.SetHitStatusQuery(_dragonTreeManager);
    }
}
