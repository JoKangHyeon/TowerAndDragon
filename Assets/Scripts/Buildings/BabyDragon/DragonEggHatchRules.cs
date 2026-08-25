using UnityEngine;

/// <summary>알 부화 판정의 단일 진실. 부화 여부를 정하는 곳과 남은 일수를 표시하는 곳이
/// <b>반드시 같은 함수를 타야 한다</b> - 갈라지면 "1일 남음"이라 적힌 알이 부화하지 않는다.
///
/// 중력 적응(heavy_gravity)이 RunCounterChannel.EggHatchDays로 필요 일수를 늘린다.
/// 뮤테이터가 없으면 가산 항등원이 0이라 BabyDragonData.DaysToHatch 그대로가 되어
/// 기존 동작과 완전히 같다.</summary>
public static class DragonEggHatchRules
{
    /// <summary>런 수정치를 반영한 실제 필요 일수. 음수가 되지 않게 0에서 멈춘다
    /// (0이면 지급 즉시 부화 - BabyDragonData의 기존 규약과 같다).</summary>
    public static int ResolveDaysToHatch(BabyDragonData data, RunModifierSnapshot snapshot)
    {
        if (data == null)
        {
            return 0;
        }

        return Mathf.Max(0, data.DaysToHatch + snapshot.GetCounter(RunCounterChannel.EggHatchDays));
    }

    /// <summary>지금 부화해야 하는가. DragonEggInventorySystem의 판정과 UI 표시가 이 함수를 공유한다.</summary>
    public static bool IsReadyToHatch(DragonEgg egg, BabyDragonData data, RunModifierSnapshot snapshot)
    {
        if (egg == null)
        {
            return false;
        }

        return egg.FedDayCount >= ResolveDaysToHatch(data, snapshot);
    }

    /// <summary>부화까지 남은 일수. 초기 지급 알처럼 이미 목표치를 넘긴 경우 0에서 멈춘다.</summary>
    public static int ResolveRemainingDays(DragonEgg egg, BabyDragonData data, RunModifierSnapshot snapshot)
    {
        if (egg == null)
        {
            return 0;
        }

        return Mathf.Max(0, ResolveDaysToHatch(data, snapshot) - egg.FedDayCount);
    }
}
