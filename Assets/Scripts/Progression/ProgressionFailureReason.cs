// TryUnlock 실패 사유. ProgressionNodeState와 1:1 대응(Available 제외).
public enum ProgressionFailureReason
{
    None,
    InvalidNode,
    AlreadyCompleted,
    UnavailablePhase,
    PrerequisiteLocked,
    GateLocked,
    InsufficientExtraCost,
    InsufficientResources,
}
