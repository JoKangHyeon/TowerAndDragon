using System;

/// <summary>
/// 자원 예측 표기가 공유하는 순수 계산 규칙이다. UI(UI_IngameWindow)와 툴팁 빌더가 같은 판정을 쓰도록
/// 한곳에 모은다. MonoBehaviour 의존이 없어 EditMode 테스트로 검증한다
/// (PopulationUpkeepRules·TerrainUpkeepRules와 같은 구조).
/// </summary>
public static class ResourceForecastRules
{
    /// <summary>하루 순증감. 생산에서 소모를 뺀 값이며 음수일 수 있다.</summary>
    public static int NetChange(int production, int consumption)
    {
        long net = (long)production - consumption;
        return Clamp(net);
    }

    /// <summary>다음 정산 직후의 예상 보유량. 음수일 수 있다(부족분을 그대로 드러내기 위함).</summary>
    public static int ProjectedAmount(int currentAmount, int netChange)
    {
        long projected = (long)currentAmount + netChange;
        return Clamp(projected);
    }

    /// <summary>
    /// 이대로 날을 넘기면 자원이 바닥나는지. "줄어드는 중"이라는 조건을 함께 보는 이유는,
    /// 소모원이 없는 특화·슬라임 자원이 보유량 0·순증감 0으로 항상 경고에 걸리는 것을 막기 위함이다.
    /// </summary>
    public static bool WillRunOut(int currentAmount, int netChange) =>
        netChange < 0 && ProjectedAmount(currentAmount, netChange) <= 0;

    /// <summary>
    /// 다음 정산에서 모자랄 양. 식량의 경우 PopulationUpkeepPreview.FoodShortage(=PopulationLost)와
    /// 정의상 같은 값이다: net = 생산 - 요구량 이므로 보유량 + net = (보유량 + 생산) - 요구량.
    /// </summary>
    public static int Shortage(int currentAmount, int netChange)
    {
        int projected = ProjectedAmount(currentAmount, netChange);
        return projected < 0 ? -projected : 0;
    }

    private static int Clamp(long value)
    {
        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        return value < int.MinValue ? int.MinValue : (int)value;
    }
}
