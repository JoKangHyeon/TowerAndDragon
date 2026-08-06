// 용 스킬트리 UI 스크립트 2개 이상이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 노드별 이름/설명 키는 DragonSkillNodeData가 직접 들고 있으므로 여기 포함하지 않는다.
public static class DragonLocKeys
{
    public const string UPGRADE_BUTTON = "dragon_upgrade_button";
    public const string UNKNOWN_RESOURCE = "dragon_unknown_resource";
    public const string RESOURCE_COST = "dragon_resource_cost";

    public const string STATE_INVALID = "dragon_state_invalid";
    public const string STATE_COMPLETED = "dragon_state_completed";
    public const string STATE_UNAVAILABLE_PHASE = "dragon_state_unavailable_phase";
    public const string STATE_PREREQUISITE_LOCKED = "dragon_state_prerequisite_locked";
    public const string STATE_GATE_LOCKED = "dragon_state_gate_locked";
    public const string STATE_INSUFFICIENT_RESOURCES = "dragon_state_insufficient_resources";
    public const string STATE_AVAILABLE = "dragon_state_available";

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
    // Life만 키 접미사가 어미용(life)과 다른 grass다 - 스트링테이블이 그렇게 등록돼 있다("초원 속성").
    public static string BabyInfoLocKey(DragonType attribute)
    {
        return attribute switch
        {
            DragonType.Ice => "dragon_babyDragon_info_ice",
            DragonType.Fire => "dragon_babyDragon_info_fire",
            DragonType.Time => "dragon_babyDragon_info_time",
            DragonType.Stone => "dragon_babyDragon_info_stone",
            DragonType.Life => "dragon_babyDragon_info_grass",
            _ => STATE_INVALID,
        };
    }
}
