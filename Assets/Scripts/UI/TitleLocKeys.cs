// 타이틀 화면(StartScene) UI 스크립트들이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 전역 Defines 대신 도메인별 LocKeys 클래스를 두는 기존 관례(SaveLocKeys, ResearchLocKeys)를 따른다.
// 버튼 라벨처럼 인스펙터에서 LocalizedText로 고정하는 키도 여기에 모아, 씬에 흩어진 문자열이
// 어떤 키를 가리키는지 코드에서 한 번에 확인할 수 있게 한다.
public static class TitleLocKeys
{
    public const string NEW_GAME = "title_new_game";
    public const string CONTINUE = "title_continue";
    public const string LOAD = "title_load";
    public const string CONFIG = "title_config";
    public const string QUIT = "title_quit";

    public const string TUTORIAL_PROMPT_MESSAGE = "title_tutorial_prompt_message";
    public const string TUTORIAL_PROMPT_PROCEED = "title_tutorial_prompt_proceed";
    public const string TUTORIAL_PROMPT_SKIP = "title_tutorial_prompt_skip";

    public const string LOAD_WINDOW_HEADER = "title_load_window_header";
    public const string LOAD_SLOT_DELETE = "title_load_slot_delete";

    /// <summary>"슬롯 {0}" - 빈 슬롯에도 번호를 보여 주기 위한 서식.</summary>
    public const string LOAD_SLOT_NUMBER = "title_load_slot_number";
}
