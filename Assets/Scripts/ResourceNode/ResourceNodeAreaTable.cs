using System;
using System.Collections.Generic;
using UnityEngine;

// 수기 지정 자원 영역 테이블 - 터레인 기본값 위에 특정 구역만 추가로 자원 플래그를 부여할 때 사용한다.
// 기존 건물 배치용 FootprintShape를 재사용해 임의 모양의 영역을 정의한다.
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Node Area Table")]
public class ResourceNodeAreaTable : ScriptableObject
{
    // Add: 터레인 기본값 위에 추가(OR). Override: 터레인 기본값을 무시하고 지정한 값으로 완전히 교체.
    private enum ApplyMode
    {
        Add,
        Override,
    }

    [Serializable]
    private struct Entry
    {
        public Vector3Int AnchorCoord;
        public FootprintShape Shape;
        public ResourceType ResourceFlags;
        public ApplyMode Mode;
    }

    [SerializeField] private Entry[] _entries;

    // 그리드 생성(터레인 기본값 적용) 다음 단계에 GridMap이 호출 - 각 영역의 풋프린트 좌표에 해당하는 셀에 Mode에 따라 적용한다.
    public void ApplyToGrid(Dictionary<Vector3Int, GridCell> cells)
    {
        foreach (Entry entry in _entries)
        {
            foreach (Vector2Int offset in entry.Shape.GetOccupiedOffsets())
            {
                Vector3Int coord = entry.AnchorCoord + new Vector3Int(offset.x, offset.y, 0);
                if (!cells.TryGetValue(coord, out GridCell cell))
                    continue;

                if (entry.Mode == ApplyMode.Override)
                    cell.SetResourceNodes(entry.ResourceFlags);
                else
                    cell.AddResourceNodes(entry.ResourceFlags);
            }
        }
    }
}
