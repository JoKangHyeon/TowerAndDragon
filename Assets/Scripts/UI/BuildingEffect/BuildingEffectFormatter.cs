using UnityEngine;

/// <summary>
/// 건물 효과의 수치를 화면 문자열로 바꾼다. 표식 라벨(UI_BuildingEffectIconSlot)과
/// 툴팁(BuildingEffectTooltipBuilder) 양쪽이 같은 표기를 쓰도록 여기 한 곳에만 둔다.
///
/// 숫자·기호만 쓰므로 언어와 무관하다(WorkerCountLabel과 같은 이유로 스트링테이블 대상이 아니다).
/// </summary>
public static class BuildingEffectFormatter
{
    private const float PERCENT_SCALE = 100f;

    // 감소율은 방향이 드러나야 해서 부호를 붙이고, 유지비는 "얼마를 낸다"는 수량이라 부호 없이
    // 적는다(아이콘의 붉은 틴트가 손해임을 알린다).
    private const string REDUCTION_FORMAT = "-{0}%";
    private const string AMOUNT_FORMAT = "{0}";

    // 여러 지형에 걸친 건물은 인구 1명당 유지비가 소수로 나온다(0.5 등). 의미 없는 뒷자리는 감춘다.
    private const string RATE_FORMAT = "0.##";

    /// <summary>표식 아이콘 옆에 붙일 짧은 라벨. 수치가 없는 종류는 빈 문자열이다.</summary>
    public static string FormatBadgeLabel(in BuildingEffectDescriptor descriptor)
    {
        switch (descriptor.Kind)
        {
            case BuildingEffectKind.YieldDown:
            case BuildingEffectKind.AttackSpeedDown:
                return string.Format(REDUCTION_FORMAT, ToPercent(descriptor.Ratio));

            case BuildingEffectKind.WoodUpkeep:
            case BuildingEffectKind.StoneUpkeep:
                return string.Format(AMOUNT_FORMAT, descriptor.DailyAmount);

            default:
                return string.Empty;
        }
    }

    public static int ToPercent(float ratio) => Mathf.RoundToInt(ratio * PERCENT_SCALE);

    /// <summary>인구 1명당 유지비처럼 소수가 나올 수 있는 비율값의 표기.</summary>
    public static string FormatRate(float rate) => rate.ToString(RATE_FORMAT);
}
