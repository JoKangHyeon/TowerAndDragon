// 연구 창 UI 스크립트 2개 이상이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 노드별 이름/설명 키는 ResearchNodeData가 직접 들고 있으므로 여기 포함하지 않는다.
public static class ResearchLocKeys
{
    public const string WINDOW_HEADER = "research_window_header";
    public const string RESEARCH_BUTTON = "research_button";
    public const string RP_AMOUNT = "research_rp_amount";
    public const string RP_COST = "research_rp_cost";
    public const string UNKNOWN_RESOURCE = "research_unknown_resource";

    public const string TIER_LABEL = "research_tier_label";
    public const string TIER_LOCKED_BADGE = "research_tier_locked_badge";

    public const string BRANCH_TOWER = "research_branch_tower";
    public const string BRANCH_PRODUCTION = "research_branch_production";
    public const string BRANCH_CONVENIENCE = "research_branch_convenience";

    public const string TIER_CAPTION_1 = "research_tier_caption_1";
    public const string TIER_CAPTION_2 = "research_tier_caption_2";
    public const string TIER_CAPTION_3 = "research_tier_caption_3";
    public const string TIER_CAPTION_4 = "research_tier_caption_4";
    public const string TIER_CAPTION_5 = "research_tier_caption_5";

    public const string STATE_INVALID = "research_state_invalid";
    public const string STATE_COMPLETED = "research_state_completed";
    public const string STATE_DAY_ONLY = "research_state_day_only";
    public const string STATE_TIER_LOCKED = "research_state_tier_locked";
    public const string STATE_PREREQUISITE_LOCKED = "research_state_prerequisite_locked";
    public const string STATE_INSUFFICIENT_RP = "research_state_insufficient_rp";
    public const string STATE_INSUFFICIENT_RESOURCES = "research_state_insufficient_resources";
    public const string STATE_AVAILABLE = "research_state_available";

    public static string ResolveStateLocKey(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => STATE_COMPLETED,
            ResearchNodeState.UnavailablePhase => STATE_DAY_ONLY,
            ResearchNodeState.TierLocked => STATE_TIER_LOCKED,
            ResearchNodeState.PrerequisiteLocked => STATE_PREREQUISITE_LOCKED,
            ResearchNodeState.InsufficientResearchPoints => STATE_INSUFFICIENT_RP,
            ResearchNodeState.InsufficientResources => STATE_INSUFFICIENT_RESOURCES,
            ResearchNodeState.Available => STATE_AVAILABLE,
            _ => STATE_INVALID,
        };
    }

    public static string BranchLocKey(ResearchBranch branch)
    {
        return branch switch
        {
            ResearchBranch.Tower => BRANCH_TOWER,
            ResearchBranch.Production => BRANCH_PRODUCTION,
            ResearchBranch.Convenience => BRANCH_CONVENIENCE,
            _ => BRANCH_TOWER,
        };
    }

    // 티어 캡션은 "해금 시점" 안내 문구다(로드맵 §3). 키를 문자열 조합으로 만들지 않고
    // 티어마다 명시적으로 매핑해 오타·미등록 키를 컴파일 시점에 드러낸다.
    public static string TierCaptionLocKey(int tier)
    {
        return tier switch
        {
            1 => TIER_CAPTION_1,
            2 => TIER_CAPTION_2,
            3 => TIER_CAPTION_3,
            4 => TIER_CAPTION_4,
            5 => TIER_CAPTION_5,
            _ => TIER_CAPTION_1,
        };
    }
}
