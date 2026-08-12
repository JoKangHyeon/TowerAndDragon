using System;
using System.Collections.Generic;
using UnityEngine;

// 어느 청크에 어떤 랜드마크가 있는지를 정하는 좌표별 엔트리 테이블.
// ConquestChunkCostTable·ChunkYieldTable과 동일한 패턴(엔트리 배열 + Dictionary 캐시).
//
// 점령 비용 테이블에 필드를 얹지 않고 별도 에셋으로 둔 이유:
// 비용 에셋이 본편/_Balancing 두 벌로 운용되고 있어(실제 연결된 건 _Balancing 쪽) 비용 쪽에
// 얹으면 랜드마크 배치를 양쪽에 중복 입력해야 한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Chunk Landmark Table",
    fileName = "Data_ChunkLandmarkTable")]
public sealed class ChunkLandmarkTable : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public Vector2Int ChunkCoord;
        public LandmarkDataSO Landmark;
    }

    [SerializeField] private Entry[] _entries;

    private Dictionary<Vector2Int, LandmarkDataSO> _landmarksByCoord;

    private void OnEnable() => BuildLookup();

    // 인스펙터에서 배치를 바꿀 때마다 재빌드한다 - 플레이 중 배치를 옮겨도 조회가 최신을 반영한다.
    private void OnValidate() => BuildLookup();

    private void BuildLookup()
    {
        _landmarksByCoord = new Dictionary<Vector2Int, LandmarkDataSO>(
            _entries != null ? _entries.Length : 0);

        if (_entries == null)
        {
            return;
        }

        foreach (Entry entry in _entries)
        {
            if (entry.Landmark == null)
            {
                continue;
            }

            // 같은 좌표에 두 개를 적으면 뒤엣것이 조용히 사라진다 - 인스펙터에서 행을 복사하다
            // 좌표를 안 고치면 나는 실수라, 게임을 돌려서는 원인을 알 수 없어 여기서 알린다.
            if (!_landmarksByCoord.TryAdd(entry.ChunkCoord, entry.Landmark))
            {
                Debug.LogError(
                    $"[ChunkLandmarkTable] 청크 {entry.ChunkCoord}에 랜드마크가 둘 이상 지정됐습니다. " +
                    $"'{entry.Landmark.name}'은 무시됩니다.", this);
            }
        }
    }

    public bool TryResolve(Vector2Int chunkCoord, out LandmarkDataSO landmark)
    {
        EnsureLookup();
        return _landmarksByCoord.TryGetValue(chunkCoord, out landmark);
    }

    public IReadOnlyDictionary<Vector2Int, LandmarkDataSO> LandmarksByCoord
    {
        get
        {
            EnsureLookup();
            return _landmarksByCoord;
        }
    }

    // OnEnable은 에디터에서 도메인 리로드 시점에 따라 호출이 보장되지 않는 경우가 있어,
    // 조회 직전에 한 번 더 확인한다(ChunkYieldTable.EnsureCache와 같은 이유).
    private void EnsureLookup()
    {
        if (_landmarksByCoord == null)
        {
            BuildLookup();
        }
    }
}
