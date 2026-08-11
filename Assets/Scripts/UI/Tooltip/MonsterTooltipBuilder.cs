using System.Text;

/// <summary>
/// 출현 예고 카드의 적 한 종류를 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·UI_TooltipPresenter)는
/// 도메인을 모르므로, 몬스터 전용 표현은 여기에만 둔다(ResourceForecastTooltipBuilder와 같은 위치).
///
/// 조건부 줄은 "그 몬스터에서만 의미가 있는 것"만 남긴다 - 방어막 없는 적에 "방어막 0"을,
/// 아무것도 안 노리는 적에 "표적 없음"을 적으면 정작 다른 점이 묻힌다.
///
/// 출력 예)
///   비행 정찰병
///   빠르게 날아 성으로 곧장 향합니다. ...
///
///   체력        30
///   이동 속도    4
///   이동        공중
/// </summary>
public static class MonsterTooltipBuilder
{
    // 체력·이동속도는 데이터상 float이지만 대부분 정수다. 소수점 이하는 있을 때만 두 자리까지 보여준다.
    private const string STAT_NUMBER_FORMAT = "0.##";

    // 툴팁 본문은 한 줄에 한 항목씩 쌓는다. 카드가 갱신될 때만 만들므로 StringBuilder로 충분하다.
    private static readonly StringBuilder BODY_BUILDER = new();

    /// <summary>적 한 종류의 툴팁 내용을 만든다. data가 없으면 내용 없는 값을 돌려준다(표시기가 무시한다).</summary>
    public static TooltipContent Build(MonsterData data)
    {
        if (data == null)
        {
            return default;
        }

        BODY_BUILDER.Clear();

        AppendDescription(data.DescriptionLocKey);

        AppendRow(
            StringTable.GetString(MonsterLocKeys.STAT_HEALTH),
            data.MaxHealth.ToString(STAT_NUMBER_FORMAT));

        AppendRow(
            StringTable.GetString(MonsterLocKeys.STAT_MOVE_SPEED),
            data.MoveSpeed.ToString(STAT_NUMBER_FORMAT));

        AppendRow(
            StringTable.GetString(MonsterLocKeys.STAT_MOVEMENT),
            StringTable.GetString(MonsterLocKeys.MovementLocKey(data.MovementType)));

        if (data.HasShield)
        {
            AppendRow(
                StringTable.GetString(MonsterLocKeys.STAT_SHIELD),
                data.ShieldAmount.ToString(STAT_NUMBER_FORMAT));
        }

        AppendElementRow(data);
        AppendTargetRow(data);

        return new TooltipContent(StringTable.GetString(data.NameLocKey), BODY_BUILDER.ToString());
    }

    // 설명문과 스탯 사이는 빈 줄로 띄운다 - 붙여 두면 설명문이 스탯 줄의 하나처럼 읽힌다.
    private static void AppendDescription(string descriptionLocKey)
    {
        if (string.IsNullOrEmpty(descriptionLocKey))
        {
            return;
        }

        // 줄바꿈은 하나만 넣는다 - 첫 스탯 줄을 쓸 때 AppendRow가 한 번 더 넣어 빈 줄이 된다.
        BODY_BUILDER.AppendLine(StringTable.GetString(descriptionLocKey));
    }

    // 속성 규칙이 Normal이면 적어 줄 것이 없다(모든 공격이 그대로 통하는 기본 상태).
    private static void AppendElementRow(MonsterData data)
    {
        string ruleFormatLocKey = MonsterLocKeys.ElementRuleFormatLocKey(data.ElementRule);

        if (string.IsNullOrEmpty(ruleFormatLocKey))
        {
            return;
        }

        // 속성 이름은 용 쪽 표기를 그대로 쓴다 - 같은 "불"을 두 곳에서 따로 적으면 표기가 어긋난다.
        string elementName = StringTable.GetString(DragonLocKeys.AttributeLocKey(data.Element));

        AppendRow(
            StringTable.GetString(MonsterLocKeys.STAT_ELEMENT),
            string.Format(StringTable.GetString(ruleFormatLocKey), elementName));
    }

    // 이동 중 타워나 용을 노리는 적만 표시한다. 아무것도 안 노리는 적은 성으로 직행한다는 뜻이라
    // 설명문 쪽에서 다루는 편이 읽기 쉽다.
    private static void AppendTargetRow(MonsterData data)
    {
        string targetLocKey = MonsterLocKeys.TargetLocKey(data.EnRouteTargetTypes);

        if (string.IsNullOrEmpty(targetLocKey))
        {
            return;
        }

        AppendRow(
            StringTable.GetString(MonsterLocKeys.STAT_TARGET),
            StringTable.GetString(targetLocKey));
    }

    private static void AppendRow(string label, string value)
    {
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
        }

        BODY_BUILDER.AppendFormat(StringTable.GetString(MonsterLocKeys.TOOLTIP_ROW), label, value);
    }
}
