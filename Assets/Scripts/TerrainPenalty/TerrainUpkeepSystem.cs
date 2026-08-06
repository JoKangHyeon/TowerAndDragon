using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설원·암석 지역에 걸친 시설이 배치 인구 수에 비례해 내는 하루치 자재(나무·돌)를 소비한다.
/// 실행 시점과 다른 정산 단계와의 순서는 DailySettlementManager가 담당한다
/// (PopulationUpkeepSystem과 동일한 구조).
///
/// 낼 자재가 부족하면 미납분만큼 해당 시설의 인구를 회수해 비활성화한다. 인구는 죽지 않고
/// 가용 인구로 돌아가므로, 플레이어가 낮에 다시 배치하면 즉시 되살아난다
/// ("파괴 대신 비활성화" - 기획서의 인구 On/Off 관례).
/// </summary>
public class TerrainUpkeepSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    // 정산 1회 안에서만 쓰는 재사용 버퍼 - 인덱스가 서로 대응한다.
    private readonly List<IPopulationAllocationTarget> _facilities = new();
    private readonly List<TerrainUpkeepRules.FacilityUpkeep> _upkeeps = new();
    private readonly List<int> _deactivationTargets = new();

    public bool TrySettle(out TerrainUpkeepResult result)
    {
        result = default;

        if (_gridMap == null || _resourceManager == null || _terrainPenaltySystem == null)
        {
            return false;
        }

        CollectFacilities();
        TerrainUpkeepRules.AccumulateRequirement(_upkeeps, out int requiredWood, out int requiredStone);

        int consumedWood = _resourceManager.ConsumeUpTo(ResourceType.Wood, requiredWood);
        int consumedStone = _resourceManager.ConsumeUpTo(ResourceType.Stone, requiredStone);

        int deactivatedCount = DeactivateForShortfall(
            requiredWood - consumedWood,
            requiredStone - consumedStone);

        result = new TerrainUpkeepResult(
            requiredWood,
            consumedWood,
            requiredStone,
            consumedStone,
            deactivatedCount);
        return true;
    }

    // 인구가 배치된 시설만 모은다 - 인구 0인 시설은 유지비도 0이고, 비활성화 대상으로 골라도
    // 미납분을 전혀 줄이지 못한다.
    private void CollectFacilities()
    {
        _facilities.Clear();
        _upkeeps.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            var target = building.GetComponent<IPopulationAllocationTarget>();

            if (target == null || !target.IsInitialized || target.AssignedPopulation <= 0)
            {
                continue;
            }

            TerrainPenaltyModifiers penalty = _terrainPenaltySystem.Resolve(building);

            // 반올림은 시설 단위로 한 번만 한다 - 여러 지형에 걸친 건물의 비율 가중치는
            // 소수이므로, 인구를 곱하기 전에 정수화하면 오차가 커진다.
            var upkeep = new TerrainUpkeepRules.FacilityUpkeep(
                Mathf.RoundToInt(target.AssignedPopulation * penalty.WoodUpkeepPerPopulation),
                Mathf.RoundToInt(target.AssignedPopulation * penalty.StoneUpkeepPerPopulation));

            if (!upkeep.HasUpkeep)
            {
                continue;
            }

            _facilities.Add(target);
            _upkeeps.Add(upkeep);
        }
    }

    private int DeactivateForShortfall(int woodShortfall, int stoneShortfall)
    {
        if (woodShortfall <= 0 && stoneShortfall <= 0)
        {
            return 0;
        }

        TerrainUpkeepRules.SelectDeactivationTargets(
            _upkeeps,
            woodShortfall,
            stoneShortfall,
            _deactivationTargets);

        int deactivatedCount = 0;

        foreach (int index in _deactivationTargets)
        {
            IPopulationAllocationTarget target = _facilities[index];

            if (!target.TryUnassign(target.AssignedPopulation))
            {
                Debug.LogWarning(
                    $"[TerrainUpkeepSystem] 지역 유지비 미납 시설의 인구 회수에 실패했습니다: {target}");
                continue;
            }

            deactivatedCount += 1;
        }

        return deactivatedCount;
    }
}
