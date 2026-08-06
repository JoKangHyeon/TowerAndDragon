/// <summary>
/// 지역 유지비 정산 1회의 결과. UI 통보(DailySettlementManager.TerrainSettlementCompleted)와
/// 로그가 같은 값을 보도록 하나의 구조체로 묶는다(PopulationUpkeepResult와 같은 역할).
/// </summary>
public readonly struct TerrainUpkeepResult
{
    public int RequiredWood { get; }
    public int ConsumedWood { get; }
    public int RequiredStone { get; }
    public int ConsumedStone { get; }

    /// <summary>미납분 때문에 인구가 회수되어 비활성화된 시설 수.</summary>
    public int DeactivatedFacilityCount { get; }

    public int ShortfallWood => RequiredWood - ConsumedWood;
    public int ShortfallStone => RequiredStone - ConsumedStone;
    public bool HasUpkeep => RequiredWood > 0 || RequiredStone > 0;

    public TerrainUpkeepResult(
        int requiredWood,
        int consumedWood,
        int requiredStone,
        int consumedStone,
        int deactivatedFacilityCount)
    {
        RequiredWood = requiredWood;
        ConsumedWood = consumedWood;
        RequiredStone = requiredStone;
        ConsumedStone = consumedStone;
        DeactivatedFacilityCount = deactivatedFacilityCount;
    }
}
