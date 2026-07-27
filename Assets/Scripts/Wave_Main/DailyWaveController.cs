using Cysharp.Threading.Tasks;
using UnityEngine;

public class DailyWaveController : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private WaveCycleProgression _waveCycleProgression;
    private void OnEnable()
    {
        _cycleManager.OnNightStart.AddListener(StartDailyWave);
    }

    private void OnDisable()
    {
        _cycleManager.OnNightStart.RemoveListener(StartDailyWave);
    }

    private void StartDailyWave(int currentDay)
    {
        if (!_waveCycleProgression.HasCurrentSnapshot || 
            _waveCycleProgression.CurrentSnapshot.TotalDay != currentDay)
        {
            Debug.LogError(
                $"[DailyWaveController] {currentDay}이차 진행 정보가 준비되지 않았습니다.", this
            );
            return;
        }

        if (!_waveCycleProgression.TryGetCurrentWaveDefinition(
                out WaveDefinitionSO waveDefinition))
        {
            Debug.LogError(
                $"[DailyWaveController] {currentDay}일차 웨이브가 없습니다.",
                this);
            return;
        }

        _waveManager.StartWaveAsync(waveDefinition).Forget();
        Debug.Log($"{currentDay}일차 웨이브 시작");
    }

}
