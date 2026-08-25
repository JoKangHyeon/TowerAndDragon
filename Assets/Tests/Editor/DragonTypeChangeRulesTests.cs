using NUnit.Framework;

// 굳은 맹세(sworn_element)의 주기당 속성 변경 제한. 상태는 Dragon이 들고 정책은
// DragonTypeChangeRules가 계산한다 - 여기서는 두 조합의 결과를 본다.
public class DragonTypeChangeRulesTests
{
    private const int ONCE_PER_CYCLE = 1;
    private const int FIRST_CYCLE = 1;
    private const int SECOND_CYCLE = 2;

    [Test]
    public void TryChangeType_Unlimited_AllowsRepeatedChangesInSameCycle()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        Assert.That(
            dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, DragonTypeChangeRules.UNLIMITED, out _),
            Is.True);
        Assert.That(
            dragon.TryChangeType(DragonType.Stone, FIRST_CYCLE, DragonTypeChangeRules.UNLIMITED, out _),
            Is.True);
        Assert.That(
            dragon.TryChangeType(DragonType.Life, FIRST_CYCLE, DragonTypeChangeRules.UNLIMITED, out _),
            Is.True);
        Assert.That(dragon.CurrentType, Is.EqualTo(DragonType.Life));
    }

    [Test]
    public void TryChangeType_OncePerCycle_BlocksSecondChangeWithReason()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        Assert.That(
            dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock first),
            Is.True);
        Assert.That(first, Is.EqualTo(DragonTypeChangeBlock.None));

        Assert.That(
            dragon.TryChangeType(DragonType.Stone, FIRST_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock second),
            Is.False);
        Assert.That(second, Is.EqualTo(DragonTypeChangeBlock.CycleLimitReached));

        // 막혔으면 속성이 바뀌지 않아야 한다.
        Assert.That(dragon.CurrentType, Is.EqualTo(DragonType.Fire));
    }

    [Test]
    public void TryChangeType_NextCycle_ResetsTheCounter()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, ONCE_PER_CYCLE, out _);
        Assert.That(
            dragon.TryChangeType(DragonType.Stone, FIRST_CYCLE, ONCE_PER_CYCLE, out _),
            Is.False);

        Assert.That(
            dragon.TryChangeType(DragonType.Stone, SECOND_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock block),
            Is.True);
        Assert.That(block, Is.EqualTo(DragonTypeChangeBlock.None));
        Assert.That(dragon.CurrentType, Is.EqualTo(DragonType.Stone));
    }

    // 같은 속성을 다시 고르는 것은 변경권을 쓰지 않는다 - 오늘 동작을 유지해야 한다.
    [Test]
    public void TryChangeType_SameType_DoesNotConsumeTheCycleAllowance()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        Assert.That(
            dragon.TryChangeType(DragonType.Ice, FIRST_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock block),
            Is.False);
        Assert.That(block, Is.EqualTo(DragonTypeChangeBlock.SameType));

        Assert.That(
            dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, ONCE_PER_CYCLE, out _),
            Is.True);
    }

    [Test]
    public void IsCycleLimitReached_ReflectsTheAllowanceBeforeAnyAttempt()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, ONCE_PER_CYCLE), Is.False);

        dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, ONCE_PER_CYCLE, out _);

        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, ONCE_PER_CYCLE), Is.True);

        // 다른 주기의 기록이면 아직 한 번도 안 쓴 것과 같다.
        Assert.That(dragon.IsCycleLimitReached(SECOND_CYCLE, ONCE_PER_CYCLE), Is.False);

        // 제한이 없으면 언제나 false다(뮤테이터 미선택 = 카운터 항등원 0).
        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, DragonTypeChangeRules.UNLIMITED), Is.False);
    }

    [Test]
    public void RestoreTypeChangeCounter_ZeroRecord_ClearsTheCycleRecord()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };
        dragon.TryChangeType(DragonType.Fire, FIRST_CYCLE, ONCE_PER_CYCLE, out _);
        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, ONCE_PER_CYCLE), Is.True);

        // 기록이 없는 세이브(구버전·표준 모드)는 0/0으로 들어온다.
        dragon.RestoreTypeChangeCounter(0, 0);

        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, ONCE_PER_CYCLE), Is.False);
    }

    // 이 테스트가 후속 작업의 본래 목적이다 - 카운터가 세이브에 실리지 않으면
    // 저장 -> 불러오기만으로 그 주기의 변경권이 한 번 되살아난다.
    [Test]
    public void RestoreTypeChangeCounter_UsedUpCycle_StaysBlocked()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Fire };

        // 저장 당시: 1주기에 이미 한 번 썼다.
        dragon.RestoreTypeChangeCounter(FIRST_CYCLE, ONCE_PER_CYCLE);

        Assert.That(dragon.IsCycleLimitReached(FIRST_CYCLE, ONCE_PER_CYCLE), Is.True);
        Assert.That(
            dragon.TryChangeType(DragonType.Ice, FIRST_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock block),
            Is.False);
        Assert.That(block, Is.EqualTo(DragonTypeChangeBlock.CycleLimitReached));
        Assert.That(dragon.CurrentType, Is.EqualTo(DragonType.Fire));

        // 다음 주기에는 다시 쓸 수 있다.
        Assert.That(
            dragon.TryChangeType(DragonType.Ice, SECOND_CYCLE, ONCE_PER_CYCLE, out _),
            Is.True);
    }

    [Test]
    public void RestoreTypeChangeCounter_NegativeValues_AreClampedToZero()
    {
        Dragon dragon = new Dragon { CurrentType = DragonType.Ice };

        dragon.RestoreTypeChangeCounter(-3, -7);

        Assert.That(dragon.TypeChangeCycleNumber, Is.EqualTo(0));
        Assert.That(dragon.TypeChangeCountInCycle, Is.EqualTo(0));
    }

    [Test]
    public void ResolveCycleNumber_NullCycleManager_FallsBackToFirstCycle()
    {
        Assert.That(
            DragonTypeChangeRules.ResolveCycleNumber(null),
            Is.EqualTo(WaveCycleRules.FIRST_CYCLE_NUMBER));
    }

    [Test]
    public void ResolveLimitPerCycle_NeutralSnapshot_MeansUnlimited()
    {
        Assert.That(
            DragonTypeChangeRules.ResolveLimitPerCycle(RunModifierSnapshot.Neutral),
            Is.EqualTo(DragonTypeChangeRules.UNLIMITED));
    }
}
