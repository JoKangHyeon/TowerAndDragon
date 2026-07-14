using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Data/ConquestDurationTable", menuName = "ConquestDurationTable")]
public class ConquestDurationTable : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public int MinDistance;
        public int DaysRequired;
    }

    [SerializeField]
    // MinDistance 오름차순 등록
    private Entry[] _entries;

    public int Resolve(int distance)
    {
        int result = _entries.Length > 0 ? _entries[0].DaysRequired : 1;
        foreach (var entry in _entries)
        {
            if (distance >= entry.MinDistance)
                result = entry.DaysRequired;
        }

        return result;
    }
}
