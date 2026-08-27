using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "지금 이 자리에 이 생산시설을 지으면 하루에 얼마가 나오는가"를 계산한다.
///
/// 아직 배치되지 않은 풋프린트를 다루는 것만 다를 뿐, 산식은 실제 정산과 같은 함수
/// (FactoryYieldRules · TerrainUpkeepRules)를 그대로 부른다 - 미리보기 숫자와 다음 아침에
/// 실제로 들어올 양이 갈라지면 이 표시는 없느니만 못하다.
///
/// 인구는 정원 만충(FULL_STAFFING_RATIO)을 가정한다. 배치 시점의 실제 배치 인구는 0이라
/// 그대로 계산하면 어디에 지어도 0이 나와 자리를 비교할 수 없기 때문이다.
///
/// 새끼용 생산 버프는 반영하지 않는다 - 용은 배치 후에도 옮길 수 있어 지금의 배율이 내일의
/// 배율이라는 보장이 없다. 실제보다 낮게 나오는 방향이라 안전한 오차다.
/// (지형 페널티 완화는 위치로 판정되므로 정확히 반영된다 - TerrainPenaltySystem.ResolvePreview 참고.)
///
/// MonoBehaviour가 아니다 - 표시기(UI_PlacementYieldTooltipDriver)가 하나 들고 재사용한다.
/// </summary>
public sealed class PlacementYieldEstimator
{
    private readonly List<ResourceAmount> _yields = new();
    private readonly List<BuildingEffectDescriptor> _effects = new();

    /// <summary>자원별 하루 생산량. 0 이하인 자원은 담지 않는다(Factory.AccumulateProjectedProduction과 같은 관례).</summary>
    public IReadOnlyList<ResourceAmount> Yields => _yields;

    /// <summary>이 자리에서 받게 될 지형 효과(생산량 감소 · 지역 유지비). 없으면 비어 있다.</summary>
    public IReadOnlyList<BuildingEffectDescriptor> Effects => _effects;

    /// <summary>이 계산이 가정한 배치 인구 = 이 시설의 정원.</summary>
    public int PopulationCapacity { get; private set; }

    /// <summary>표시할 이름의 스트링테이블 키.</summary>
    public string NameLocKey { get; private set; }

    /// <summary>
    /// factory를 footprint 자리에 지었을 때의 결과로 이 객체를 갱신한다.
    /// 계산할 수 없으면(데이터·그리드 미비) false를 돌려주고 내용은 비운다.
    ///
    /// worldPosition은 그 자리에 실제로 지었을 때 건물이 놓일 좌표여야 한다
    /// (MouseSelectController.PreviewGroundWorldPosition).
    /// terrainPenaltySystem은 null이어도 된다 - 지형 페널티를 쓰지 않는 씬에서는 페널티 없이 계산한다.
    /// </summary>
    public bool TryEstimate(
        Factory factory,
        GridMap gridMap,
        TerrainPenaltySystem terrainPenaltySystem,
        IReadOnlyList<Vector3Int> footprint,
        Vector3 worldPosition)
    {
        Clear();

        if (factory == null || factory.Data == null || gridMap == null || footprint == null)
        {
            return false;
        }

        NameLocKey = factory.Data.NameLocKey;
        PopulationCapacity = factory.PopulationCapacity;

        TerrainPenaltyModifiers penalty = terrainPenaltySystem != null
            ? terrainPenaltySystem.ResolvePreview(footprint, worldPosition)
            : TerrainPenaltyModifiers.Neutral;

        // 자원 종류마다 따로 돈다 - 슬라임 농장은 지형별 슬라임 5종을 한 시설에서 낸다.
        foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
        {
            int produced = FactoryYieldRules.Compute(
                gridMap.GetFootprintYield(footprint, resourceType),
                FactoryYieldRules.FULL_STAFFING_RATIO,
                FactoryYieldRules.NEUTRAL_MULTIPLIER,
                penalty.YieldMultiplier);

            if (produced <= 0)
            {
                continue;
            }

            _yields.Add(new ResourceAmount { Type = resourceType, Amount = produced });
        }

        // 지형 감소율·유지비 표식은 배치된 건물과 같은 규칙으로 뽑는다 - 판정을 여기 다시 적으면
        // 한쪽만 고쳐져 미리보기와 배치 후 표식이 서로 다른 말을 하게 된다.
        BuildingEffectResolver.Collect(factory, penalty, PopulationCapacity, _effects);

        return true;
    }

    private void Clear()
    {
        _yields.Clear();
        _effects.Clear();
        NameLocKey = string.Empty;
        PopulationCapacity = 0;
    }
}
