/// <summary>
/// 툴팁의 "상태" 한 줄. Label·Value는 이미 로컬라이즈를 마친 문자열이다 -
/// 표시기(UI_TooltipPresenter)와 빌더가 상태 시스템을 몰라도 그릴 수 있게 한다.
///
/// 출처(전역 강화 / 개체 상태이상)가 서로 다른 줄을 한 목록에 모으되, Scope로 구분해
/// 빌더가 두 구역으로 나눠 그린다 - 섞어 두면 "이 적만 지금 타고 있다"는 판단이 불가능해진다.
/// </summary>
public readonly struct MonsterStatusLine
{
    public MonsterStatusScope Scope { get; }

    /// <summary>줄 왼쪽에 적는 이름 (예: "화상", "체력").</summary>
    public string Label { get; }

    /// <summary>줄 오른쪽에 적는 값 (예: "2.4초", "+30%").</summary>
    public string Value { get; }

    public bool HasContent => !string.IsNullOrEmpty(Label);

    public MonsterStatusLine(MonsterStatusScope scope, string label, string value)
    {
        Scope = scope;
        Label = label;
        Value = value;
    }
}
