using System.Collections.Generic;

public readonly struct WaveCycleSnapshot
{
    public int TotalDay { get; }
    public int CycleNumber { get; }
    public int WaveNumber { get; }
    public WaveDefinitionSO WaveDefinition { get; }
    public IReadOnlyList<PortalDirection> ActivePortals { get; }
    public bool IsBossWave => WaveNumber == WaveCycleRules.WAVES_PER_CYCLE;
    public bool IsFinalCycle => CycleNumber == WaveCycleRules.MAX_CYCLE_COUNT;

    public WaveCycleSnapshot(
        int totalDay,
        int cycleNumber,
        int waveNumber,
        WaveDefinitionSO waveDefinition,
        IReadOnlyList<PortalDirection> activePortals)
    {
        TotalDay = totalDay;
        CycleNumber = cycleNumber;
        WaveNumber = waveNumber;
        WaveDefinition = waveDefinition;
        ActivePortals = activePortals;
    }
}
