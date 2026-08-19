using System.Collections.Generic;
using NUnit.Framework;

public class SlimeBalanceRulesTests
{
    [Test]
    public void Evaluate_NoEntries_IsBalanced()
    {
        Assert.That(
            SlimeBalanceRules.Evaluate(new List<SlimeBalanceEntry>()),
            Is.EqualTo(SlimeBalanceState.Balanced));
    }

    [Test]
    public void Evaluate_NullEntries_IsBalanced()
    {
        Assert.That(SlimeBalanceRules.Evaluate(null), Is.EqualTo(SlimeBalanceState.Balanced));
    }

    [Test]
    public void Evaluate_AllNetChangesZero_IsBalanced()
    {
        List<SlimeBalanceEntry> entries = new()
        {
            new SlimeBalanceEntry(0, 0),
            new SlimeBalanceEntry(120, 0),
        };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Balanced));
    }

    [Test]
    public void Evaluate_OnlyGrowing_IsSurplus()
    {
        List<SlimeBalanceEntry> entries = new()
        {
            new SlimeBalanceEntry(0, 0),
            new SlimeBalanceEntry(10, 5),
        };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Surplus));
    }

    // 줄어들긴 하지만 보유량이 남으므로 다음 정산에서 굶는 새끼용은 없다.
    [Test]
    public void Evaluate_ShrinkingWithStockLeft_IsDeficit()
    {
        List<SlimeBalanceEntry> entries = new()
        {
            new SlimeBalanceEntry(100, -20),
            new SlimeBalanceEntry(10, 5),
        };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Deficit));
    }

    // 정확히 0이 되는 종은 먹이를 전액 지불할 수 있으므로 Starving이 아니다(부족분 0).
    [Test]
    public void Evaluate_ShrinkingExactlyToZero_IsDeficit()
    {
        List<SlimeBalanceEntry> entries = new() { new SlimeBalanceEntry(15, -15) };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Deficit));
    }

    [Test]
    public void Evaluate_ShortageOnAnyType_IsStarving()
    {
        List<SlimeBalanceEntry> entries = new() { new SlimeBalanceEntry(10, -15) };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Starving));
    }

    // 다른 종이 아무리 넉넉해도 한 종이 모자라면 그 속성의 새끼용은 그날 정지한다.
    [Test]
    public void Evaluate_SurplusElsewhereDoesNotMaskShortage()
    {
        List<SlimeBalanceEntry> entries = new()
        {
            new SlimeBalanceEntry(999, 500),
            new SlimeBalanceEntry(1, -3),
        };

        Assert.That(SlimeBalanceRules.Evaluate(entries), Is.EqualTo(SlimeBalanceState.Starving));
    }
}
