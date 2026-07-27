// 연구 티어(1~5)와 웨이브 주기(1~4)의 잠금 매핑. Docs/Sangwook/연구트리_로드맵.md §3 기준.
// T1 게임 시작 / T2 1주기 종료 / T3 2주기 종료 / T4 3주기 종료 / T5 4주기 진입.
// "3주기 종료"와 "4주기 진입"은 같은 시점이므로 T4·T5는 모두 4주기에 열린다.
// 매핑은 밸런싱 대상(로드맵 §7)이므로 WaveCycleRules.PORTAL_UNLOCK_ORDER와 같은
// 정적 순서 배열 한 곳에서만 관리한다.
public static class ResearchTierRules
{
    public const int FIRST_TIER = 1;

    // 인덱스 = (티어 - FIRST_TIER), 값 = 해당 티어가 열리는 주기 번호.
    private static readonly int[] TIER_UNLOCK_CYCLES = { 1, 2, 3, 4, 4 };

    public static int MaxTier => TIER_UNLOCK_CYCLES.Length;

    public static bool IsValidTier(int tier)
    {
        return tier >= FIRST_TIER && tier <= MaxTier;
    }

    /// <summary>해당 티어가 열리는 주기 번호. 티어가 범위 밖이면 첫 주기를 돌려준다.</summary>
    public static int GetUnlockCycle(int tier)
    {
        return IsValidTier(tier)
            ? TIER_UNLOCK_CYCLES[tier - FIRST_TIER]
            : WaveCycleRules.FIRST_CYCLE_NUMBER;
    }

    public static bool IsTierUnlocked(int tier, int currentCycleNumber)
    {
        return IsValidTier(tier) && currentCycleNumber >= GetUnlockCycle(tier);
    }
}
