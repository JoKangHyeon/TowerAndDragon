using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가동 중인 타워가 배치 인구 수에 비례해 내는 하루치 자재를 소비한다.
/// 실행 시점과 다른 정산 단계와의 순서는 DailySettlementManager가 담당한다
/// (TerrainUpkeepSystem과 동일한 구조).
///
/// 낼 자원이 부족하면 미납분만큼 해당 타워의 인구를 회수해 비활성화한다. 인구는 죽지 않고
/// 가용 인구로 돌아가므로, 플레이어가 낮에 다시 배치하면 즉시 되살아난다
/// ("파괴 대신 비활성화" - 기획서의 인구 On/Off 관례).
/// </summary>
public class TowerUpkeepSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("타워 종류별 인구 1명당 유지비. 미연결이면 타워 유지비를 걷지 않는다.")]
    [SerializeField] private TowerUpkeepData _towerUpkeepData;

    // 정산 1회 안에서만 쓰는 재사용 버퍼 - _towers와 _upkeeps는 인덱스가 서로 대응한다.
    private readonly List<IPopulationAllocationTarget> _towers = new();
    private readonly List<TowerUpkeepRules.TowerUpkeep> _upkeeps = new();
    private readonly Dictionary<ResourceType, int> _requirement = new();
    private readonly Dictionary<ResourceType, int> _shortfall = new();
    private readonly List<int> _deactivationTargets = new();

    // 인구 해제 직전에 대상 타워를 옮겨 담는 버퍼 - CollectTowers가 건드리지 않아야 하므로
    // _towers와 반드시 별개로 유지한다. 이유는 DeactivateForShortfall 주석 참고.
    private readonly List<IPopulationAllocationTarget> _deactivationBuffer = new();

    public bool TrySettle(out TowerUpkeepResult result)
    {
        result = default;

        if (_gridMap == null || _resourceManager == null)
        {
            return false;
        }

        if (!WiringGuard.Optional(_towerUpkeepData, nameof(_towerUpkeepData), this))
        {
            return false;
        }

        CollectTowers();
        _requirement.Clear();
        TowerUpkeepRules.AccumulateRequirement(_upkeeps, _requirement);

        int requiredTotal = 0;
        int consumedTotal = 0;
        _shortfall.Clear();

        foreach (KeyValuePair<ResourceType, int> pair in _requirement)
        {
            int consumed = _resourceManager.ConsumeUpTo(pair.Key, pair.Value);
            requiredTotal += pair.Value;
            consumedTotal += consumed;
            _shortfall[pair.Key] = pair.Value - consumed;
        }

        int deactivatedCount = DeactivateForShortfall();

        result = new TowerUpkeepResult(requiredTotal, consumedTotal, deactivatedCount);
        return true;
    }

    /// <summary>
    /// 다음 정산에서 걷힐 하루치 타워 유지비를 자원 종류별로 누적한다. 자원을 소비하거나 인구를 해제하지 않는다.
    /// 실제 정산(TrySettle)과 같은 경로(CollectTowers + AccumulateRequirement)를 그대로 쓰므로
    /// 예측치와 실제 차감액이 어긋나지 않는다 (TerrainUpkeepSystem.AccumulateProjectedUpkeep과 같은 구조).
    ///
    /// 재사용 버퍼(_towers·_upkeeps)를 TrySettle과 공유한다. 둘 다 메인 스레드에서만 돌지만
    /// **중첩 호출은 실제로 일어난다** - TrySettle의 인구 해제가 PopulationChanged를 발화시키고,
    /// 그 리스너인 ResourceForecast가 다시 이 메서드를 부른다. 그래서 이 메서드는 언제 불려도
    /// 버퍼를 새로 채우기만 하고, 순회 중 재구축에 대한 방어는 DeactivateForShortfall이 담당한다.
    /// </summary>
    public void AccumulateProjectedUpkeep(IDictionary<ResourceType, int> into)
    {
        if (into == null || _gridMap == null || _towerUpkeepData == null)
        {
            return;
        }

        CollectTowers();
        TowerUpkeepRules.AccumulateRequirement(_upkeeps, into);
    }

    // 인구가 배치된 타워만 모은다 - 인구 0인 타워는 유지비도 0이고, 비활성화 대상으로 골라도
    // 미납분을 전혀 줄이지 못한다. 인구로 가동하지 않는 새끼용 타워도 이 조건에서 자연히 빠진다.
    private void CollectTowers()
    {
        _towers.Clear();
        _upkeeps.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is not Tower tower)
            {
                continue;
            }

            var target = building.GetComponent<IPopulationAllocationTarget>();

            if (target == null || !target.IsInitialized || target.AssignedPopulation <= 0)
            {
                continue;
            }

            IReadOnlyList<ResourceAmount> perPopulation =
                _towerUpkeepData.GetUpkeepPerPopulation(tower.Data);

            var upkeep = new TowerUpkeepRules.TowerUpkeep(target.AssignedPopulation, perPopulation);

            if (!upkeep.HasUpkeep)
            {
                continue;
            }

            _towers.Add(target);
            _upkeeps.Add(upkeep);
        }
    }

    private int DeactivateForShortfall()
    {
        TowerUpkeepRules.SelectDeactivationTargets(_upkeeps, _shortfall, _deactivationTargets);

        // 인구를 해제하기 전에 대상을 별도 버퍼로 옮긴다. TryUnassign은 PopulationChanged를 발화시키고,
        // 그 리스너(ResourceForecast)가 AccumulateProjectedUpkeep -> CollectTowers로 _towers를 비우고
        // 다시 채운다. _towers[index]를 순회 도중에 읽으면 인덱스가 어긋나 엉뚱한 타워를 해제하거나
        // 범위를 벗어난다.
        _deactivationBuffer.Clear();

        foreach (int index in _deactivationTargets)
        {
            _deactivationBuffer.Add(_towers[index]);
        }

        int deactivatedCount = 0;

        foreach (IPopulationAllocationTarget target in _deactivationBuffer)
        {
            if (!target.TryUnassign(target.AssignedPopulation))
            {
                Debug.LogWarning(
                    $"[TowerUpkeepSystem] 타워 유지비 미납 타워의 인구 회수에 실패했습니다: {target}");
                continue;
            }

            deactivatedCount += 1;
        }

        return deactivatedCount;
    }
}
