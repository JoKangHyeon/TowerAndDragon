using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Data/ConquestChunkCostTable", menuName = "Conquest/ConquestChunkCostTable")]
public class ConquestChunkCostTable : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public Vector2Int ChunkCoord;
        public ResourceCost Cost;
    }

    [SerializeField] private Entry[] _entries;

    public bool TryResolve(Vector2Int chunkCoord, out ResourceCost cost)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.ChunkCoord == chunkCoord)
            {
                cost = entry.Cost;
                return true;
            }
        }

        cost = default;
        return false;
    }
}
