using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEnhancementManager : MonoBehaviour
{
    private readonly Dictionary<
        TerrainType,
        List<EnemyEnhancementProfileSO>> _profilesByTerrain = new();

    public event Action<TerrainType> ProfilesChanged;

    public void ApplyProfile(
        TerrainType terrainType,
        EnemyEnhancementProfileSO profile)
    {
        if (profile == null)
        {
            return;
        }

        if (!_profilesByTerrain.TryGetValue(
            terrainType,
            out List<EnemyEnhancementProfileSO> profiles))
        {
            profiles = new List<EnemyEnhancementProfileSO>();
            _profilesByTerrain.Add(terrainType, profiles);
        }

        profiles.Add(profile);
        Debug.Log(
            $"[EnemyEnhancementManager] Terrain={terrainType}, Profile={profile.name}, " +
            $"AppliedProfileCount={profiles.Count}",
            this);
        ProfilesChanged?.Invoke(terrainType);
    }

    public IReadOnlyList<EnemyEnhancementProfileSO> GetProfiles(
        TerrainType terrainType)
    {
        if (_profilesByTerrain.TryGetValue(
            terrainType,
            out List<EnemyEnhancementProfileSO> profiles))
        {
            return profiles;
        }

        return Array.Empty<EnemyEnhancementProfileSO>();
    }
}
