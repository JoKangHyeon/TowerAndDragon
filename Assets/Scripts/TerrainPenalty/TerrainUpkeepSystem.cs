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

    /// <summary>
    /// 다음 정산에서 걷힐 하루치 자재 유지비를 자원 종류별로 누적한다. 자원을 소비하거나 인구를 해제하지 않는다.
    /// 실제 정산(TrySettle)과 같은 경로(CollectFacilities + AccumulateRequirement)를 그대로 쓰므로
    /// 예측치와 실제 차감액이 어긋나지 않는다 (Factory.AccumulateProjectedProduction과 같은 구조).
    ///
    /// 재사용 버퍼(_facilities·_upkeeps)를 TrySettle과 공유하지만, 둘 다 메인 스레드에서만 돌고
    /// 서로 중첩 호출되지 않으므로 안전하다.
    /// </summary>
    public void AccumulateProjectedUpkeep(IDictionary<ResourceType, int> into)
    {
        if (into == null || _gridMap == null || _terrainPenaltySystem == null)
        {
            return;
        }

        CollectFacilities();
        TerrainUpkeepRules.AccumulateRequirement(_upkeeps, out int requiredWood, out int requiredStone);

        Accumulate(into, ResourceType.Wood, requiredWood);
        Accumulate(into, ResourceType.Stone, requiredStone);
    }

    private static void Accumulate(IDictionary<ResourceType, int> into, ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        into[type] = into.TryGetValue(type, out int existing) ? existing + amount : amount;
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

            TerrainUpkeepRules.FacilityUpkeep upkeep =
                TerrainUpkeepRules.ResolveFacilityUpkeep(target.AssignedPopulation, penalty);

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
