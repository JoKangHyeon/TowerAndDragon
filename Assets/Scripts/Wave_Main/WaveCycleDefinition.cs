using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WaveCycleDefinition
{
    [SerializeField] private int _cycleNumber;
    [SerializeField] private PortalDirection _portalToUnlock;
    [SerializeField] private List<WaveDefinitionSO> _waves;

    public int CycleNumber => _cycleNumber;
    public PortalDirection PortalToUnlock => _portalToUnlock;
    public IReadOnlyList<WaveDefinitionSO> Waves => _waves;

    public bool TryGetWaveDefinition(
        int waveNumber,
        out WaveDefinitionSO waveDefinition)
    {
        int waveIndex = waveNumber - 1;

        if (_waves == null || waveIndex < 0 || waveIndex >= _waves.Count)
        {
            waveDefinition = null;
            return false;
        }

        waveDefinition = _waves[waveIndex];
        return waveDefinition != null;
    }
}
