using NUnit.Framework;

public class WaveCycleRulesTests
{
    [TestCase(1, 1, 1)]
    [TestCase(7, 1, 7)]
    [TestCase(8, 2, 1)]
    [TestCase(14, 2, 7)]
    [TestCase(15, 3, 1)]
    [TestCase(21, 3, 7)]
    [TestCase(22, 4, 1)]
    [TestCase(28, 4, 7)]
    public void TryResolveDay_ValidDay_ReturnsCycleAndWave(
        int totalDay,
        int expectedCycleNumber,
        int expectedWaveNumber)
    {
        bool isResolved = WaveCycleRules.TryResolveDay(
            totalDay,
            out int cycleNumber,
            out int waveNumber);

        Assert.That(isResolved, Is.True);
        Assert.That(cycleNumber, Is.EqualTo(expectedCycleNumber));
        Assert.That(waveNumber, Is.EqualTo(expectedWaveNumber));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(29)]
    public void TryResolveDay_OutOfRangeDay_ReturnsFalse(int totalDay)
    {
        bool isResolved = WaveCycleRules.TryResolveDay(
            totalDay,
            out int cycleNumber,
            out int waveNumber);

        Assert.That(isResolved, Is.False);
        Assert.That(cycleNumber, Is.Zero);
        Assert.That(waveNumber, Is.Zero);
    }

    [TestCase(1, PortalDirection.North)]
    [TestCase(2, PortalDirection.South)]
    [TestCase(3, PortalDirection.East)]
    [TestCase(4, PortalDirection.West)]
    public void TryGetPortalToUnlock_ValidCycle_ReturnsConfiguredOrder(
        int cycleNumber,
        PortalDirection expectedPortal)
    {
        bool isResolved = WaveCycleRules.TryGetPortalToUnlock(
            cycleNumber,
            out PortalDirection portalDirection);

        Assert.That(isResolved, Is.True);
        Assert.That(portalDirection, Is.EqualTo(expectedPortal));
    }
}
