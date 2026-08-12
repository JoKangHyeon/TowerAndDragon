using UnityEngine;

// 용알 둥지 보상. RunData.DragonEggs를 직접 건드리지 않고 반드시 GrantEgg를 거친다 -
// 직접 추가하면 토스트 알림(BabyDragonEggNotifier)과 인벤토리 갱신 이벤트를 우회하게 된다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Rewards/Dragon Egg",
    fileName = "LandmarkDragonEggReward")]
public sealed class DragonEggRewardSO : LandmarkRewardSO
{
    [Tooltip("켜면 아래 고정 속성 대신 보상 풀에서 무작위로 뽑는다.")]
    [SerializeField] private bool _rollsFromPool;

    [Tooltip("_rollsFromPool이 꺼져 있을 때 지급할 속성.")]
    [SerializeField] private DragonType _dragonType;

    [Tooltip("_rollsFromPool이 켜져 있을 때 사용할 풀. 주기 보스 보상과 같은 에셋을 재사용해도 된다.")]
    [SerializeField] private DragonEggRewardPoolSO _rewardPool;

    public override bool Grant(LandmarkGrantContext context)
    {
        if (context?.EggInventory == null)
        {
            return false;
        }

        if (!TryResolveDragonType(out DragonType dragonType))
        {
            return false;
        }

        return context.EggInventory.GrantEgg(dragonType);
    }

    private bool TryResolveDragonType(out DragonType dragonType)
    {
        if (!_rollsFromPool)
        {
            dragonType = _dragonType;
            return true;
        }

        if (_rewardPool == null)
        {
            Debug.LogError($"[DragonEggRewardSO] {name}: 풀 추첨으로 설정됐지만 보상 풀이 비어 있습니다.");
            dragonType = default;
            return false;
        }

        return _rewardPool.TryRoll(out dragonType);
    }
}
