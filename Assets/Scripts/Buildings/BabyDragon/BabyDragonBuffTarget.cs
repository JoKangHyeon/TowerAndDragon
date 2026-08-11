/// <summary>
/// 새끼용 한 마리가 버프하고 있는 대상 하나. 생산시설이 여러 자원을 내면(슬라임 농장) 같은 시설이
/// 자원마다 한 줄씩 나온다 - 버프 대상 자원만 골라 곱하는 실제 계산과 같은 단위다.
/// 수량은 담지 않는다. Factory.GetCurrentYield가 늘 최신값을 주므로 표시 시점에 읽는 편이 어긋나지 않는다.
/// </summary>
public readonly struct BabyDragonBuffTarget
{
    public Factory Factory { get; }
    public ResourceType ResourceType { get; }

    public BabyDragonBuffTarget(Factory factory, ResourceType resourceType)
    {
        Factory = factory;
        ResourceType = resourceType;
    }
}
