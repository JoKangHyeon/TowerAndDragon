using UnityEngine;

/// <summary>아이소메트릭 타일 종횡비(Grid_HeightVariant의 Grid.cellSize = 1.0 x, 0.5 y)에 맞춰
/// 원형 판정/미리보기를 타원으로 납작하게 만드는 데 쓰는 공용 상수·연산. 타일 기준 사거리가
/// 방향과 무관하게 일정하도록, 이 값을 쓰는 모든 판정/미리보기(스킬, 타워 등)가 하나를 공유한다.</summary>
public static class IsometricMath
{
    public const float RADIUS_Y_RATIO = 0.5f;

    // 그리드 한 행이 화면에서 차지하는 월드 Y 간격(Grid.cellSize.y의 절반).
    public const float ROW_WORLD_HEIGHT = 0.25f;

    public static bool IsWithinEllipse(Vector3 point, Vector3 center, float radiusX, float radiusY) =>
        EllipseNormalizedDistanceSqr(point, center, radiusX, radiusY) <= 1f;

    // (x+y)가 커질수록 화면 위쪽(카메라에서 먼 뒤쪽)이므로, 앞쪽 건물이 위에 그려지도록 부호를 뒤집는다.
    public static int ComputeDepthSortOrder(Vector3Int cellCoord) => -(cellCoord.x + cellCoord.y);

    // 이동 유닛용 - 고저차를 뺀 평면 좌표(MonsterMovement.GroundPlanePosition)의 월드 Y를 같은 행 눈금으로 환산한다.
    public static int ComputeDepthSortOrder(float groundPlaneWorldY) =>
        -Mathf.RoundToInt(groundPlaneWorldY / ROW_WORLD_HEIGHT);

    // 타원 경계를 1로 정규화한 거리 제곱 - 값이 작을수록(1 미만일수록) 중심에 가깝다.
    // 여러 후보의 "타일 기준 가까움"을 비교할 때(예: 타워의 최근접 타겟 선정) 원시 월드 거리 대신 이 값을 쓴다 -
    // 그리드가 세로로 눌려있어 원시 거리로 비교하면 세로 방향 타겟이 부당하게 멀게 취급된다.
    public static float EllipseNormalizedDistanceSqr(Vector3 point, Vector3 center, float radiusX, float radiusY)
    {
        float dx = point.x - center.x;
        float dy = point.y - center.y;
        return (dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY);
    }
}
