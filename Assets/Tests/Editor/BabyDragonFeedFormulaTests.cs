using NUnit.Framework;

public class BabyDragonFeedFormulaTests
{
    // baseFeed, perSameType, perTotal, sameTypeCount, totalCount, expected
    // 첫 마리는 추가분이 붙지 않는다(마릿수 - 1이 곱해진다).
    [TestCase(3, 2, 1, 1, 1, 3)]
    [TestCase(3, 2, 1, 2, 2, 6)]
    [TestCase(3, 2, 1, 1, 3, 5)]
    [TestCase(3, 2, 1, 3, 5, 11)]
    [TestCase(3, 0, 0, 4, 9, 3)]
    // 아직 아무것도 등록되지 않은 상태(0)에서도 음수 추가분이 나오지 않는다.
    [TestCase(3, 2, 1, 0, 0, 3)]
    public void ResolveDailyFeed_AddsPerSameTypeAndPerTotalBeyondFirst(
        int baseFeed,
        int additionalFeedPerSameType,
        int additionalFeedPerTotal,
        int sameTypeCount,
        int totalCount,
        int expected
    )
    {
        int feed = BabyDragonFeedFormula.ResolveDailyFeed(
            baseFeed,
            additionalFeedPerSameType,
            additionalFeedPerTotal,
            sameTypeCount,
            totalCount
        );

        Assert.That(feed, Is.EqualTo(expected));
    }
}
