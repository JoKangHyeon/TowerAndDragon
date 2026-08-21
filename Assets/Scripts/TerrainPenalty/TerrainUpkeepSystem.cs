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
    [Tooltip("건물별 지형 페널티의 출처. 미연결이면 지역 자재 유지비 정산을 통째로 건너뛴다 - " +
        "지형 페널티를 일부러 쓰지 않는 씬(튜토리얼)이 그렇다.")]
    [WiringOptional]
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    // 정산 1회 안에서만 쓰는 재사용 버퍼 - 인덱스가 서로 대응한다.
    private readonly List<IPopulationAllocationTarget> _facilities = new();
    private readonly List<TerrainUpkeepRules.FacilityUpkeep> _upkeeps = new();
    private readonly List<int> _deactivationTargets = new();

    // 인구 해제 직전에 대상 시설을 옮겨 담는 버퍼 - CollectFacilities가 건드리지 않아야 하므로
    // _facilities와 반드시 별개로 유지한다. 이유는 DeactivateForShortfall 주석 참고.
    private readonly List<IPopulationAllocationTarget> _deactivationBuffer = new();

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
    /// 재사용 버퍼(_facilities·_upkeeps)를 TrySettle과 공유한다. 둘 다 메인 스레드에서만 돌지만
    /// **중첩 호출은 실제로 일어난다** - TrySettle의 인구 해제가 PopulationChanged를 발화시키고,
    /// 그 리스너인 ResourceForecast가 다시 이 메서드를 부른다. 그래서 이 메서드는 언제 불려도
    /// 버퍼를 새로 채우기만 하고, 순회 중 재구축에 대한 방어는 DeactivateForShortfall이 담당한다.
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

        // 인구를 해제하기 전에 대상을 별도 버퍼로 옮긴다. TryUnassign은 PopulationChanged를 발화시키고,
        // 그 리스너(ResourceForecast)가 AccumulateProjectedUpkeep -> CollectFacilities로 _facilities를
        // 비우고 다시 채운다. _facilities[index]를 순회 도중에 읽으면 인덱스가 어긋나 엉뚱한 시설을
        // 해제하거나 범위를 벗어난다.
        _deactivationBuffer.Clear();

        foreach (int index in _deactivationTargets)
        {
            _deactivationBuffer.Add(_facilities[index]);
        }

        int deactivatedCount = 0;

        foreach (IPopulationAllocationTarget target in _deactivationBuffer)
        {
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
