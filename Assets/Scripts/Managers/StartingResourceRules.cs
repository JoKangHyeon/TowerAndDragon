using System;
using UnityEngine;

/// <summary>
/// 런 시작 시 지급하는 자원에 새 게임 +(empty_hands) 배율을 적용하는 순수 규칙이다.
/// PopulationUpkeepRules·TerrainUpkeepRules와 같은 계층 - MonoBehaviour를 모르므로 테스트가 가능하다.
/// </summary>
public static class StartingResourceRules
{
    // 배율 채널의 항등원. 이 값이면 뮤테이터가 없는 것과 같다.
    private const float NEUTRAL_MULTIPLIER = 1f;

    // 배율을 적용한 뒤 보장하는 최소 지급량. 근거는 Scale 주석 참조.
    private const int MIN_GRANTED_AMOUNT = 1;

    /// <summary>배율이 항등원인가. 뮤테이터가 없으면 배율 경로를 아예 타지 않게 하는 판정이다 -
    /// "뮤테이터 0개일 때 이전과 숫자 하나까지 동일"을 데이터가 아니라 구조로 보장한다.</summary>
    public static bool IsNeutral(float multiplier) => multiplier == NEUTRAL_MULTIPLIER;

    /// <summary>
    /// 초기 지급량 한 항목에 배율을 적용한다.
    ///
    /// 반올림은 최근접(RoundToInt)을 쓴다 - 내림은 x0.75 같은 배율에서 체계적으로 한쪽으로
    /// 치우쳐, 표에 적힌 감소폭보다 항상 더 깎인다.
    ///
    /// 반올림 결과가 0이어도 최소 1은 남긴다: 지급 목록에 올라 있다는 것은 "이 자원을 들고
    /// 시작한다"는 저작 의도이고, 소량 지급되는 특화 자원이 0이 되면 그 자원으로 할 수 있는
    /// 행동 자체가 사라져 뮤테이터의 의도(적게 시작한다)를 넘어선 변화가 된다.
    /// </summary>
    public static int Scale(int amount, float multiplier)
    {
        if (amount <= 0 || IsNeutral(multiplier))
        {
            return amount;
        }

        int scaled = Mathf.RoundToInt(amount * Math.Max(0f, multiplier));

        return Math.Max(MIN_GRANTED_AMOUNT, scaled);
    }

    /// <summary>
    /// 시작 식량 하한 보정(조합 안전장치). 배율 적용 후 식량이 하한 미만이면 하한으로 올린다.
    ///
    /// <b>배율이 항등원이면 아무것도 하지 않는다</b> - 이 하한은 뮤테이터 조합이 1일차부터
    /// 회복 불능이 되는 것을 막는 장치이고, 표준 모드의 시작 식량을 바꾸는 장치가 아니다.
    /// "항상 적용하고 하한값을 낮게 둔다" 방식은 하한값이 표준 시작 식량을 넘는 순간
    /// 조용히 표준 밸런스를 바꾼다.
    /// </summary>
    public static int ApplyFoodFloor(int food, int floor, float multiplier)
    {
        if (IsNeutral(multiplier))
        {
            return food;
        }

        return Math.Max(food, Math.Max(0, floor));
    }

    /// <summary>
    /// <see cref="ApplyFoodFloor"/>에 넘길 하한값을 계산한다 - "1일차 유지비 × 며칠치"라는
    /// 공식으로, 최대 인구나 대식가 배율이 바뀌어도 자동으로 따라간다(고정 상수는 그 값들이
    /// 바뀌는 순간 조용히 틀려진다).
    ///
    /// <b>배율 적용 전 시작 식량을 절대 넘지 않는다</b> - 하드 모드가 표준 모드보다 후해지는
    /// 것을 막는 상한이다. 곱셈은 long으로 올려서 계산한다
    /// (<see cref="PopulationUpkeepRules.GetRequiredFood(int, int)"/>와 같은 이유로,
    /// int 곱셈의 오버플로가 음수 하한을 만드는 것을 막는다).
    /// </summary>
    public static int ResolveFoodFloor(int unscaledFoodAmount, int dailyFoodUpkeep, int bufferDays)
    {
        long required = (long)Math.Max(0, dailyFoodUpkeep) * Math.Max(0, bufferDays);
        int cappedRequired = required > int.MaxValue ? int.MaxValue : (int)required;

        return Math.Min(Math.Max(0, unscaledFoodAmount), cappedRequired);
    }
}
