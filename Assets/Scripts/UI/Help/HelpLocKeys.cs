/// <summary>
/// 도감 창 UI 문구의 스트링테이블 키. 항목 문구(제목·본문)는 HelpEntrySO가 직접 들고 있으므로
/// 여기 넣지 않는다 - 항목이 늘 때마다 코드를 고치게 된다.
/// 전역 Defines 대신 도메인별 LocKeys를 두는 기존 관례(ResourceLocKeys·SaveLocKeys·TitleLocKeys)를 따른다.
/// </summary>
public static class HelpLocKeys
{
    public const string WINDOW_HEADER = "help_window_header";

    // "해금 {0} / 전체 {1}" - 목록에 없는 항목이 더 있다는 사실을 이 한 줄이 대신 전한다
    // (잠긴 항목을 회색으로 깔면 아직 만나지 않은 시스템의 존재를 미리 알려버린다). 두 탭이 함께 쓴다.
    public const string UNLOCK_COUNT = "help_window_unlock_count";

    // 아직 아무것도 해금하지 않았을 때 목록 자리에 띄운다.
    public const string EMPTY_LIST = "help_window_empty";

    // 적 정보 탭에서 아직 만난 적이 없을 때 목록 자리에 띄운다 - 도움말과 문구가 달라야 한다
    // (해금 계기가 "플레이하며 하나씩"이 아니라 "낮 예고 카드에서 정찰"이다).
    public const string MONSTER_EMPTY_LIST = "help_window_monster_empty";

    public const string CLOSE_BUTTON = "help_window_close";

    // 탭 라벨. 값 자체는 코드가 아니라 프리팹의 LocalizedText 컴포넌트가 직접 읽는다 -
    // 상수는 CSV 키를 코드 밖(디자이너용 문서·검증)에서도 참조할 수 있도록 남겨 둔다.
    public const string TAB_HELP = "help_tab_help";
    public const string TAB_MONSTER = "help_tab_monster";

    private const string CATEGORY_MONSTER_GROUND = "help_category_monster_ground";
    private const string CATEGORY_MONSTER_AIR = "help_category_monster_air";
    private const string CATEGORY_MONSTER_BOSS = "help_category_monster_boss";

    private const string CATEGORY_BASICS = "help_category_basics";
    private const string CATEGORY_BUILDING = "help_category_building";
    private const string CATEGORY_RESOURCE = "help_category_resource";
    private const string CATEGORY_POPULATION = "help_category_population";
    private const string CATEGORY_DRAGON = "help_category_dragon";
    private const string CATEGORY_COMBAT = "help_category_combat";
    private const string CATEGORY_CONQUEST = "help_category_conquest";
    private const string CATEGORY_RESEARCH = "help_category_research";
    private const string CATEGORY_TERRAIN = "help_category_terrain";

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
            HelpCategory.Terrain => CATEGORY_TERRAIN,
            _ => CATEGORY_BASICS,
        };
    }

    /// <summary>적 정보 탭의 갈래 이름 키. CategoryLocKey와 같은 이유로 switch로 명시한다.</summary>
    public static string MonsterCategoryLocKey(MonsterCodexCategory category)
    {
        return category switch
        {
            MonsterCodexCategory.Ground => CATEGORY_MONSTER_GROUND,
            MonsterCodexCategory.Air => CATEGORY_MONSTER_AIR,
            MonsterCodexCategory.Boss => CATEGORY_MONSTER_BOSS,
            _ => CATEGORY_MONSTER_GROUND,
        };
    }
}
