/// <summary>
/// 타워 유지비 정산 1회의 결과. UI 통보(DailySettlementManager.TowerSettlementCompleted)와
/// 로그가 같은 값을 보도록 하나의 구조체로 묶는다(TerrainUpkeepResult와 같은 역할).
///
/// 자원 종류별 내역이 아니라 합계만 담는 이유: 타워는 속성마다 내는 자원이 달라 종류가 열려 있고,
/// 종류별 예상 내역은 자원 예측(ResourceForecast)이 이미 출처별로 보여준다.
/// 여기서 필요한 것은 "얼마가 모자라 몇 기가 꺼졌는가"다.
/// </summary>
public readonly struct TowerUpkeepResult
{
    public int RequiredTotal { get; }
    public int ConsumedTotal { get; }

    /// <summary>미납분 때문에 인구가 회수되어 비활성화된 타워 수.</summary>
    public int DeactivatedTowerCount { get; }

    public int Shortfall => RequiredTotal - ConsumedTotal;
    public bool HasUpkeep => RequiredTotal > 0;

    public TowerUpkeepResult(int requiredTotal, int consumedTotal, int deactivatedTowerCount)
    {
        RequiredTotal = requiredTotal;
        ConsumedTotal = consumedTotal;
        DeactivatedTowerCount = deactivatedTowerCount;
    }
}
