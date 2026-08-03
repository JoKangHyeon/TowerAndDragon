using System;
using System.Collections.Generic;
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

    private Dictionary<Vector2Int, Entry> _entriesByCoord;

    private void OnEnable() => BuildLookup();

    // 인스펙터에서 _entries를 수정할 때마다 호출되므로, 플레이 중 밸런싱 값을 바꿔도 조회가 최신 상태를 반영한다.
    private void OnValidate() => BuildLookup();

    private void BuildLookup()
    {
        _entriesByCoord = new Dictionary<Vector2Int, Entry>(_entries.Length);
        foreach (Entry entry in _entries)
            _entriesByCoord.TryAdd(entry.ChunkCoord, entry);
    }

    public bool TryResolve(Vector2Int chunkCoord, out ResourceCost cost)
    {
        if (_entriesByCoord.TryGetValue(chunkCoord, out Entry entry))
        {
            cost = entry.Cost;
            return true;
        }

        cost = default;
        return false;
    }

    public EnemyEnhancementProfileSO ResolveEnemyEnhancementProfile(Vector2Int chunkCoord) =>
        _entriesByCoord.TryGetValue(chunkCoord, out Entry entry) ? entry.EnemyEnhancementProfile : null;

    public int ResolvePopulationReward(Vector2Int chunkCoord) =>
        _entriesByCoord.TryGetValue(chunkCoord, out Entry entry) ? entry.PopulationReward : 0;

    public ResourceType ResolveUnlockedResources(Vector2Int chunkCoord) =>
        _entriesByCoord.TryGetValue(chunkCoord, out Entry entry) ? entry.UnlockedResources : ResourceType.None;
}
