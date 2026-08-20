// 용 스킬트리 UI 스크립트 2개 이상이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 노드별 이름/설명 키는 DragonSkillNodeData가 직접 들고 있으므로 여기 포함하지 않는다.
public static class DragonLocKeys
{
    public const string UPGRADE_BUTTON = "dragon_upgrade_button";

    // 슬롯 뷰의 랭크 뱃지("Lv {0}/{1}") - 랭크가 2 이상인 슬롯에만 표시한다.
    public const string RANK_BADGE = "dragon_rank_badge";
    public const string UNKNOWN_RESOURCE = "dragon_unknown_resource";
    public const string RESOURCE_COST = "dragon_resource_cost";

    public const string STATE_INVALID = "dragon_state_invalid";
    public const string STATE_COMPLETED = "dragon_state_completed";
    public const string STATE_UNAVAILABLE_PHASE = "dragon_state_unavailable_phase";
    public const string STATE_PREREQUISITE_LOCKED = "dragon_state_prerequisite_locked";
    public const string STATE_GATE_LOCKED = "dragon_state_gate_locked";
    public const string STATE_INSUFFICIENT_RESOURCES = "dragon_state_insufficient_resources";
    public const string STATE_AVAILABLE = "dragon_state_available";

    // 안내가 아직 그 단계를 가르치지 않았다. 미리 해금해 버리면 그 단계의 완료 조건이 영영 오지 않아
    // 거기서 진행이 막히므로(실제로 그렇게 갇혔다), 열 수 없다는 것을 이 문구로 알린다.
    // 토스트도 같은 문구를 쓰므로 키 자체는 Defines에 둔다(CLAUDE.md 커밋규칙 §3.2).
    public const string STATE_TUTORIAL_LOCKED = Defines.DRAGON_TUTORIAL_LOCKED_LOC_KEY;

    public static string ResolveStateLocKey(ProgressionNodeState state)
    {
        return state switch
        {
            ProgressionNodeState.Completed => STATE_COMPLETED,
            ProgressionNodeState.UnavailablePhase => STATE_UNAVAILABLE_PHASE,
            ProgressionNodeState.PrerequisiteLocked => STATE_PREREQUISITE_LOCKED,
            ProgressionNodeState.GateLocked => STATE_GATE_LOCKED,
            ProgressionNodeState.InsufficientResources => STATE_INSUFFICIENT_RESOURCES,
            ProgressionNodeState.Available => STATE_AVAILABLE,
            ProgressionNodeState.TutorialLocked => STATE_TUTORIAL_LOCKED,
            _ => STATE_INVALID,
        };
    }

    public static string AttributeLocKey(DragonType attribute)
    {
        return attribute switch
        {
            DragonType.Ice => "dragon_attribute_ice",
            DragonType.Fire => "dragon_attribute_fire",
            DragonType.Time => "dragon_attribute_time",
            DragonType.Stone => "dragon_attribute_stone",
            DragonType.Life => "dragon_attribute_life",
            _ => STATE_INVALID,
        };
    }

    // 용 창 좌측 프레임(Panel_Info)에 띄우는 속성별 설명.
    public static string MotherInfoLocKey(DragonType attribute)
    {
        return attribute switch
        {
            DragonType.Ice => "dragon_motherDragon_info_ice",
            DragonType.Fire => "dragon_motherDragon_info_fire",
            DragonType.Time => "dragon_motherDragon_info_time",
            DragonType.Stone => "dragon_motherDragon_info_stone",
            DragonType.Life => "dragon_motherDragon_info_life",
            _ => STATE_INVALID,
        };
    }

    // 새끼용 탭 우측(Panel_DragonInfo)에 띄우는 속성별 설명.
    public static string BabyInfoLocKey(DragonType attribute)
    {
        return attribute switch
        {
            DragonType.Ice => "dragon_babyDragon_info_ice",
            DragonType.Fire => "dragon_babyDragon_info_fire",
            DragonType.Time => "dragon_babyDragon_info_time",
            DragonType.Stone => "dragon_babyDragon_info_stone",
            DragonType.Life => "dragon_babyDragon_info_life",
            _ => STATE_INVALID,
        };
    }
}
