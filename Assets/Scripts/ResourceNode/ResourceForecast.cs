using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// '다음 정산에서 자원이 얼마나 늘고 줄지'를 자원 종류별로 집계한다.
/// - 생산: 각 Factory가 OnDayStart에 쓰는 것과 같은 공식(Factory.AccumulateProjectedProduction)
/// - 소모: 인구 식량 유지비(PopulationUpkeepRules)와 지역 자재 유지비(TerrainUpkeepSystem)
/// 실제 정산은 DailySettlementManager가 OnDayStartUpkeep에 수행하고, 이 클래스는 같은 규칙으로
/// 예상치만 계산해 UI에 제공한다. 건물 추가/제거·인구 배치·버프·지형 페널티 변경 시 다시 계산하고
/// ForecastChanged로 알린다.
/// </summary>
public class ResourceForecast : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    [Tooltip("인구 배치(충원율) 변경과 인구 식량 유지비 계산에 쓴다.")]
    [SerializeField] private PopulationManager _populationManager;

    [Tooltip("새끼용 지역 생산 버프가 재확정될 때 예상치를 다시 계산하기 위해 구독한다. " +
        "미연결이면 이 값 변동만으로는 예상치가 갱신되지 않는다(다음 건물 추가/제거·인구 변경 때 함께 반영됨).")]
    [SerializeField] private BabyDragonBuffSystem _babyDragonBuffSystem;

    [Tooltip("지역(지형) 페널티가 다시 계산될 때 예상치를 갱신하기 위해 구독한다. " +
        "미연결이면 이 값 변동만으로는 예상치가 갱신되지 않는다(다음 건물 추가/제거·인구 변경 때 함께 반영됨).")]
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    [Tooltip("지역(설원·암석) 자재 유지비 예상 소모량을 가져온다. 미연결이면 자재 유지비를 0으로 본다.")]
    [SerializeField] private TerrainUpkeepSystem _terrainUpkeepSystem;

    /// <summary>예상 증감이 바뀌었을 때 발화. UI가 구독해 표기를 갱신한다.</summary>
    public UnityEvent ForecastChanged;

    private readonly Dictionary<ResourceType, int> _production = new();
    private readonly Dictionary<ResourceType, int> _consumption = new();

    // 툴팁 내역용 평탄 목록. 자원 12종 × 출처 3종이라 선형 스캔으로 충분하고, 자원별 리스트를
    // 따로 두는 것보다 재계산 때 할당이 없다.
    private readonly List<ResourceForecastEntry> _entries = new();

    // 지역 유지비를 받아올 때만 쓰는 임시 버퍼. 소모 합계(_consumption)에 출처별로 나눠 담기 위해
    // 한 번 거쳐 간다.
    private readonly Dictionary<ResourceType, int> _terrainUpkeepBuffer = new();

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingChanged);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_babyDragonBuffSystem != null)
        {
            _babyDragonBuffSystem.BuffsRecomputed.AddListener(HandleBuffsRecomputed);
        }

        if (_terrainPenaltySystem != null)
        {
            _terrainPenaltySystem.PenaltiesRecomputed.AddListener(HandleBuffsRecomputed);
        }

        Recompute();
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingChanged);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_babyDragonBuffSystem != null)
        {
            _babyDragonBuffSystem.BuffsRecomputed.RemoveListener(HandleBuffsRecomputed);
        }

        if (_terrainPenaltySystem != null)
        {
            _terrainPenaltySystem.PenaltiesRecomputed.RemoveListener(HandleBuffsRecomputed);
        }
    }

    /// <summary>자원 종류별 예상 하루 생산량(양수). 없으면 0.</summary>
    public int GetDailyProduction(ResourceType type) =>
        _production.TryGetValue(type, out int amount) ? amount : 0;

    /// <summary>자원 종류별 예상 하루 소모량. 소모를 나타내지만 양수로 돌려준다.</summary>
    public int GetDailyConsumption(ResourceType type) =>
        _consumption.TryGetValue(type, out int amount) ? amount : 0;

    /// <summary>생산에서 소모를 뺀 하루 순증감. 음수면 자원이 줄어드는 중이다.</summary>
    public int GetDailyNetChange(ResourceType type) =>
        ResourceForecastRules.NetChange(GetDailyProduction(type), GetDailyConsumption(type));

    /// <summary>
    /// 자원 하나의 증감 내역을 출처별로 담아준다. 버퍼는 호출자가 소유하며 내용은 먼저 비운다
    /// (TerrainUpkeepRules.SelectDeactivationTargets·BuildingTerrainExposure.Accumulate와 같은 관례).
    /// </summary>
    public void CollectBreakdown(ResourceType type, List<ResourceForecastEntry> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        foreach (ResourceForecastEntry entry in _entries)
        {
            if (entry.Type == type)
            {
                into.Add(entry);
            }
        }
    }

    private void HandleBuildingChanged(Building building) => Recompute();
    private void HandlePopulationChanged(PopulationState state) => Recompute();
    private void HandleBuffsRecomputed() => Recompute();

    private void Recompute()
    {
        _production.Clear();
        _consumption.Clear();
        _entries.Clear();

        AccumulateProduction();
        AccumulatePopulationUpkeep();
        AccumulateTerrainUpkeep();

        ForecastChanged?.Invoke();
    }

    private void AccumulateProduction()
    {
        if (_gridMap == null)
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Factory factory)
            {
                factory.AccumulateProjectedProduction(_production);
            }
        }

        foreach (KeyValuePair<ResourceType, int> pair in _production)
        {
            AddEntry(pair.Key, ResourceForecastSource.Production, pair.Value);
        }
    }

    // 식량은 최대 인구 1명당 1씩 걷힌다(PopulationUpkeepSystem이 실제로 쓰는 값과 같은 출처).
    private void AccumulatePopulationUpkeep()
    {
        if (_populationManager == null)
        {
            return;
        }

        int requiredFood = PopulationUpkeepRules.GetRequiredFood(_populationManager.MaxPopulation);
        AddConsumption(ResourceType.Food, ResourceForecastSource.PopulationUpkeep, requiredFood);
    }

    private void AccumulateTerrainUpkeep()
    {
        if (_terrainUpkeepSystem == null)
        {
            return;
        }

        _terrainUpkeepBuffer.Clear();
        _terrainUpkeepSystem.AccumulateProjectedUpkeep(_terrainUpkeepBuffer);

        foreach (KeyValuePair<ResourceType, int> pair in _terrainUpkeepBuffer)
        {
            AddConsumption(pair.Key, ResourceForecastSource.TerrainUpkeep, pair.Value);
        }
    }

    private void AddConsumption(ResourceType type, ResourceForecastSource source, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _consumption[type] = _consumption.TryGetValue(type, out int existing)
            ? existing + amount
            : amount;

        AddEntry(type, source, -amount);
    }

    private void AddEntry(ResourceType type, ResourceForecastSource source, int signedAmount)
    {
        if (signedAmount == 0)
        {
            return;
        }

        _entries.Add(new ResourceForecastEntry(type, source, signedAmount));
    }
}
