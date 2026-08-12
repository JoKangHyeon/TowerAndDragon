using System.Text;

/// <summary>
/// 랜드마크 하나를 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·UI_TooltipPresenter)는
/// 도메인을 모르므로 랜드마크 전용 표현은 여기에만 둔다
/// (BuildingEffectTooltipBuilder·ResourceForecastTooltipBuilder와 같은 역할).
///
/// 출력 예)
///   용알 둥지
///   미점령
///   이 지역을 점령하면 용 알을 얻는다.
///   인구 2 / 4
/// </summary>
public static class LandmarkTooltipBuilder
{
    private static readonly StringBuilder BODY_BUILDER = new();

    // 호버 대상이 바뀔 때만 호출한다(매 프레임 문자열을 새로 만들지 않는다).
    public static TooltipContent Build(Landmark landmark)
    {
        if (landmark == null || landmark.Data == null)
        {
            return default;
        }

        LandmarkDataSO data = landmark.Data;

        BODY_BUILDER.Clear();
        BODY_BUILDER.AppendLine(
            StringTable.GetString(LandmarkLocKeys.ResolveStateLocKey(landmark)));

        if (!string.IsNullOrEmpty(data.DescriptionLocKey))
        {
            BODY_BUILDER.AppendLine(StringTable.GetString(data.DescriptionLocKey));
        }

        // 가동할 수 없는(정원 0) 랜드마크에는 인구 줄을 띄우지 않는다 - 넣을 수 없는 칸을
        // 0/0으로 보여주면 배치를 잊은 것처럼 읽힌다.
        if (data.IsOperable && landmark.Population != null)
        {
            BODY_BUILDER.AppendFormat(
                StringTable.GetString(LandmarkLocKeys.POPULATION_LABEL),
                landmark.Population.AssignedPopulation,
                landmark.Population.Capacity);
        }

        return new TooltipContent(
            StringTable.GetString(data.NameLocKey),
            BODY_BUILDER.ToString().TrimEnd());
    }
}
