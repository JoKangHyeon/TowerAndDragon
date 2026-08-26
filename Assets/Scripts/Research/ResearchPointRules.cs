using UnityEngine;

/// <summary>
/// 연구소 하루 산출의 순수 계산 규칙. 실제 정산(<see cref="ResearchManager"/>.GrantResearchPoints)과
/// UI 미리보기(<see cref="ResearchManager"/>.PreviewResearchPointsPerDay)가 반드시 같은 값을 내야
/// 하므로 산식은 여기 하나만 둔다(FactoryYieldRules·TerrainUpkeepRules와 같은 분리).
/// MonoBehaviour를 모르므로 씬 없이 단위 테스트할 수 있다.
/// </summary>
public static class ResearchPointRules
{
    /// <summary>
    /// 연구소 한 채가 하루에 내는 RP.
    ///
    /// terrainYieldMultiplier에는 이미 지형 페널티(사막 등)와 완화 소스(새끼용) 배율이
    /// 반영돼 있다(TerrainPenaltySystem 참고). 반올림은 Factory.CalculateYield와 같이 한 번만 한다.
    /// </summary>
    public static int Compute(
        int assignedPopulation,
        int pointsPerPopulation,
        float terrainYieldMultiplier) =>
        Mathf.RoundToInt(assignedPopulation * pointsPerPopulation * terrainYieldMultiplier);
}
