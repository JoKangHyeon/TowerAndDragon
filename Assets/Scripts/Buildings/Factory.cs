using System.Collections.Generic;
using UnityEngine;

// 생산시설 공통 구현체. 서브클래스 없이 ResourceProductionData만 갈아끼워 벌목장/채굴장/광산 등을 표현한다(Tower/TowerData와 동일한 패턴).
public class Factory : Building
{
    // 새끼용 버프가 없을 때의 생산량 배율(BabyDragonBuffSystem이 매일 밤 종료 시 갱신).
    public const float NEUTRAL_YIELD_MULTIPLIER = 1f;

    [SerializeField] private ResourceProductionData _data;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    private bool _isInitialized;
    private FactoryPopulation _population;
    private float _areaYieldMultiplier = NEUTRAL_YIELD_MULTIPLIER;

    public void SetAreaYieldMultiplier(float multiplier)
    {
        _areaYieldMultiplier = multiplier;
    }

    // 건설 가능 여부 판정에 필요 - GridMap.CanConstructResourceFootprint 호출 시 전달한다.
    public ResourceType RequiredResourceNode => _data != null ? _data.RequiredResourceNode : ResourceType.None;

    // FactoryPopulation.Initialize에서 배치 가능 인구를 읽어가기 위해 필요(TowerPopulation과 동일한 용도).
    public ResourceProductionData Data => _data;
    public override IReadOnlyList<ResourceAmount> BuildCost => _data != null ? _data.BuildCost : base.BuildCost;
    public override int PopulationCapacity => _data != null ? _data.PopulationCapacity : base.PopulationCapacity;

    public bool IsInitialized => _isInitialized;

    // 프리팹은 씬 오브젝트(ResourceManager/CycleManager/GridMap)를 들고 있을 수 없으므로,
    // 건설 직후 FactoryResourceCoordinator가 주입한다(TowerPopulation.Initialize와 동일한 패턴).
    public bool Initialize(ResourceManager resourceManager, CycleManager cycleManager, GridMap gridMap)
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

        // ProducedResourceType이 여러 비트를 동시에 가질 수 있다(슬라임 농장 - 풋프린트에 걸친 지형별 슬라임을
        // 각각 따로 합산해서 정산). 단일 비트(기존 농장/벌목장/채석장)면 이 반복은 그냥 한 번만 돈다.
        foreach (ResourceType resourceType in GridMap.EnumerateResourceFlags(_data.ProducedResourceType))
        {
            // 생산량은 이 생산시설의 footprint에 속한 셀들이 보유한 자원별 생산량의 합이다(GridMap.GetFootprintYield 참고).
            int footprintYield = _gridMap.GetFootprintYield(this, resourceType);
            int produced = _data.CalculateYield(footprintYield, staffingRatio);
            produced = Mathf.RoundToInt(produced * _areaYieldMultiplier);
            Debug.Log($"[Factory] {name} 정산 - footprintYield: {footprintYield}, staffingRatio: {staffingRatio:F2}, produced: {produced} ({resourceType})");
            _resourceManager.Add(resourceType, produced);
        }
    }
}
