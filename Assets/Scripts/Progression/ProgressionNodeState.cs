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

    // 안내가 아직 가르치지 않아 지금은 열 수 없다. 새 값은 뒤에 붙인다 - 중간에 끼우면
    // 이 값으로 판정을 저장한 곳이 다른 것으로 바뀐다.
    TutorialLocked,
}
