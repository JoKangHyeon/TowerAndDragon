using UnityEngine;

/// <summary>
/// 생산시설 하루 산출의 순수 계산 규칙. 실제 정산(<see cref="Factory"/>.OnSettlement)과 배치 미리보기가
/// 반드시 같은 값을 내야 하므로 산식은 여기 하나만 둔다 - 두 벌이 되면 한쪽만 고쳐져 조용히 어긋난다.
/// MonoBehaviour를 모르므로 씬 없이 단위 테스트할 수 있다(TerrainUpkeepRules·PopulationUpkeepRules와 같은 분리).
/// </summary>
public static class FactoryYieldRules
{
    /// <summary>버프·페널티가 없을 때의 배율.</summary>
    public const float NEUTRAL_MULTIPLIER = 1f;

    /// <summary>정원을 100% 채웠을 때의 충원율 - 최대 생산량(상한 표시·배치 미리보기) 계산에 쓴다.</summary>
    public const float FULL_STAFFING_RATIO = 1f;

    /// <summary>
    /// 생산시설 한 채가 하루에 내는 산출량.
    ///
    /// footprintYield에는 이미 셀별 자원 배율과 청크 단위 연구·랜드마크·어미용 강화가 반영돼 있다
    /// (GridMap.GetFootprintYield 참고).
    ///
    /// 충원율에 비례해 오르며 최소 인구 문턱은 없다 - 1명만 배치해도 그만큼 생산하고,
    /// 상한은 충원율이 1을 넘지 못하는 것으로 자연히 걸린다.
    ///
    /// 반올림을 인구를 곱한 뒤와 배율을 곱한 뒤 두 번 하는 것은 기존 정산 동작 그대로다.
    /// </summary>
    public static int Compute(
        int footprintYield,
        float staffingRatio,
        float areaYieldMultiplier,
        float terrainYieldMultiplier)
    {
        int staffed = Mathf.RoundToInt(footprintYield * Mathf.Clamp01(staffingRatio));

        return Mathf.RoundToInt(staffed * areaYieldMultiplier * terrainYieldMultiplier);
    }
}
