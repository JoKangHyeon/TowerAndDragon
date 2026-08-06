// 건물 하나가 실제로 받는 지역 페널티의 최종 형태. 지형별 원본 값(TerrainPenaltyEntry)에
// 풋프린트 지형 비율과 외부 완화 배율까지 모두 반영된 값이므로, 소비처(Factory/TowerAttack/
// TerrainUpkeepSystem)는 이 값을 그대로 곱하거나 더하기만 하면 된다.
// 유지비가 float인 이유: 건물이 여러 지형에 걸치면 비율 가중으로 소수가 나오며, 정수화는
// 인구를 곱한 뒤 시설 단위로 한 번만 한다(GridMap.GetFootprintYield가 청크 단위로 한 번만
// 반올림하는 것과 같은 이유 - 중간 단계마다 반올림하면 오차가 누적된다).
public readonly struct TerrainPenaltyModifiers
{
    public float YieldMultiplier { get; }
    public float AttackSpeedMultiplier { get; }
    public float WoodUpkeepPerPopulation { get; }
    public float StoneUpkeepPerPopulation { get; }

    public static TerrainPenaltyModifiers Neutral => new TerrainPenaltyModifiers(1f, 1f, 0f, 0f);

    public TerrainPenaltyModifiers(
        float yieldMultiplier,
        float attackSpeedMultiplier,
        float woodUpkeepPerPopulation,
        float stoneUpkeepPerPopulation)
    {
        YieldMultiplier = yieldMultiplier;
        AttackSpeedMultiplier = attackSpeedMultiplier;
        WoodUpkeepPerPopulation = woodUpkeepPerPopulation;
        StoneUpkeepPerPopulation = stoneUpkeepPerPopulation;
    }
}
