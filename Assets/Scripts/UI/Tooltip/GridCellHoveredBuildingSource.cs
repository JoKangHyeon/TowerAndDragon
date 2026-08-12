using UnityEngine;

/// <summary>
/// 커서가 지금 얹혀 있는 그리드 셀의 점유 건물을 집는다. 클릭과는 무관하다 -
/// MouseSelectController.GetHoveredCell()은 마우스 좌표를 셀로 바꾸는 조회일 뿐이고,
/// 툴팁을 띄울지는 드라이버가 커서가 머문 시간으로 정한다.
///
/// 클릭 선택(BuildingPlacementController)도 같은 조회를 거치므로 커서가 가리키는 건물과
/// 눌렀을 때 잡히는 건물이 서로 어긋나지 않는다. GridMap.GetBuildingAt은 풋프린트에 속한 모든
/// 셀에서 같은 점유 건물을 돌려주므로 여러 칸짜리 건물도 어느 칸을 짚든 동작한다.
/// </summary>
public class GridCellHoveredBuildingSource : HoveredBuildingSource
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private MouseSelectController _mouseSelectController;

    public override bool TryGetHoveredBuilding(out Building building)
    {
        building = null;

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this) ||
            !WiringGuard.Require(_mouseSelectController, nameof(_mouseSelectController), this))
        {
            return false;
        }

        // 매 프레임 도는 경로라 예외를 던지지 않는 판을 쓴다 - 카메라나 마우스가 없는 순간에는
        // 조용히 "가리키는 것 없음"으로 처리한다.
        if (!_mouseSelectController.TryGetHoveredCell(out Vector3Int cell))
        {
            return false;
        }

        building = _gridMap.GetBuildingAt(cell);

        return building != null;
    }
}
