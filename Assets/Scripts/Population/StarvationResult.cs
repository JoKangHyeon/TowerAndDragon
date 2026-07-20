/// <summary>
/// 기아 적용 전후의 인구 감소 내역을 전달하는 읽기 전용 결과다.
/// </summary>
public readonly struct StarvationResult
{
    public int RequestedDeaths { get; }
    public int PopulationLost { get; }
    public int AvailablePopulationLost { get; }
    public int TowerPopulationLost { get; }
    public int ProductionPopulationLost { get; }
    public int ConquestPopulationLost { get; }

    public StarvationResult(
        int requestedDeaths,
        int availablePopulationLost,
        int towerPopulationLost,
        int productionPopulationLost,
        int conquestPopulationLost
    )
    {
        RequestedDeaths = requestedDeaths;
        AvailablePopulationLost = availablePopulationLost;
        TowerPopulationLost = towerPopulationLost;
        ProductionPopulationLost = productionPopulationLost;
        ConquestPopulationLost = conquestPopulationLost;
        PopulationLost =
            availablePopulationLost +
            towerPopulationLost +
            productionPopulationLost +
            conquestPopulationLost;
    }
}
