using System.Collections.Generic;
using UnityEngine;

// 생산시설 공통 구현체. 서브클래스 없이 ResourceProductionData만 갈아끼워 벌목장/채굴장/광산 등을 표현한다(Tower/TowerData와 동일한 패턴).
public class Factory : Building
{
    // 새끼용 버프가 없을 때의 생산량 배율(BabyDragonBuffSystem이 배치/철거/이동 즉시,
    // 그리고 매일 밤 종료 시 갱신).
    public const float NEUTRAL_YIELD_MULTIPLIER = 1f;

    // 정원을 100% 채웠을 때의 충원율 - 최대 생산량(GetMaxYield) 계산에 쓴다.
    private const float FULL_STAFFING_RATIO = 1f;

    [SerializeField] private ResourceProductionData _data;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    private bool _isInitialized;
    protected FactoryPopulation _population;

    private readonly Dictionary<ResourceType, float> _areaYieldMultiplierByResource = new();

    // 지역(지형) 페널티 조회원 - 새끼용 버프(_areaYieldMultiplierByResource)와 반드시 별도 채널이어야
    // 한다. BabyDragonBuffSystem이 SetAreaYieldMultipliers로 그 사전을 매번 통째로 덮어쓰기 때문에,
    // 같은 곳에 지형 배율을 넣으면 새끼용이 재계산할 때마다 사라진다.
    // TerrainPenaltyCoordinator가 주입하며, 미배선 씬에서는 null로 남아 페널티 없이 동작한다.
    private IBuildingTerrainPenaltyQuery _terrainPenaltyQuery;

    public void SetTerrainPenaltyQuery(IBuildingTerrainPenaltyQuery terrainPenaltyQuery) =>
        _terrainPenaltyQuery = terrainPenaltyQuery;

    private float TerrainYieldMultiplier =>
        _terrainPenaltyQuery != null
            ? _terrainPenaltyQuery.Resolve(this).YieldMultiplier
            : NEUTRAL_YIELD_MULTIPLIER;

// BabyDragonBuffSystem이 재계산할 때마다 전체를 덮어쓴다 - 부분 갱신을 허용하면 버프
// 범위를 벗어난 뒤에도 이전 배율이 남는다.
public void SetAreaYieldMultipliers(IReadOnlyDictionary<ResourceType, float> multiplierByResource)
{
    _areaYieldMultiplierByResource.Clear();

    if (multiplierByResource == null)
    {
        return;
    }

    foreach (KeyValuePair<ResourceType, float> entry in multiplierByResource)
    {
        _areaYieldMultiplierByResource[entry.Key] = entry.Value;
    }
}

// 등록되지 않은 자원은 버프 대상이 아니므로 중립 배율을 쓴다(GridCell.GetYield와 같은 관례).
private float GetAreaYieldMultiplier(ResourceType resourceType) =>
    _areaYieldMultiplierByResource.TryGetValue(resourceType, out float multiplier)
        ? multiplier
        : NEUTRAL_YIELD_MULTIPLIER;

    // 건설 가능 여부 판정에 필요 - GridMap.CanConstructResourceFootprint 호출 시 전달한다.
    public ResourceType RequiredResourceNode => _data != null ? _data.RequiredResourceNode : ResourceType.None;

    // FactoryPopulation.Initialize에서 배치 가능 인구를 읽어가기 위해 필요(TowerPopulation과 동일한 용도).
    public ResourceProductionData Data => _data;
    public override IReadOnlyList<ResourceAmount> BuildCost => _data != null ? _data.BuildCost : base.BuildCost;
    public override int PopulationCapacity => _data != null ? _data.PopulationCapacity : base.PopulationCapacity;

    public bool IsInitialized => _isInitialized;
    protected GridMap FactoryGridMap => _gridMap;

    // ProducedResourceType이 여러 비트를 동시에 가질 수 있다(슬라임 농장). 정산도 UI 표시도
    // 자원 종류별로 따로 다뤄야 하므로 개별 비트 순회를 공통으로 열어둔다.
    public IEnumerable<ResourceType> EnumerateProducedResourceTypes()
    {
        if (_data == null)
            return System.Array.Empty<ResourceType>();

        return GridMap.EnumerateResourceFlags(_data.ProducedResourceType);
    }

    // 정산 시점과 UI 미리보기가 반드시 같은 값을 내야 하므로 계산식은 여기 하나만 둔다.
    protected int CalculateYield(ResourceType resourceType, float staffingRatio)
    {
        if (_data == null || _gridMap == null)
            return 0;

        // 생산량은 이 생산시설의 footprint에 속한 셀들이 보유한 자원별 생산량의 합이다(GridMap.GetFootprintYield 참고).
        int footprintYield = _gridMap.GetFootprintYield(this, resourceType);
        int produced = _data.CalculateYield(footprintYield, staffingRatio);
        return Mathf.RoundToInt(
            produced * GetAreaYieldMultiplier(resourceType) * TerrainYieldMultiplier);
    }

    // 현재 배치 인구 기준 생산량 - 다음 정산에서 실제로 들어올 양이다.
    public int GetCurrentYield(ResourceType resourceType)
    {
        float staffingRatio = _population != null ? _population.StaffingRatio : 0f;
        return CalculateYield(resourceType, staffingRatio);
    }

    // 정원을 다 채웠을 때의 생산량 - 현재 생산량의 상한 표시용이다.
    public int GetMaxYield(ResourceType resourceType)
    {
        return CalculateYield(resourceType, FULL_STAFFING_RATIO);
    }

    // 프리팹은 씬 오브젝트(ResourceManager/CycleManager/GridMap)를 들고 있을 수 없으므로,
    // 건설 직후 FactoryResourceCoordinator가 주입한다(TowerPopulation.Initialize와 동일한 패턴).
    public virtual bool Initialize(ResourceManager resourceManager, CycleManager cycleManager, GridMap gridMap)
    {
        if (_isInitialized || resourceManager == null || cycleManager == null || gridMap == null)
            return false;

        _resourceManager = resourceManager;
        _cycleManager = cycleManager;
        _gridMap = gridMap;
        _population = GetComponent<FactoryPopulation>();
        _cycleManager.OnDayStart.AddListener(OnSettlement);
        _isInitialized = true;
        return true;
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
            _cycleManager.OnDayStart.RemoveListener(OnSettlement);
    }

    private void OnSettlement(int currentCycle)
    {
        if (_data == null || _resourceManager == null || _gridMap == null)
            return;

        float staffingRatio = _population != null ? _population.StaffingRatio : 0f;

        // 슬라임 농장은 풋프린트에 걸친 지형별 슬라임을 각각 따로 합산해서 정산한다.
        // 단일 비트(기존 농장/벌목장/채석장)면 이 반복은 그냥 한 번만 돈다.
        foreach (ResourceType resourceType in EnumerateProducedResourceTypes())
        {
            int produced = CalculateYield(resourceType, staffingRatio);
            Debug.Log($"[Factory] {name} 정산 - staffingRatio: {staffingRatio:F2}, areaYieldMultiplier: {GetAreaYieldMultiplier(resourceType):F2}, terrainYieldMultiplier: {TerrainYieldMultiplier:F2}, produced: {produced} ({resourceType})");
            _resourceManager.Add(resourceType, produced);
        }
    }

    // 정산 없이 '다음 정산 예상 생산량'을 자원 종류별로 into에 누적한다(UI 표기용).
    // 실제 정산(OnSettlement)과 같은 공식(CalculateYield)을 쓰므로 표시값과 실제 지급값이 일치한다.
    public void AccumulateProjectedProduction(IDictionary<ResourceType, int> into)
    {
        if (into == null || _data == null || _gridMap == null)
            return;

        float staffingRatio = _population != null ? _population.StaffingRatio : 0f;

        foreach (ResourceType resourceType in EnumerateProducedResourceTypes())
        {
            int produced = CalculateYield(resourceType, staffingRatio);
            if (produced <= 0)
                continue;

            into.TryGetValue(resourceType, out int current);
            into[resourceType] = current + produced;
        }
    }
}
