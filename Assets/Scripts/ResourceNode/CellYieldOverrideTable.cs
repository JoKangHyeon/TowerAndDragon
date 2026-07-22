using System;
using System.Collections.Generic;
using UnityEngine;

// 셀 단위 생산량 오버라이드 테이블 - 청크 기본 생산량(ChunkYieldTable)이 셀에 구워진(GridMap.ApplyCellYields) 다음 단계에서
// 지정한 영역의 셀만 자원별 생산량을 덮어쓴다. ResourceNodeAreaTable과 동일한 앵커+FootprintShape 패턴을 재사용한다.
[CreateAssetMenu(menuName = "TowerAndDragon/Cell Yield Override Table")]
public class CellYieldOverrideTable : ScriptableObject
{
    [Serializable]
    private struct ResourceYieldEntry
    {
        public ResourceType ResourceType;
        public int Yield;
    }

    [Serializable]
    private struct Entry
    {
        public Vector3Int AnchorCoord;
        public FootprintShape Shape;
        public ResourceYieldEntry[] Overrides;
    }

    [SerializeField] private Entry[] _entries;

    // GridMap.ApplyCellYields() 다음 단계에 호출 - 지정한 영역의 셀에 자원별 생산량을 덮어쓴다.
    public void ApplyToGrid(Dictionary<Vector3Int, GridCell> cells)
    {
        foreach (Entry entry in _entries)
        {
            foreach (Vector2Int offset in entry.Shape.GetOccupiedOffsets())
            {
                Vector3Int coord = entry.AnchorCoord + new Vector3Int(offset.x, offset.y, 0);
                if (!cells.TryGetValue(coord, out GridCell cell))
                    continue;

                foreach (ResourceYieldEntry resourceYield in entry.Overrides)
                    cell.SetYield(resourceYield.ResourceType, resourceYield.Yield);
            }
        }
    }
}
