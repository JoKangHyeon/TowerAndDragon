using UnityEngine;
using System;

[Serializable]
public class WaveScheduleEntry
{
    [SerializeField] private int _day;
    [SerializeField] private WaveDefinitionSO _waveDefinition;

    public int Day => _day;
    public WaveDefinitionSO WaveDefinition => _waveDefinition;
}
