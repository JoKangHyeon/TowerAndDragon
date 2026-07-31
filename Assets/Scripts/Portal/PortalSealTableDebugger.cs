using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
// --- PortalSealTable 배치 영역 디버깅용 (에디터 전용, 빌드 미포함) ---
// GridMap과 같은 오브젝트에 이 컴포넌트를 붙이면 Scene 뷰에서 테이블에 저작된 셀을 포탈 방향별 색 마름모(아이소메트릭 타일 모양)로 표시한다.
// GridMap.ConvertGridToWorld가 Tilemap 변환 행렬만 참조하고 런타임 셀 딕셔너리(Awake에서 채워짐)에는
// 의존하지 않으므로, Play 모드에 들어가지 않아도(에디트 모드에서도) 바로 확인 가능하다.
[RequireComponent(typeof(GridMap))]
public class PortalSealTableDebugger : MonoBehaviour
{
    [SerializeField]
    private bool _showSealSiteGizmos = true;

    [SerializeField]
    private PortalSealTable _table;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    private const float SEAL_SITE_GIZMO_FILL_ALPHA = 0.45f;
    private const float SEAL_SITE_GIZMO_OUTLINE_ALPHA = 0.9f;

    private readonly Dictionary<Vector3Int, PortalDirection> _lookup = new();

    // 개발용 디버그 기즈모 - 방향별 저작 셀을 아이소메트릭 타일 모양(마름모)의 색 오버레이 + 좌표 라벨로 표시
    // (ChunkDebugger.OnDrawGizmos와 동일한 패턴, 다만 축 정렬 큐브 대신 실제 타일 모양을 그린다).
    private void OnDrawGizmos()
    {
        GridMap gridMap = GetComponent<GridMap>();
        if (!_showSealSiteGizmos || gridMap == null || _table == null)
            return;

        _table.BuildSiteLookup(_lookup);

        float yOffset = MouseSelectController.GetYOffsetOrZero(_mouseSelectController);

        // 그리드 한 칸(+X, +Y)을 옮길 때 월드 좌표가 얼마나 움직이는지 - 마름모의 두 대각선(합·차)을 구하는 재료.
        // ComputeRotationCompensation과 동일한 방식으로, 하드코딩 없이 실제 아이소메트릭 셀 크기에서 유도한다.
        Vector3 unitX = gridMap.ConvertGridToWorld(Vector3Int.right) - gridMap.ConvertGridToWorld(Vector3Int.zero);
        Vector3 unitY = gridMap.ConvertGridToWorld(Vector3Int.up) - gridMap.ConvertGridToWorld(Vector3Int.zero);
        Vector3 halfDiagonalSum = (unitX + unitY) * 0.5f;
        Vector3 halfDiagonalDiff = (unitX - unitY) * 0.5f;

        foreach (KeyValuePair<Vector3Int, PortalDirection> entry in _lookup)
        {
            Vector3 center = gridMap.ConvertGridToWorld(entry.Key);
            center.y += yOffset;

            Vector3 top = center + halfDiagonalSum;
            Vector3 bottom = center - halfDiagonalSum;
            Vector3 right = center + halfDiagonalDiff;
            Vector3 left = center - halfDiagonalDiff;

            Color color = GetDirectionGizmoColor(entry.Value);

            Vector3[] diamond = { top, right, bottom, left };
            Handles.color = new Color(color.r, color.g, color.b, SEAL_SITE_GIZMO_FILL_ALPHA);
            Handles.DrawAAConvexPolygon(diamond);

            Handles.color = new Color(color.r, color.g, color.b, SEAL_SITE_GIZMO_OUTLINE_ALPHA);
            Handles.DrawAAPolyLine(new[] { top, right, bottom, left, top });

            Handles.Label(center, $"{entry.Value}\n{entry.Key}");
        }
    }

    // 색 범례: North=파랑 · East=빨강 · South=초록 · West=노랑 (BuildSiteLookup은 None을 채우지 않으므로 그 경우는 없음)
    private static Color GetDirectionGizmoColor(PortalDirection direction) => direction switch
    {
        PortalDirection.North => Color.blue,
        PortalDirection.East => Color.red,
        PortalDirection.South => Color.green,
        PortalDirection.West => Color.yellow,
        _ => Color.white,
    };
}
#endif
