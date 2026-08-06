using System.Collections.Generic;

/// <summary>
/// 건물 풋프린트가 각 지형에 얼마만큼 걸쳐 있는지를 셀 개수 비율로 환산하는 순수 계산이다.
/// 지역 페널티 값 자체는 지역 전체가 동일하고, 건물이 받는 양만 이 비율로 가중된다 -
/// 청크 경계에 반씩 걸친 건물이 페널티도 반만 받게 하기 위함이다.
/// GridMap 순회는 호출자(TerrainPenaltySystem)가 하고 여기에는 지형 목록만 넘어오므로,
/// 씬 없이 단위 테스트할 수 있다(PopulationUpkeepRules와 같은 분리).
/// </summary>
public static class BuildingTerrainExposure
{
    /// <summary>
    /// 지형별 셀 비율(합계 1)을 into에 채운다. into는 먼저 비워진다.
    /// 페널티가 없는 지형(초원·도로·물)도 그대로 담기지만, 테이블 값이 0이라 결과에 영향이 없다 -
    /// 분모에는 포함되어야 하므로 여기서 걸러내지 않는다.
    /// </summary>
    public static void Accumulate(
        IReadOnlyList<TerrainType> footprintTerrains,
        IDictionary<TerrainType, float> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        if (footprintTerrains == null || footprintTerrains.Count == 0)
        {
            return;
        }

        for (int i = 0; i < footprintTerrains.Count; i++)
        {
            TerrainType terrain = footprintTerrains[i];
            into.TryGetValue(terrain, out float count);
            into[terrain] = count + 1f;
        }

        float cellCount = footprintTerrains.Count;

        // 키를 순회하면서 값을 바꿀 수 없으므로 키 목록을 먼저 뽑는다. 지형은 최대 7종이라
        // 이 할당은 건물당 최초 1회(캐시 미스)로 제한된다.
        var terrains = new List<TerrainType>(into.Keys);

        foreach (TerrainType terrain in terrains)
        {
            into[terrain] = into[terrain] / cellCount;
        }
    }
}
