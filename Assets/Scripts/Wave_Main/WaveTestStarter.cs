using UnityEngine;
using Cysharp.Threading.Tasks;


/// <summary>
/// 싸이클 매니저에서 OnNightStart 받아와서 웨이브 시작
/// </summary>
public class WaveTestStarter : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private WaveDefinitionSO _waveDefinition;

    
    private void OnEnable()
    {
        _cycleManager.OnNightStart.AddListener(StartWave);
    }    

    private void OnDisable()
    {
        _cycleManager.OnNightStart.RemoveListener(StartWave);
    }

    private void StartWave(int _)
    {
        _waveManager.StartWaveAsync(_waveDefinition).Forget();
    }
}
