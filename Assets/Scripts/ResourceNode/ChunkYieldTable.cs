using System;
using System.Collections.Generic;
using UnityEngine;

// 청크 단위 자원별 생산량 배율 테이블 - 바이옴 특화 자원의 배율만 담당한다(거리·대각선 기반 원시 생산량은
// GridMap이 셀 좌표로 직접 계산한다 - GridMap.CalculateDistanceYield 참고).
// ConquestChunkCostTable과 동일한 "좌표별 엔트리 테이블" 패턴.
[CreateAssetMenu(menuName = "TowerAndDragon/Chunk Yield Table")]
public class ChunkYieldTable : ScriptableObject
{
    [Serializable]
    private struct ResourceMultiplierEntry
    {
        public ResourceType ResourceType;
        public float Multiplier;
    }

    [Serializable]
    private struct Entry
    {
        public Vector2Int ChunkCoord;

        [Tooltip("이 청크에서 특정 자원의 생산량에 곱하는 배율. 지정하지 않은 자원 종류는 배율 1(변화 없음)로 취급한다.")]
        public ResourceMultiplierEntry[] ResourceMultipliers;
    }

    [SerializeField] private Entry[] _entries;

    public float ResolveResourceMultiplier(Vector2Int chunkCoord, ResourceType resourceType)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.ChunkCoord != chunkCoord)
                continue;

            if (entry.ResourceMultipliers == null)
                return 1f;

            foreach (ResourceMultiplierEntry multiplierEntry in entry.ResourceMultipliers)
            {
                if (multiplierEntry.ResourceType == resourceType)
                    return multiplierEntry.Multiplier;
            }

            return 1f;
        }

        return 1f;
    }
}
