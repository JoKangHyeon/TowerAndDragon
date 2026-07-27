using UnityEngine;

// 어미용 강화/궁극 게이트 - 새끼용(KinTower/KinArea) 해금 수가 전역 합산으로 N개 이상.
// 새끼용(#102·#103)이 이번 범위에 없으므로 _requiredKinCount는 SO 필드로 두고
// 데이터는 임시 0을 넣는다 - 로드맵 §5·§10, 일정계획 A-1.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Gates/Kin Count",
    fileName = "KinCountGate")]
public sealed class KinCountGateSO : ProgressionGateSO
{
    [Min(0)]
    [SerializeField] private int _requiredKinCount;

    public override bool IsSatisfied(ProgressionContext context)
    {
        return CountUnlockedKinNodes(context.State) >= _requiredKinCount;
    }

    private static int CountUnlockedKinNodes(IProgressionState state)
    {
        int count = 0;

        foreach (string nodeId in state.UnlockedIds)
        {
            if (!state.TryGetNode(nodeId, out ProgressionNodeData node) ||
                !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            if (dragonNode.Kind == DragonNodeKind.KinTower || dragonNode.Kind == DragonNodeKind.KinArea)
            {
                count++;
            }
        }

        return count;
    }
}
