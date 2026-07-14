using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Data/ConquestDurationTable", menuName = "Conquest/ConquestDurationTable")]
public class ConquestDurationTable : ScriptableObject
{
    private const int DEFAULT_DAYS_REQUIRED = 1;

    [Serializable]
    private struct Entry
    {
        public TerrainType TerrainType;
        public int DaysRequired;
    }

    [SerializeField]
    private Entry[] _entries;

    public int ResolveDaysRequired(TerrainType terrainType)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.TerrainType == terrainType)
                return entry.DaysRequired;
        }

        return DEFAULT_DAYS_REQUIRED;
    }
}
