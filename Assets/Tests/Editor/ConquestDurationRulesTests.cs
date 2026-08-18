using NUnit.Framework;
using UnityEngine;

public class ConquestDurationRulesTests
{
    private const int MINIMUM_DAYS_REQUIRED = 1;
    private const int NEAR_DISTANCE_THRESHOLD = 2;
    private const int NEAR_DAYS_REDUCTION = 1;

    [Test]
    public void ResolveDaysRequired_NearOrigin_ReducesOneDay()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            3,
            0,
            new Vector2Int(0, 2),
            Vector2Int.zero,
            NEAR_DISTANCE_THRESHOLD,
            NEAR_DAYS_REDUCTION,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(2));
    }

    [Test]
    public void ResolveDaysRequired_DiagonalTwoSteps_DoesNotCountAsNear()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            3,
            0,
            new Vector2Int(2, 2),
            Vector2Int.zero,
            NEAR_DISTANCE_THRESHOLD,
            NEAR_DAYS_REDUCTION,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(3));
    }

    [Test]
    public void ResolveDaysRequired_NeverDropsBelowMinimum()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            1,
            1,
            new Vector2Int(1, 0),
            Vector2Int.zero,
            NEAR_DISTANCE_THRESHOLD,
            NEAR_DAYS_REDUCTION,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(MINIMUM_DAYS_REQUIRED));
    }

    [Test]
    public void ResolveDaysRequired_CombinesExternalReduction()
    {
        int days = ConquestDurationRules.ResolveDaysRequired(
            4,
            1,
            new Vector2Int(2, 0),
            Vector2Int.zero,
            NEAR_DISTANCE_THRESHOLD,
            NEAR_DAYS_REDUCTION,
            MINIMUM_DAYS_REQUIRED);

        Assert.That(days, Is.EqualTo(2));
    }
}
