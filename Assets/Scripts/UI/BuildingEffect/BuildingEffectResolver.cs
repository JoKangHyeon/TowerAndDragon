using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 하나의 상태와 지역 페널티를 "지금 화면에 그릴 표식 목록"으로 바꾸는 순수 규칙이다.
/// MonoBehaviour를 모르므로 단위 테스트가 가능하다(TerrainUpkeepRules·BuildingTerrainExposure와 같은 계층).
///
/// 수치를 스스로 만들어내지 않는다 - 유지비는 TerrainUpkeepRules.ResolveFacilityUpkeep(실제 정산과
/// 같은 함수)를, 감소율은 TerrainPenaltyModifiers를 그대로 쓴다. 표식에 뜬 숫자와 다음 아침에
/// 실제로 빠져나가는 양이 어긋날 수 없게 하기 위함이다.
/// </summary>
public static class BuildingEffectResolver
{
    // 페널티가 없는 상태의 배율. 부동소수 오차로 0.9999가 나올 수 있어 Approximately로 함께 판정한다.
    private const float NEUTRAL_MULTIPLIER = 1f;

    /// <summary>
    /// building에 지금 걸려 있는 효과를 into에 채운다. into는 호출자가 소유하는 재사용 버퍼이며
    /// 여기서 먼저 비운다(ResourceForecast.CollectBreakdown과 같은 관례).
    ///
    /// population이 null이면 인구를 배치할 수 없는 건물이라 유지비 표식을 붙이지 않는다
    /// (TerrainUpkeepSystem.CollectFacilities가 대상을 고르는 기준과 같다).
    /// </summary>
    public static void Collect(
        Building building,
        in TerrainPenaltyModifiers modifiers,
        IPopulationAllocationTarget population,
        List<BuildingEffectDescriptor> into) =>
        Collect(building, modifiers, population?.AssignedPopulation, into);

    /// <summary>
    /// 배치 인구를 수치로 직접 받는 판. 아직 배치되지 않은 건물의 미리보기
    /// (PlacementYieldEstimator - "정원을 다 채웠다면")가 쓴다.
    /// assignedPopulation이 null이면 인구를 배치할 수 없는 건물이라 유지비 표식을 붙이지 않는다.
    /// </summary>
    public static void Collect(
        Building building,
        in TerrainPenaltyModifiers modifiers,
        int? assignedPopulation,
        List<BuildingEffectDescriptor> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        if (building == null)
        {
            return;
        }

        if (building.IsSuspended)
        {
            into.Add(BuildingEffectDescriptor.Flag(BuildingEffectKind.Suspended));
        }

        // 지형 페널티는 건물 종류를 가리지 않고 계산된다 - 사막 테이블 하나가 생산량과 공격속도를
        // 함께 깎으므로, 걸러내지 않으면 타워에 "생산량 -30%", 생산시설에 "공격속도 -30%"가 뜬다.
        // 그래서 각 배율을 실제로 소비하는 쪽에만 표식을 붙인다
        // (YieldMultiplier -> Factory.CalculateYield, AttackSpeedMultiplier -> TowerAttack).
        if (building is Factory && IsReduced(modifiers.YieldMultiplier))
        {
            into.Add(BuildingEffectDescriptor.Reduction(
                BuildingEffectKind.YieldDown,
                NEUTRAL_MULTIPLIER - modifiers.YieldMultiplier));
        }

        if (building is Tower && IsReduced(modifiers.AttackSpeedMultiplier))
        {
            into.Add(BuildingEffectDescriptor.Reduction(
                BuildingEffectKind.AttackSpeedDown,
                NEUTRAL_MULTIPLIER - modifiers.AttackSpeedMultiplier));
        }

        if (!assignedPopulation.HasValue)
        {
            return;
        }

        // 유지비는 반대로 건물 종류를 가리지 않는다 - TerrainUpkeepSystem이 인구가 배치된 시설이면
        // 생산시설이든 타워든 똑같이 걷어간다.
        TerrainUpkeepRules.FacilityUpkeep upkeep =
            TerrainUpkeepRules.ResolveFacilityUpkeep(assignedPopulation.Value, modifiers);

        // 인구가 0이면 소모량도 0이지만 표식은 계속 띄운다 - "이 땅은 나무를 먹는다"는 사실을
        // 인구를 넣기 전에 알아야 하기 때문이다. 라벨에 뜨는 0은 다음 정산의 실제 차감액과 같다.
        if (modifiers.WoodUpkeepPerPopulation > 0f)
        {
            into.Add(BuildingEffectDescriptor.Upkeep(BuildingEffectKind.WoodUpkeep, upkeep.WoodAmount));
        }

        if (modifiers.StoneUpkeepPerPopulation > 0f)
        {
            into.Add(BuildingEffectDescriptor.Upkeep(BuildingEffectKind.StoneUpkeep, upkeep.StoneAmount));
        }
    }

    private static bool IsReduced(float multiplier) =>
        multiplier < NEUTRAL_MULTIPLIER && !Mathf.Approximately(multiplier, NEUTRAL_MULTIPLIER);
}
