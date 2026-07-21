using Cysharp.Threading.Tasks;
using UnityEngine;

public class DailyWaveController : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private WaveScheduleSO _waveSchedule;

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
        if (!_waveSchedule.TryGetWaveDefinition(currentDay, out WaveDefinitionSO waveDefinition))
        {
            Debug.LogError($"[DailyWaveController] {currentDay} 일차 웨이브가 존재하지 않습니다.", this);
            return;
        }
        _waveManager.StartWaveAsync(waveDefinition).Forget();
        Debug.Log($"{currentDay}일차 웨이브 시작");
    }

}
