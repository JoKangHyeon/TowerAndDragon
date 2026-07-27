using System.Collections.Generic;

// 게이트(ProgressionGateSO)가 완료 집합을 읽는 최소 창구.
// ProgressionManagerBase가 구현하며, 게이트는 매니저 구현이 아니라 이 인터페이스만 바라본다.
public interface IProgressionState
{
    bool IsUnlocked(string nodeId);
    IReadOnlyCollection<string> UnlockedIds { get; }
    bool TryGetNode(string nodeId, out ProgressionNodeData node);
}
