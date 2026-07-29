using System.Collections.Generic;
using UnityEngine;

// 봉인석 배치 가능 영역 조회 접점. PortalSealManager가 구현하고 GridMap.SealStonePlacementQuery에 자신을 등록한다.
// null이면 배치를 전부 거부한다(fail-closed) - 영역을 모르는 채 아무데나 짓게 둘 수는 없기 때문
// (해금 여부 자체의 null 기본값은 반대로 fail-open이며, 그 판단은 이 인터페이스의 IsUnlocked가 대신한다).
public interface ISealStonePlacementQuery
{
    bool CellIsSealSite(Vector3Int coord);

    // footprint를 단일 진입점으로 판정한다 - "같은 포탈 영역인지"와 "그 포탈에 이미 봉인석이 있는지"를
    // 각각 anchor/coord로 따로 조회하면 두 조회가 서로 다른 좌표를 볼 수 있어(footprint가 여러 셀인데
    // anchor 한 점만 보는 등) 판정이 어긋날 여지가 생긴다. footprint 전체를 한 번에 해석하는
    // 이 메서드 하나로 통일해, 배치 판정과 매니저의 포탈 식별이 같은 경로를 공유하게 한다.
    // 반환값 true + direction: footprint 전체가 같은 포탈 영역 안에 있고 아직 그 포탈에 봉인석이 없음.
    bool TryResolveOpenSite(List<Vector3Int> footprint, out PortalDirection direction);

    // 연구 해금 상태 - 봉인석은 차수(SealStoneData.Order)별로 해금되므로 차수를 받는다.
    // ISealStoneUnlockQuery가 null이면 여기서도 true(해금됨)로 접힌다.
    bool IsUnlocked(int order);
}
