using System.Collections.Generic;
using UnityEngine;

// 건물 효과 표식의 아이콘·문구 테이블. TerrainPenaltyData와 동일한 "엔트리 배열 + 지연 캐시"
// 패턴이며, 등록되지 않은 효과는 표식을 그리지 않는다(아트가 아직 없는 종류를 조용히 건너뛴다).
[CreateAssetMenu(menuName = "TowerAndDragon/Building Effect Icon Table")]
public class BuildingEffectIconData : ScriptableObject
{
    [Tooltip("효과 종류별 아이콘과 문구. 여기에 없는 종류는 표식을 그리지 않는다.")]
    [SerializeField] private BuildingEffectIconEntry[] _entries;

    private Dictionary<BuildingEffectKind, BuildingEffectIconEntry> _cache;

    private void EnsureCache()
    {
        if (_cache != null)
            return;

        _cache = new Dictionary<BuildingEffectKind, BuildingEffectIconEntry>();

        if (_entries == null)
            return;

        foreach (BuildingEffectIconEntry entry in _entries)
        {
            if (entry != null)
                _cache[entry.Kind] = entry;
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => _cache = null;
#endif

    public bool TryResolve(BuildingEffectKind kind, out BuildingEffectIconEntry entry)
    {
        EnsureCache();
        return _cache.TryGetValue(kind, out entry);
    }
}
