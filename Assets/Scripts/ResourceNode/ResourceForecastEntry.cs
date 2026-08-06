/// <summary>자원 하루 증감의 출처. 툴팁이 항목별 라벨을 고르는 데 쓴다.</summary>
public enum ResourceForecastSource
{
    /// <summary>생산시설(Factory)의 예상 생산량.</summary>
    Production,

    /// <summary>인구 식량 유지비(PopulationUpkeepSystem).</summary>
    PopulationUpkeep,

    /// <summary>설원·암석 등 지역 자재 유지비(TerrainUpkeepSystem).</summary>
    TerrainUpkeep,
}

/// <summary>
/// 자원 하나의 하루 증감 내역 한 줄이다. Amount는 생산이면 양수, 소모면 음수로 담는다.
/// 부호를 값에 실어두면 툴팁이 합계를 그대로 더해서 낼 수 있다.
/// </summary>
public readonly struct ResourceForecastEntry
{
    public ResourceType Type { get; }
    public ResourceForecastSource Source { get; }
    public int Amount { get; }

    public ResourceForecastEntry(ResourceType type, ResourceForecastSource source, int amount)
    {
        Type = type;
        Source = source;
        Amount = amount;
    }
}
