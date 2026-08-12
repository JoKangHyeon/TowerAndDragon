using UnityEngine;

// 점령 즉시 자원을 지급하는 보상.
// 청크의 지속 생산 보너스는 이쪽이 아니라 LandmarkYieldEffectSO(가동 효과)가 담당한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Rewards/Resource Grant",
    fileName = "LandmarkResourceGrantReward")]
public sealed class ResourceGrantRewardSO : LandmarkRewardSO
{
    [SerializeField] private ResourceAmount[] _amounts;

    public override bool Grant(LandmarkGrantContext context)
    {
        if (context?.Resources == null || _amounts == null || _amounts.Length == 0)
        {
            return false;
        }

        context.Resources.Add(_amounts);
        return true;
    }
}
