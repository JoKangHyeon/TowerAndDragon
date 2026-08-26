using UnityEngine;

// 씬의 모든 생산시설·연구소·타워에 지역 페널티 조회원을 주입한다.
// 신규 건설(GridMap.OnBuildingAdded)뿐 아니라 OnEnable 시점에 GridMap.Buildings를 1회 순회해
// 씬에 이미 배치된 건물에도 소급 주입한다 - TowerStatMultiplierCoordinator와 동일한 관용구로,
// 그렇지 않으면 사전 배치 건물은 OnBuildingAdded가 호출되지 않아 페널티를 영구히 못 받는다.
public sealed class TerrainPenaltyCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    private void OnEnable()
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        if (!WiringGuard.Require(_terrainPenaltySystem, nameof(_terrainPenaltySystem), this))
        {
            return;
        }

        _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);

        foreach (Building building in _gridMap.Buildings)
        {
            InjectQuery(building, _terrainPenaltySystem);
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

    private void HandleBuildingAdded(Building building) =>
        InjectQuery(building, _terrainPenaltySystem);

    private void HandleBuildingRemoving(Building building) =>
        InjectQuery(building, null);

    private static void InjectQuery(Building building, IBuildingTerrainPenaltyQuery query)
    {
        if (building is Factory factory)
        {
            factory.SetTerrainPenaltyQuery(query);
            return;
        }

        if (building is ResearchLab lab)
        {
            lab.SetTerrainPenaltyQuery(query);
            return;
        }

        if (building is Tower tower && tower.Attack != null)
        {
            tower.Attack.SetTerrainPenaltyQuery(query);
        }
    }
}
