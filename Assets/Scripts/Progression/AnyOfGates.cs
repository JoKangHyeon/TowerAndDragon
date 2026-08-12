using UnityEngine;

// 하위 게이트 중 하나만 만족하면 통과. ProgressionRules의 선행·게이트가 전부 AND이므로,
// "A 경로 또는 B 경로"를 표현하려면 이 게이트로 감싸는 수밖에 없다
// (코어인 ProgressionRules는 연구 트리와 공유하므로 건드리지 않는다).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Progression/Gates/Any Of",
    fileName = "AnyOfGate")]
public sealed class AnyOfGatesSO : ProgressionGateSO
{
    [Tooltip("이 중 하나라도 만족하면 통과한다. 비어 있으면 항상 잠긴다.")]
    [SerializeField] private ProgressionGateSO[] _options;

    public override bool IsSatisfied(ProgressionContext context)
    {
        if (_options == null)
        {
            return false;
        }

        foreach (ProgressionGateSO option in _options)
        {
            if (option != null && option.IsSatisfied(context))
            {
                return true;
            }
        }

        return false;
    }
}