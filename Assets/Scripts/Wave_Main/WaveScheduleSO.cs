using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(menuName = "TowerAndDragon/Wave/Wave Schedule", fileName = "WaveSchedule")]
public class WaveScheduleSO : ScriptableObject
{

    private const int FIRST_DAY = 1;
    [SerializeField] private List<WaveScheduleEntry> _entries;
    public IReadOnlyList<WaveScheduleEntry> Entries => _entries;


    public bool TryGetWaveDefinition (
        int day, out WaveDefinitionSO waveDefinition
    )
    {
        if (_entries != null)
        {
            foreach (WaveScheduleEntry entry in _entries)
            {
                if (entry != null && entry.Day == day)
                {
                    waveDefinition = entry.WaveDefinition;
                    return waveDefinition != null;
                }
            }
        }

        waveDefinition = null;
        return false;
    }

    private void OnValidate()
    {
        if (_entries == null)
        {
            Debug.LogError(
                "[WaveScheduleSO] 웨이브 일정 목록이 없습니다.",
                this);

            return;
        }

        HashSet<int> registeredDays = new HashSet<int>();

        foreach (WaveScheduleEntry entry in _entries)
        {
            if (entry == null)
            {
                Debug.LogError(
                    "[WaveScheduleSO] 비어 있는 일정 항목이 있습니다.",
                    this);

                continue;
            }

            if (entry.Day < FIRST_DAY)
            {
                Debug.LogError(
                    $"[WaveScheduleSO] 일차는 {FIRST_DAY} 이상이어야 합니다.",
                    this);
            }

            if (!registeredDays.Add(entry.Day))
            {
                Debug.LogError(
                    $"[WaveScheduleSO] {entry.Day}일차 일정이 중복되었습니다.",
                    this);
            }

            if (entry.WaveDefinition == null)
            {
                Debug.LogError(
                    $"[WaveScheduleSO] {entry.Day}일차 웨이브가 지정되지 않았습니다.",
                    this);
            }
        }
    }
}
