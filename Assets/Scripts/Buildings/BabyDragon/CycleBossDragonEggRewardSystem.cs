using UnityEngine;

public sealed class CycleBossDragonEggRewardSystem : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private WaveCycleProgression _cycleProgression;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private DragonEggRewardPoolSO _rewardPool;

    private void OnEnable()
    {
        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.AddListener(HandleCycleCompleted);
        }
    }

    private void OnDisable()
    {
        if (_cycleProgression != null)
        {
            _cycleProgression.CycleCompleted.RemoveListener(HandleCycleCompleted);
        }
    }

    private void HandleCycleCompleted(int cycleNumber)
    {
        if (!CanGrantReward(cycleNumber) ||
            !_rewardPool.TryRoll(out DragonType dragonType) ||
            !_eggInventorySystem.GrantEgg(dragonType))
        {
            return;
        }

        _gameManager.CurrentRun.TryRecordBossDragonEggReward(cycleNumber, dragonType);
        Debug.Log(
            $"[CycleBossDragonEggRewardSystem] {cycleNumber}주기 보상으로 {dragonType} 알을 획득했습니다.",
            this);
    }

    private bool CanGrantReward(int cycleNumber)
    {
        if (_gameManager == null ||
            _gameManager.CurrentRun == null ||
            _eggInventorySystem == null ||
            _rewardPool == null ||
            !WaveCycleRules.IsValidCycleNumber(cycleNumber))
        {
            return false;
        }

        return !_gameManager.CurrentRun.HasClaimedBossDragonEggReward(cycleNumber);
    }
}
