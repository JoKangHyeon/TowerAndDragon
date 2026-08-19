using System.Collections.Generic;

/// <summary>슬라임 5종을 한 칸으로 요약했을 때의 표시 상태.</summary>
public enum SlimeBalanceState
{
    /// <summary>전 종의 하루 순증감이 0. 늘지도 줄지도 않는다.</summary>
    Balanced,

    /// <summary>줄어드는 종이 없고 하나 이상이 늘어난다.</summary>
    Surplus,

    /// <summary>줄어드는 종이 있지만 다음 정산은 아직 버틴다.</summary>
    Deficit,

    /// <summary>다음 정산에서 먹이가 모자라 새끼용이 정지한다.</summary>
    Starving,
}

/// <summary>슬라임 한 종의 현재 보유량과 하루 순증감.</summary>
public readonly struct SlimeBalanceEntry
{
    public int Amount { get; }
    public int NetChange { get; }

    public SlimeBalanceEntry(int amount, int netChange)
    {
        Amount = amount;
        NetChange = netChange;
    }
}

/// <summary>
/// 슬라임 5종의 수급을 한 상태로 압축하는 순수 계산 규칙이다. HUD 요약 칸(UI_SlimeSummaryIndicator)이
/// 쓰며, MonoBehaviour 의존이 없어 EditMode 테스트로 검증한다
/// (ResourceForecastRules·PopulationUpkeepRules와 같은 구조).
/// </summary>
public static class SlimeBalanceRules
{
    /// <summary>
    /// 우선순위대로 판정한다: 굶는 종이 하나라도 있으면 Starving, 그다음 줄어드는 종이 있으면 Deficit,
    /// 전부 0이면 Balanced, 나머지는 Surplus.
    ///
    /// Starving을 가장 앞에 두는 이유는, 한 종이 넉넉해도 다른 종이 모자라면 그 속성의 새끼용은
    /// 그날 정지하기 때문이다(먹이는 전부-또는-전무 - BabyDragonFeedingSystem.Feed).
    /// </summary>
    public static SlimeBalanceState Evaluate(IReadOnlyList<SlimeBalanceEntry> entries)
    {
        if (entries == null)
        {
            return SlimeBalanceState.Balanced;
        }

        bool hasDeficit = false;
        bool hasSurplus = false;

        foreach (SlimeBalanceEntry entry in entries)
        {
            // 부족분이 생기면 그 속성의 새끼용 중 최소 한 마리가 먹이를 못 받는다.
            if (ResourceForecastRules.Shortage(entry.Amount, entry.NetChange) > 0)
            {
                return SlimeBalanceState.Starving;
            }

            if (entry.NetChange < 0)
            {
                hasDeficit = true;
            }
            else if (entry.NetChange > 0)
            {
                hasSurplus = true;
            }
        }

        if (hasDeficit)
        {
            return SlimeBalanceState.Deficit;
        }

        return hasSurplus ? SlimeBalanceState.Surplus : SlimeBalanceState.Balanced;
    }
}
