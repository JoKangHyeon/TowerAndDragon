/// <summary>
/// 도감 창 UI 문구의 스트링테이블 키. 항목 문구(제목·본문)는 HelpEntrySO가 직접 들고 있으므로
/// 여기 넣지 않는다 - 항목이 늘 때마다 코드를 고치게 된다.
/// 전역 Defines 대신 도메인별 LocKeys를 두는 기존 관례(ResourceLocKeys·SaveLocKeys·TitleLocKeys)를 따른다.
/// </summary>
public static class HelpLocKeys
{
    public const string WINDOW_HEADER = "help_window_header";

    // "해금 {0} / 전체 {1}" - 목록에 없는 항목이 더 있다는 사실을 이 한 줄이 대신 전한다
    // (잠긴 항목을 회색으로 깔면 아직 만나지 않은 시스템의 존재를 미리 알려버린다).
    public const string UNLOCK_COUNT = "help_window_unlock_count";

    // 아직 아무것도 해금하지 않았을 때 목록 자리에 띄운다.
    public const string EMPTY_LIST = "help_window_empty";

    public const string CLOSE_BUTTON = "help_window_close";

    private const string CATEGORY_BASICS = "help_category_basics";
    private const string CATEGORY_BUILDING = "help_category_building";
    private const string CATEGORY_RESOURCE = "help_category_resource";
    private const string CATEGORY_POPULATION = "help_category_population";
    private const string CATEGORY_DRAGON = "help_category_dragon";
    private const string CATEGORY_COMBAT = "help_category_combat";
    private const string CATEGORY_CONQUEST = "help_category_conquest";
    private const string CATEGORY_RESEARCH = "help_category_research";

    /// <summary>
    /// 갈래 이름 키. 문자열을 조합하지 않고 switch로 명시한다 -
    /// 조합하면 오타가 런타임에야 빈 문구로 드러난다(SaveLocKeys.ResolveSaveFailureLocKey와 같은 이유).
    /// </summary>
    public static string CategoryLocKey(HelpCategory category)
    {
        return category switch
        {
            HelpCategory.Basics => CATEGORY_BASICS,
            HelpCategory.Building => CATEGORY_BUILDING,
            HelpCategory.Resource => CATEGORY_RESOURCE,
            HelpCategory.Population => CATEGORY_POPULATION,
            HelpCategory.Dragon => CATEGORY_DRAGON,
            HelpCategory.Combat => CATEGORY_COMBAT,
            HelpCategory.Conquest => CATEGORY_CONQUEST,
            HelpCategory.Research => CATEGORY_RESEARCH,
            _ => CATEGORY_BASICS,
        };
    }
}
