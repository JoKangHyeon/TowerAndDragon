using UnityEngine;

// 생산시설 공통 구현체. 서브클래스 없이 ResourceProductionData만 갈아끼워 벌목장/채굴장/광산 등을 표현한다(Tower/TowerData와 동일한 패턴).
public class Factory : Building
{
    [SerializeField] private ResourceProductionData _data;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;

    private bool _isInitialized;

    // 건설 가능 여부 판정에 필요 - GridMap.CanConstructResourceFootprint 호출 시 전달한다.
    public ResourceType RequiredResourceNode => _data != null ? _data.RequiredResourceNode : ResourceType.None;

    public bool IsInitialized => _isInitialized;

    // 프리팹은 씬 오브젝트(ResourceManager/CycleManager)를 들고 있을 수 없으므로,
    // 건설 직후 FactoryResourceCoordinator가 주입한다(TowerPopulation.Initialize와 동일한 패턴).
    public bool Initialize(ResourceManager resourceManager, CycleManager cycleManager)
    {
        if (_isInitialized || resourceManager == null || cycleManager == null)
            return false;

        _resourceManager = resourceManager;
        _cycleManager = cycleManager;
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
        if (_data == null || _resourceManager == null)
            return;

        _resourceManager.Add(_data.ProducedResourceType, _data.YieldPerCycle);
    }
}
