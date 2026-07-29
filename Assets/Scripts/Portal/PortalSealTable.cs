using System;
using System.Collections.Generic;
using UnityEngine;

// 포탈별 봉인석 건설 가능 영역 테이블. 비용은 여기서 다루지 않는다 - 각 봉인석(1~4차)의 비용은
// SealStoneData가 고정값으로 가진다(Factory/Tower와 동일한 패턴). 이 테이블은 순수하게 "어디에
// 지을 수 있는가"만 담당한다.
// ResourceNodeAreaTable(앵커 + FootprintShape 저작 방식)과 같은 패턴을 쓰되,
// 그 테이블과 달리 조회 API를 신설한다 - ResourceNodeAreaTable은 Awake 1회성 bake라
// 런타임에 "이 셀이 어느 영역인가"를 물어볼 수 없기 때문(이 프로젝트에 조회 가능한 선례 없음).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Portal/Portal Seal Table",
    fileName = "PortalSealTable")]
public sealed class PortalSealTable : ScriptableObject
{
    // 인스펙터 저작과 테스트/도구 코드가 함께 쓸 수 있도록 public으로 둔다 - 대신 배열 자체는
    // SetSites로만 갈아끼울 수 있게 해, 필드를 직접 건드리지 않고도(리플렉션 없이) 이 SO를 코드로 구성할 수 있다.
    [Serializable]
    public struct SiteEntry
    {
        public PortalDirection PortalDirection;
        public Vector3Int AnchorCoord;
        public FootprintShape Shape; // 임의 모양 영역 - 기존 FootprintShape 재사용
    }

    [Tooltip("포탈별 봉인석 건설 가능 영역. 포탈 개수(4)만큼 저작 - 이 배열 길이가 승리 임계값(SiteCount)이 된다.")]
    [SerializeField] private SiteEntry[] _sites;

    // 승리 임계값 - 리터럴 4를 쓰지 않기 위해 사이트 저작 개수를 그대로 쓴다.
    public int SiteCount => _sites != null ? _sites.Length : 0;

    // 인스펙터 없이(테스트, 에디터 도구) 이 SO를 코드로 구성할 수 있게 하는 공개 설정 메서드.
    public void SetSites(SiteEntry[] sites) => _sites = sites;

    // 호출자(PortalSealManager)가 Awake에 1회 전개해 캐시한다 - SO 자신은 런타임 상태를 들지 않는다.
    // 좌표 계산식은 GridMap.GetFootprintCoords / ResourceNodeAreaTable.ApplyToGrid와 동일(앵커 + occupied offset).
    public void BuildSiteLookup(Dictionary<Vector3Int, PortalDirection> lookup)
    {
        lookup.Clear();

        if (_sites == null)
            return;

        foreach (SiteEntry entry in _sites)
        {
            if (entry.Shape == null)
                continue;

            foreach (Vector2Int offset in entry.Shape.GetOccupiedOffsets())
            {
                Vector3Int coord = entry.AnchorCoord + new Vector3Int(offset.x, offset.y, 0);
                lookup[coord] = entry.PortalDirection;
            }
        }
    }
}
