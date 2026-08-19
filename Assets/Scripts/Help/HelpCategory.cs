/// <summary>
/// 도감 목록에서 항목을 묶는 갈래. 목록 순서와 헤더 문구가 이 값으로 정해진다.
///
/// 정수로 직렬화되므로 <b>새 값은 반드시 끝에 추가한다</b> - 중간에 끼우면 이미 만든 항목 에셋이
/// 다른 갈래로 밀려난다(TutorialBuildingKind·UI_WarningWindow.MessageId와 같은 규칙).
/// 선언 순서가 곧 목록에 보이는 순서다.
/// </summary>
public enum HelpCategory
{
    // 조작·낮밤처럼 게임 전체에 걸리는 것.
    Basics = 0,

    Building,
    Resource,
    Population,
    Dragon,
    Combat,
    Conquest,
    Research,
}
