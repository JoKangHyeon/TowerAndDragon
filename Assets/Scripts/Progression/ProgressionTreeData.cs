using System.Collections.Generic;
using UnityEngine;

// 트리 전체(노드 묶음)의 공용 계약. 파생 클래스가 실제 노드 배열 필드를 보유한다
// (배열 타입이 파생 노드 타입이어야 인스펙터에서 다른 트리의 노드를 못 넣는다).
public abstract class ProgressionTreeData : ScriptableObject
{
    public abstract IReadOnlyList<ProgressionNodeData> Nodes { get; }

    // 인스펙터에서 다른 트리의 노드를 선행으로 잘못 넣으면 조용히 영구 잠금이 되므로
    // 저장 시점에 (1)ID 중복 (2)빈 ID (3)선행이 같은 트리에 등록되어 있는지를 검사한다.
    protected virtual void OnValidate()
    {
        IReadOnlyList<ProgressionNodeData> nodes = Nodes;
        if (nodes == null)
        {
            return;
        }

        var seenIds = new HashSet<string>();
        var nodeSet = new HashSet<ProgressionNodeData>();

        foreach (ProgressionNodeData node in nodes)
        {
            if (node != null)
            {
                nodeSet.Add(node);
            }
        }

        foreach (ProgressionNodeData node in nodes)
        {
            if (node == null)
            {
                Debug.LogError($"[{name}] 트리에 빈(null) 노드 항목이 있습니다.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                Debug.LogError($"[{name}] 노드 ID가 비어 있습니다: {node.name}", node);
            }
            else if (!seenIds.Add(node.NodeId))
            {
                Debug.LogError($"[{name}] 중복된 노드 ID: {node.NodeId}", node);
            }

            foreach (ProgressionNodeData prerequisite in node.Prerequisites)
            {
                if (prerequisite != null && !nodeSet.Contains(prerequisite))
                {
                    Debug.LogError(
                        $"[{name}] 노드 '{node.NodeId}'의 선행 '{prerequisite.name}'이 " +
                        "같은 트리에 등록되어 있지 않습니다 (영구 잠금 위험).",
                        node);
                }
            }
        }
    }
}
