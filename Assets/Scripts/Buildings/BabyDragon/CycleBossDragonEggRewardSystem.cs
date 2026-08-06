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
            _rewardPool == null)
        {
            return false;
        }

        // 마지막 주기는 클리어 즉시 승리로 끝나 알을 쓸 다음 주기가 없다.
        // CycleCompleted가 AllCyclesCompleted보다 먼저 발행되므로 이 시점의 IsGameEnded는
        // 아직 false다 - 마지막 주기 판정은 반드시 주기 번호로 해야 한다.
        if (!WaveCycleRules.HasNextCycle(cycleNumber))
        {
            return false;
        }

        // 포탈 4개 봉인처럼 주기 도중에 승부가 난 경우를 막는다.
        if (_gameManager.IsGameEnded)
        {
            return false;
        }

        return !_gameManager.CurrentRun.HasClaimedBossDragonEggReward(cycleNumber);
    }
}
