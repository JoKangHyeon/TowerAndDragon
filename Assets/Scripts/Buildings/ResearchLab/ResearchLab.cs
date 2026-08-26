using UnityEngine;

public sealed class ResearchLab : Building
{
    [SerializeField] private ResearchLabData _data;

    // 지역(지형) 페널티 조회원 - Factory.SetTerrainPenaltyQuery와 같은 관용구.
    // TerrainPenaltyCoordinator가 주입하며, 미배선 씬(튜토리얼 등)에서는 null로 남아
    // 페널티 없이 동작한다.
    private IBuildingTerrainPenaltyQuery _terrainPenaltyQuery;

    public ResearchLabData Data => _data;
    public ResearchLabPopulation Population => GetComponent<ResearchLabPopulation>();

    public void SetTerrainPenaltyQuery(IBuildingTerrainPenaltyQuery terrainPenaltyQuery) =>
        _terrainPenaltyQuery = terrainPenaltyQuery;

    public float TerrainYieldMultiplier =>
        _terrainPenaltyQuery != null
            ? _terrainPenaltyQuery.Resolve(this).YieldMultiplier
            : FactoryYieldRules.NEUTRAL_MULTIPLIER;
}
