using System;
using System.Collections.Generic;
using UnityEngine;

// 지형별 지역 페널티 테이블. 밸런스 수치의 단일 출처이며, 코드에는 어떤 지형이 어떤 값을 갖는지
// 박아두지 않는다(기획서 §변경이력의 외곽 바이옴 수치가 아직 [미정]이라 팀이 계속 조정한다).
// ChunkYieldTable과 동일한 "엔트리 배열 + 지연 캐시" 패턴.
[CreateAssetMenu(menuName = "TowerAndDragon/Terrain Penalty Table")]
public class TerrainPenaltyData : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public TerrainType Terrain;
        public TerrainPenaltyEntry Penalty;
    }

    [Tooltip("지형별 페널티. 여기에 없는 지형(초원 등)은 페널티 없음으로 취급한다.")]
    [SerializeField] private Entry[] _entries;

    private Dictionary<TerrainType, TerrainPenaltyEntry> _cache;

    private void EnsureCache()
    {
        if (_cache != null)
            return;

        _cache = new Dictionary<TerrainType, TerrainPenaltyEntry>();

        if (_entries == null)
            return;

        foreach (Entry entry in _entries)
            _cache[entry.Terrain] = entry.Penalty;
    }

#if UNITY_EDITOR
    private void OnValidate() => _cache = null;
#endif

    // 등록되지 않은 지형은 전 항목 0인 기본값을 반환한다 - 초원/도로/물이 여기 해당한다.
    public TerrainPenaltyEntry Resolve(TerrainType terrain)
    {
        EnsureCache();
        return _cache.TryGetValue(terrain, out TerrainPenaltyEntry penalty) ? penalty : default;
    }
}
