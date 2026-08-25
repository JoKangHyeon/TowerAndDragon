/// <summary>어미용 속성 변경의 주기당 횟수 정책. 굳은 맹세(sworn_element)가 켜졌을 때만 제한이 생긴다.
///
/// 상태(횟수·주기 번호)는 <see cref="Dragon"/>이 들고, 이 클래스는 "지금 몇 번까지 되는가"와
/// "지금이 몇 번째 주기인가"만 계산한다 - ResearchTierRules와 같은 순수 static 규칙 클래스다.
///
/// <b>주기 번호를 이벤트가 아니라 계산으로 얻는 이유</b> - CycleManager에는 주기 전환 이벤트가 없고
/// (CurrentCycleNumber는 누적 일차의 별칭이다), WaveCycleProgression.CycleStarted는 불러오기 때의
/// 따라잡기 호출에서 다시 발화한다. 그 이벤트로 카운터를 리셋하면 저장→불러오기만으로
/// 그 주기의 변경권이 되살아난다. 일차에서 직접 유도하면 그런 세탁 경로가 생기지 않는다.</summary>
public static class DragonTypeChangeRules
{
    /// <summary>제한 없음. 카운터 채널의 항등원이 0이라 "뮤테이터 미선택 = 무제한"이 그대로 성립한다.</summary>
    public const int UNLIMITED = 0;

    /// <summary>누적 일차로부터 현재 주기 번호를 구한다. WaveCycleProgression이 쓰는 것과 같은 계산이며,
    /// 범위 밖이면 첫 주기로 본다(WaveCycleProgression.CurrentCycleNumber와 같은 규약).</summary>
    public static int ResolveCycleNumber(CycleManager cycleManager)
    {
        int day = cycleManager != null ? cycleManager.CurrentDayNumber : 0;

        if (!WaveCycleRules.TryResolveDay(day, out int cycleNumber, out int _))
        {
            return WaveCycleRules.FIRST_CYCLE_NUMBER;
        }

        return cycleNumber;
    }

    /// <summary>이번 런에서 주기당 허용되는 속성 변경 횟수. 0이면 제한 없음.</summary>
    public static int ResolveLimitPerCycle(RunModifierSnapshot snapshot)
    {
        return snapshot.GetCounter(RunCounterChannel.DragonTypeChangeLimitPerCycle);
    }
}
