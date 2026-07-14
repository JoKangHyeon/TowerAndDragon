using UnityEngine;
using Cysharp.Threading.Tasks;

public class WaveTestStarter : MonoBehaviour
{
    [SerializeField] private WaveManager _waveManager;

    [SerializeField] private WaveDefinitionSO _waveDefinition;

    private void Start()
    {
        _waveManager.StartWaveAsync(_waveDefinition).Forget();
    }    
}
