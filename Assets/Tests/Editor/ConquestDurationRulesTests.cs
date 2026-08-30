using NUnit.Framework;
using UnityEngine;

public class ConquestDurationRulesTests
{
    private const int MINIMUM_DAYS_REQUIRED = 1;
    private const int BASE_DAYS_AT_INNER_RING = 1;
    private const int DAYS_PER_DISTANCE_STEP = 1;

    [Test]
    public void ResolveDaysRequired_InnerGrassRingStaysOneDay()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            new Vector2Int(1, 0),
            Vector2Int.zero,
            BASE_DAYS_AT_INNER_RING,
            DAYS_PER_DISTANCE_STEP,
            0,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(1));
    }

    [Test]
    public void ResolveDaysRequired_IncreasesAfterInnerGrassRing()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            new Vector2Int(0, 2),
            Vector2Int.zero,
            BASE_DAYS_AT_INNER_RING,
            DAYS_PER_DISTANCE_STEP,
            0,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(2));
    }

    [Test]
    public void ResolveDaysRequired_DiagonalOffsetCountsBothAxes()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            new Vector2Int(2, 2),
            Vector2Int.zero,
            BASE_DAYS_AT_INNER_RING,
            DAYS_PER_DISTANCE_STEP,
            0,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(4));
    }

    [Test]
    public void ResolveDaysRequired_NeverDropsBelowMinimum()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            new Vector2Int(1, 0),
            Vector2Int.zero,
            BASE_DAYS_AT_INNER_RING,
            DAYS_PER_DISTANCE_STEP,
            10,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(MINIMUM_DAYS_REQUIRED));
    }

    [Test]
    public void ResolveDaysRequired_CombinesExternalReduction()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            new Vector2Int(2, 0),
            Vector2Int.zero,
            BASE_DAYS_AT_INNER_RING,
            DAYS_PER_DISTANCE_STEP,
            1,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(MINIMUM_DAYS_REQUIRED));
    }

    [Test]
    public void CalculateOrthogonalDistance_UsesManhattanDistance()
    {
        int distance = ConquestDurationRules.CalculateOrthogonalDistance(
            new Vector2Int(-3, 2),
            Vector2Int.zero);

        Assert.That(distance, Is.EqualTo(5));
    }
}
