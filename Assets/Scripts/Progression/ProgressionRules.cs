using System;
using System.Collections.Generic;

// 진행도 노드 해금 판정의 유일한 소스. MonoBehaviour·씬 의존이 전혀 없어
// EditMode 테스트로 직접 호출할 수 있다. ProgressionManagerBase는 여기에 위임만 한다.
// 단락 순서: 등록 확인 -> 완료 확인 -> 낮 여부 -> 선행 -> 게이트 -> 파생 추가비용 -> 자원.
public static class ProgressionRules
{
    public static ProgressionNodeState Evaluate(
        ProgressionNodeData node,
        IProgressionState state,
        bool isDay,
        Func<IReadOnlyList<ResourceAmount>, bool> canAfford,
        ProgressionContext gateContext,
        Func<ProgressionNodeData, ProgressionNodeState> checkExtraCost)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
        {
            return ProgressionNodeState.Invalid;
        }

        if (!state.TryGetNode(node.NodeId, out ProgressionNodeData registered) ||
            !ReferenceEquals(registered, node))
        {
            return ProgressionNodeState.Invalid;
        }

        if (state.IsUnlocked(node.NodeId))
        {
            return ProgressionNodeState.Completed;
        }

        if (!isDay)
        {
            return ProgressionNodeState.UnavailablePhase;
        }

        foreach (ProgressionNodeData prerequisite in node.Prerequisites)
        {
            if (prerequisite == null || !state.IsUnlocked(prerequisite.NodeId))
            {
                return ProgressionNodeState.PrerequisiteLocked;
            }
        }

        foreach (ProgressionGateSO gate in node.Gates)
        {
            if (gate == null || !gate.IsSatisfied(gateContext))
            {
                return ProgressionNodeState.GateLocked;
            }
        }

        ProgressionNodeState extraCostState = checkExtraCost != null
            ? checkExtraCost(node)
            : ProgressionNodeState.Available;

        if (extraCostState != ProgressionNodeState.Available)
        {
            return extraCostState;
        }

        if (canAfford == null || !canAfford(node.ResourceCost))
        {
            return ProgressionNodeState.InsufficientResources;
        }

        return ProgressionNodeState.Available;
    }

    public static ProgressionFailureReason ToFailureReason(ProgressionNodeState nodeState)
    {
        return nodeState switch
        {
            ProgressionNodeState.Invalid => ProgressionFailureReason.InvalidNode,
            ProgressionNodeState.Completed => ProgressionFailureReason.AlreadyCompleted,
            ProgressionNodeState.UnavailablePhase => ProgressionFailureReason.UnavailablePhase,
            ProgressionNodeState.PrerequisiteLocked => ProgressionFailureReason.PrerequisiteLocked,
            ProgressionNodeState.GateLocked => ProgressionFailureReason.GateLocked,
            ProgressionNodeState.InsufficientExtraCost => ProgressionFailureReason.InsufficientExtraCost,
            ProgressionNodeState.InsufficientResources => ProgressionFailureReason.InsufficientResources,
            _ => ProgressionFailureReason.None,
        };
    }
}
