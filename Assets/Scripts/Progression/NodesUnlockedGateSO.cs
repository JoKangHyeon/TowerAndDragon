using UnityEngine;

// 지정한 노드들의 해금 여부를 게이트로 노출한다. 선행(_prerequisites)과 달리
// AnyOfGatesSO 안에 넣어 "이 경로를 완주했는가"를 OR의 한쪽 항으로 쓸 수 있다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Progression/Gates/Nodes Unlocked",
    fileName = "NodesUnlockedGate")]
public sealed class NodesUnlockedGateSO : ProgressionGateSO
{
    [SerializeField] private ProgressionNodeData[] _nodes;

    [Tooltip("켜면 전부 해금돼야 통과, 끄면 하나만 해금돼도 통과한다.")]
    [SerializeField] private bool _requireAll = true;

    public override bool IsSatisfied(ProgressionContext context)
    {
        if (_nodes == null || _nodes.Length == 0)
        {
            return false;
        }

        foreach (ProgressionNodeData node in _nodes)
        {
            bool isUnlocked = node != null && context.State.IsUnlocked(node.NodeId);

            if (_requireAll && !isUnlocked)
            {
                return false;
            }

            if (!_requireAll && isUnlocked)
            {
                return true;
            }
        }

        return _requireAll;
    }
}