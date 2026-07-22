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
        public EnemyEnhancementProfileSO EnemyEnhancementProfile;

        [Tooltip("점령 완료 시 실제로 지급되는 인구 보상.")]
        public int PopulationReward;

        [Tooltip("점령 시 해금되는 자원 종류(수량 아님, 인구 제외) - 보상 패널에 아이콘만 표시.")]
        public ResourceType UnlockedResources;
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

    public EnemyEnhancementProfileSO ResolveEnemyEnhancementProfile(Vector2Int chunkCoord)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.ChunkCoord == chunkCoord)
                return entry.EnemyEnhancementProfile;
        }

        return null;
    }

    public int ResolvePopulationReward(Vector2Int chunkCoord)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.ChunkCoord == chunkCoord)
                return entry.PopulationReward;
        }

        return 0;
    }

    public ResourceType ResolveUnlockedResources(Vector2Int chunkCoord)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.ChunkCoord == chunkCoord)
                return entry.UnlockedResources;
        }

        return ResourceType.None;
    }
}
