using System.Collections.Generic;
using NUnit.Framework;

public class TowerUpkeepRulesTests
{
    private const int WOOD_PER_POPULATION = 1;
    private const int STONE_PER_POPULATION = 1;
    private const int SPECIALIZED_PER_POPULATION = 1;

    private static ResourceAmount[] PerPopulation(params (ResourceType type, int amount)[] entries)
    {
        var amounts = new ResourceAmount[entries.Length];

        for (int i = 0; i < entries.Length; i++)
        {
            amounts[i] = new ResourceAmount { Type = entries[i].type, Amount = entries[i].amount };
        }

        return amounts;
    }

    private static TowerUpkeepRules.TowerUpkeep Tower(
        int assignedPopulation,
        params (ResourceType type, int amount)[] entries) =>
        new TowerUpkeepRules.TowerUpkeep(assignedPopulation, PerPopulation(entries));

    [Test]
    public void AccumulateRequirement_MultipliesPerPopulationByAssignedPopulation()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(5, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(7, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var into = new Dictionary<ResourceType, int>();
        TowerUpkeepRules.AccumulateRequirement(towers, into);

        Assert.That(into[ResourceType.Wood], Is.EqualTo(12));
    }

    [Test]
    public void AccumulateRequirement_SumsEachResourceTypeSeparately()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(5, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(7,
                (ResourceType.Stone, STONE_PER_POPULATION),
                (ResourceType.FlameHeart, SPECIALIZED_PER_POPULATION)),
        };

        var into = new Dictionary<ResourceType, int>();
        TowerUpkeepRules.AccumulateRequirement(towers, into);

        Assert.That(into[ResourceType.Wood], Is.EqualTo(5));
        Assert.That(into[ResourceType.Stone], Is.EqualTo(7));
        Assert.That(into[ResourceType.FlameHeart], Is.EqualTo(7));
    }

    [Test]
    public void AccumulateRequirement_AddsOnTopOfExistingEntries()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(3, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var into = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 10 };
        TowerUpkeepRules.AccumulateRequirement(towers, into);

        Assert.That(into[ResourceType.Wood], Is.EqualTo(13));
    }

    [Test]
    public void HasUpkeep_IsFalseWithoutPopulationOrAmount()
    {
        Assert.That(Tower(0, (ResourceType.Wood, WOOD_PER_POPULATION)).HasUpkeep, Is.False);
        Assert.That(Tower(5, (ResourceType.Wood, 0)).HasUpkeep, Is.False);
        Assert.That(Tower(5).HasUpkeep, Is.False);
        Assert.That(Tower(5, (ResourceType.Wood, WOOD_PER_POPULATION)).HasUpkeep, Is.True);
    }

    // 같은 미납분을 메우는 데 꺼야 하는 타워 수가 가장 적어야 한다 - 기여가 큰 쪽부터 고른다.
    [Test]
    public void SelectDeactivationTargets_PicksLargestContributorFirst()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(2, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(9, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(4, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var shortfall = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 8 };
        var into = new List<int>();

        TowerUpkeepRules.SelectDeactivationTargets(towers, shortfall, into);

        Assert.That(into, Is.EqualTo(new List<int> { 1 }));
    }

    [Test]
    public void SelectDeactivationTargets_KeepsGoingUntilShortfallIsCovered()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(5, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(4, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(3, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var shortfall = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 8 };
        var into = new List<int>();

        TowerUpkeepRules.SelectDeactivationTargets(towers, shortfall, into);

        Assert.That(into, Is.EqualTo(new List<int> { 0, 1 }));
    }

    // 이미 다 낸 자원만 내는 타워는 꺼봐야 미납분이 줄지 않으므로 고르지 않는다.
    [Test]
    public void SelectDeactivationTargets_IgnoresTowersPayingOnlyFullyPaidResources()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(9, (ResourceType.Stone, STONE_PER_POPULATION)),
            Tower(3, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var shortfall = new Dictionary<ResourceType, int>
        {
            [ResourceType.Wood] = 3,
            [ResourceType.Stone] = 0,
        };
        var into = new List<int>();

        TowerUpkeepRules.SelectDeactivationTargets(towers, shortfall, into);

        Assert.That(into, Is.EqualTo(new List<int> { 1 }));
    }

    [Test]
    public void SelectDeactivationTargets_NoShortfall_SelectsNothing()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(5, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var shortfall = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 0 };
        var into = new List<int>();

        TowerUpkeepRules.SelectDeactivationTargets(towers, shortfall, into);

        Assert.That(into, Is.Empty);
    }

    // 남은 타워를 다 꺼도 미납분이 남으면 무한 루프에 빠지지 않고 종료해야 한다.
    [Test]
    public void SelectDeactivationTargets_ShortfallExceedsAllTowers_StopsAfterExhausting()
    {
        var towers = new List<TowerUpkeepRules.TowerUpkeep>
        {
            Tower(2, (ResourceType.Wood, WOOD_PER_POPULATION)),
            Tower(1, (ResourceType.Wood, WOOD_PER_POPULATION)),
        };

        var shortfall = new Dictionary<ResourceType, int> { [ResourceType.Wood] = 100 };
        var into = new List<int>();

        TowerUpkeepRules.SelectDeactivationTargets(towers, shortfall, into);

        Assert.That(into, Is.EqualTo(new List<int> { 0, 1 }));
    }
}
