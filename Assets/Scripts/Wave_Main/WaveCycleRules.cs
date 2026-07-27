public static class WaveCycleRules
{
    public const int FIRST_CYCLE_NUMBER = 1;
    public const int WAVES_PER_CYCLE = 7;
    public const int MAX_CYCLE_COUNT = 4;

    private static readonly PortalDirection[] PORTAL_UNLOCK_ORDER =
    {
        PortalDirection.North,
        PortalDirection.South,
        PortalDirection.East,
        PortalDirection.West,
    };

    public static bool TryResolveDay(
        int totalDay,
        out int cycleNumber,
        out int waveNumber)
    {
        if (totalDay < FIRST_CYCLE_NUMBER)
        {
            cycleNumber = 0;
            waveNumber = 0;
            return false;
        }

        cycleNumber = (totalDay - 1) / WAVES_PER_CYCLE + FIRST_CYCLE_NUMBER;

        if (!IsValidCycleNumber(cycleNumber))
        {
            cycleNumber = 0;
            waveNumber = 0;
            return false;
        }

        waveNumber = (totalDay - 1) % WAVES_PER_CYCLE + 1;
        return true;
    }

    public static bool IsValidCycleNumber(int cycleNumber)
    {
        return cycleNumber >= FIRST_CYCLE_NUMBER &&
               cycleNumber <= MAX_CYCLE_COUNT;
    }

    public static bool TryGetPortalToUnlock(
        int cycleNumber,
        out PortalDirection portalDirection)
    {
        if (!IsValidCycleNumber(cycleNumber))
        {
            portalDirection = PortalDirection.None;
            return false;
        }

        portalDirection = PORTAL_UNLOCK_ORDER[cycleNumber - FIRST_CYCLE_NUMBER];
        return true;
    }
}
