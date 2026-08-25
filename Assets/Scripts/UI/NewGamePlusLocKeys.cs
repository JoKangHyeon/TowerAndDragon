// 새 게임 +(뮤테이터) 진입 UI가 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 도메인별 LocKeys 클래스를 두는 기존 관례를 따른다(TitleLocKeys·SaveLocKeys·ResearchLocKeys).
// 창과 슬롯·단계 버튼·프리셋 버튼이 같은 키를 나눠 쓰므로 한곳에 모은다.
// 스트링테이블 CSV 등록은 T6이 한다 - 여기 있는 key가 그 목록이다.
public static class NewGamePlusLocKeys
{
    public const string WINDOW_HEADER = "ngplus_window_header";

    /// <summary>단계 선택기의 "없음" 칸.</summary>
    public const string TIER_NONE = "ngplus_tier_none";

    /// <summary>"{0}단계" - 단계 버튼 라벨. 단계 수가 데이터마다 달라 번호를 서식으로 넣는다.</summary>
    public const string TIER_LABEL = "ngplus_tier_label";

    /// <summary>"{0}점" - 항목 하나의 현재 단계 점수.</summary>
    public const string SCORE_FORMAT = "ngplus_score_format";

    /// <summary>"난이도 점수 {0}" - 이 모드의 진행 지표.</summary>
    public const string TOTAL_SCORE = "ngplus_total_score";

    /// <summary>"최고 기록 {0}" - MetaProgress.BestDifficultyScore.</summary>
    public const string BEST_SCORE = "ngplus_best_score";

    public const string PRESET_HEADER = "ngplus_preset_header";
    public const string START = "ngplus_start";
    public const string CLOSE = "ngplus_close";

    /// <summary>같은 상호배타 그룹을 두 개 켜려 했을 때의 사유.</summary>
    public const string STATUS_EXCLUSIVE_CONFLICT = "ngplus_status_exclusive_conflict";

    /// <summary>프리셋에 미지 id·범위 밖 단계가 있어 적용을 거부했을 때의 사유.</summary>
    public const string STATUS_PRESET_INVALID = "ngplus_status_preset_invalid";

    /// <summary>설정창 시연용 아이콘으로 방금 해금됐음을 알리는 라벨.</summary>
    public const string DEMO_UNLOCK_DONE = "ngplus_demo_unlock_done";
}
