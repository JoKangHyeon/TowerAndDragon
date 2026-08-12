using UnityEngine;

// 어미용 심화/궁극 게이트 - 해금된 새끼용 노드(KinTower/KinArea) 랭크 수.
// _scopeToAttribute를 켜면 해당 속성의 새끼용 노드만 센다 - 전체 합산으로 두면
// 원하는 속성 알이 안 나왔을 때 다른 속성 알 운에 따라 궁극이 영영 잠기기 때문이다
// (알 획득은 주기 보스 보상이라 순전히 랜덤 · 런당 최대 4마리).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Gates/Kin Count",
    fileName = "KinCountGate")]
public sealed class KinCountGateSO : ProgressionGateSO
{
    [Min(0)]
    [SerializeField] private int _requiredKinCount;

    [Tooltip("켜면 _attribute 속성의 새끼용 노드만 센다. 끄면 전 속성 합산(구 동작).")]
    [SerializeField] private bool _scopeToAttribute = true;

    [SerializeField] private DragonType _attribute;

    public override bool IsSatisfied(ProgressionContext context)
    {
        return CountUnlockedKinNodes(context.State) >= _requiredKinCount;
    }

    private int CountUnlockedKinNodes(IProgressionState state)
    {
        int count = 0;

        foreach (string nodeId in state.UnlockedIds)
        {
            if (!state.TryGetNode(nodeId, out ProgressionNodeData node) ||
                !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            if (dragonNode.Kind != DragonNodeKind.KinTower && dragonNode.Kind != DragonNodeKind.KinArea)
            {
                continue;
            }

            if (_scopeToAttribute && dragonNode.Attribute != _attribute)
            {
                continue;
            }

            count++;
        }

        return count;
    }
}