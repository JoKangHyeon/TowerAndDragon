// 진행도 트리(연구·용 스킬트리 공용) 노드의 판정 결과.
// ResearchNodeState와 동일 철학이되, 티어 대신 다형 게이트(GateLocked)로 일반화했다.
public enum ProgressionNodeState
{
    Invalid,
    Completed,
    UnavailablePhase,
    PrerequisiteLocked,
    GateLocked,
    InsufficientExtraCost,
    InsufficientResources,
    Available,
}
