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

    /// <summary>
    /// 이 주기 다음에 이어질 주기가 있는지. 마지막 주기는 클리어 즉시 게임이 끝나므로
    /// "다음 주기에서 쓸" 보상(새끼용 알 등)을 지급하지 않는 판정에 쓴다.
    /// </summary>
    public static bool HasNextCycle(int cycleNumber)
    {
        return IsValidCycleNumber(cycleNumber) &&
               cycleNumber < MAX_CYCLE_COUNT;
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
