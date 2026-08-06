using System.Collections.Generic;
using NUnit.Framework;

public class TerrainUpkeepRulesTests
{
    private readonly List<int> _targets = new();

    private static List<TerrainUpkeepRules.FacilityUpkeep> Facilities(
        params (int wood, int stone)[] entries)
    {
        var facilities = new List<TerrainUpkeepRules.FacilityUpkeep>();

        foreach ((int wood, int stone) in entries)
        {
            facilities.Add(new TerrainUpkeepRules.FacilityUpkeep(wood, stone));
        }

        return facilities;
    }

    [Test]
    public void AccumulateRequirement_SumsPerResource()
    {
        TerrainUpkeepRules.AccumulateRequirement(
            Facilities((3, 0), (0, 5), (2, 1)),
            out int requiredWood,
            out int requiredStone);

        Assert.That(requiredWood, Is.EqualTo(5));
        Assert.That(requiredStone, Is.EqualTo(6));
    }

    [Test]
    public void AccumulateRequirement_EmptyList_IsZero()
    {
        TerrainUpkeepRules.AccumulateRequirement(
            Facilities(),
            out int requiredWood,
            out int requiredStone);

        Assert.That(requiredWood, Is.Zero);
        Assert.That(requiredStone, Is.Zero);
    }

    [Test]
    public void SelectDeactivationTargets_NoShortfall_SelectsNothing()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((3, 0), (4, 0)),
            0,
            0,
            _targets);

        Assert.That(_targets, Is.Empty);
    }

    // 꺼야 하는 시설 수가 최소가 되도록 기여가 큰 시설부터 고른다.
    [Test]
    public void SelectDeactivationTargets_PicksLargestContributorFirst()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((1, 0), (5, 0), (2, 0)),
            4,
            0,
            _targets);

        Assert.That(_targets, Is.EqualTo(new List<int> { 1 }));
    }

    [Test]
    public void SelectDeactivationTargets_KeepsGoingUntilShortfallCovered()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((3, 0), (2, 0), (1, 0)),
            5,
            0,
            _targets);

        Assert.That(_targets, Is.EqualTo(new List<int> { 0, 1 }));
    }

    // 기여가 같으면 입력 순서가 빠른 쪽 - 정산 결과가 건물 순회 순서에만 의존해 재현 가능해야 한다.
    [Test]
    public void SelectDeactivationTargets_TieBreaksByInputOrder()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((2, 0), (2, 0)),
            1,
            0,
            _targets);

        Assert.That(_targets, Is.EqualTo(new List<int> { 0 }));
    }

    // 나무만 부족하면 돌만 내는 시설은 꺼도 미납분이 줄지 않으므로 고르지 않는다.
    [Test]
    public void SelectDeactivationTargets_IgnoresFacilitiesThatDoNotHelpTheShortfall()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((0, 9), (1, 0)),
            1,
            0,
            _targets);

        Assert.That(_targets, Is.EqualTo(new List<int> { 1 }));
    }

    [Test]
    public void SelectDeactivationTargets_CoversBothResources()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((4, 0), (0, 3)),
            4,
            3,
            _targets);

        Assert.That(_targets, Is.EquivalentTo(new List<int> { 0, 1 }));
    }

    // 남은 시설을 다 꺼도 못 메우면 무한 루프에 빠지지 않고 종료해야 한다.
    [Test]
    public void SelectDeactivationTargets_UnpayableShortfall_StopsWithoutHanging()
    {
        TerrainUpkeepRules.SelectDeactivationTargets(
            Facilities((1, 0)),
            100,
            0,
            _targets);

        Assert.That(_targets, Is.EqualTo(new List<int> { 0 }));
    }
}
