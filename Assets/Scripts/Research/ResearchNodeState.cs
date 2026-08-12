public enum ResearchNodeState
{
    Invalid,
    Completed,
    UnavailablePhase,
    TierLocked,
    PrerequisiteLocked,
    InsufficientResearchPoints,
    InsufficientResources,
    Available,

    // 역설계 연구 - 해당 랜드마크를 아직 점령하지 않았다.
    // 판정 순서는 TierLocked 다음, 선행 노드 검사 앞이다(ResearchManager.GetNodeState).
    LandmarkLocked,
}
