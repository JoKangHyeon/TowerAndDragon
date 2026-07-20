using UnityEngine;

// 생산시설 공통 구현체. 서브클래스 없이 ResourceProductionData만 갈아끼워 벌목장/채굴장/광산 등을 표현한다(Tower/TowerData와 동일한 패턴).
public class Factory : Building
{
    [SerializeField] private ResourceProductionData _data;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private CycleManager _cycleManager;

    // 건설 가능 여부 판정에 필요 - GridMap.CanConstructResourceFootprint 호출 시 전달한다.
    public ResourceType RequiredResourceNode => _data != null ? _data.RequiredResourceNode : ResourceType.None;

    private void OnEnable()
    {
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.AddListener(OnSettlement);
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.RemoveListener(OnSettlement);
    }

    private void OnSettlement(int currentCycle)
    {
        if (_data == null || _resourceManager == null)
            return;

        _resourceManager.Add(_data.ProducedResourceType, _data.YieldPerCycle);
    }
}
