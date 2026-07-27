using System.Collections.Generic;
using UnityEngine;

// 트리 등록·완료 상태의 순수 데이터 보관소 (MonoBehaviour 아님).
// ResearchManager의 Dictionary<string,Node> + HashSet<string> 조합과 동일 패턴이며,
// 씬 없이 직접 생성할 수 있어 EditMode 테스트에서도 그대로 쓸 수 있다.
public sealed class ProgressionTreeState : IProgressionState
{
    private readonly Dictionary<string, ProgressionNodeData> _nodesById = new();
    private readonly HashSet<string> _unlockedIds = new();

    public IReadOnlyCollection<string> UnlockedIds => _unlockedIds;

    public void CacheNodes(ProgressionTreeData tree)
    {
        _nodesById.Clear();

        if (tree == null)
        {
            return;
        }

        foreach (ProgressionNodeData node in tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            if (!_nodesById.TryAdd(node.NodeId, node))
            {
                Debug.LogError($"[ProgressionTreeState] 중복 노드 ID: {node.NodeId}", node);
            }
        }
    }

    // id뿐 아니라 레퍼런스 동일성까지 확인한다 - 같은 id를 가진 다른 트리의 노드가
    // 잘못 전달되는 것을 막는다 (ResearchManager.IsRegisteredNode와 동일 이유).
    public bool IsRegistered(ProgressionNodeData node)
    {
        return node != null &&
            !string.IsNullOrWhiteSpace(node.NodeId) &&
            _nodesById.TryGetValue(node.NodeId, out ProgressionNodeData registered) &&
            registered == node;
    }

    public bool IsUnlocked(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _unlockedIds.Contains(nodeId);
    }

    public bool TryGetNode(string nodeId, out ProgressionNodeData node)
    {
        return _nodesById.TryGetValue(nodeId, out node);
    }

    public void MarkUnlocked(string nodeId)
    {
        _unlockedIds.Add(nodeId);
    }
}
