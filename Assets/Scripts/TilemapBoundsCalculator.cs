using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵의 월드 AABB 경계를 계산하는 공용 유틸리티.
/// CameraController(메인 카메라 클램프)와 MinimapController(미니맵 범위)가 함께 사용한다.
/// </summary>
public static class TilemapBoundsCalculator
{
    /// 타일맵 전체(또는 excludedTile 제외한 나머지) 타일들의 월드 경계를 계산한다.
    /// 계산 불가(타일맵 없음 / 남는 타일 없음)면 false를 반환한다.
    public static bool TryCalculate(Tilemap tilemap, TileBase excludedTile, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (tilemap == null) return false;

        tilemap.CompressBounds(); // 빈 셀 제외한 타이트한 경계 확보

        return excludedTile == null
            ? TryCalculateFromFullTilemap(tilemap, out min, out max)
            : TryCalculateExcludingTile(tilemap, excludedTile, out min, out max);
    }

    /// 타일맵 전체 렌더 경계를 그대로 사용
    private static bool TryCalculateFromFullTilemap(Tilemap tilemap, out Vector2 min, out Vector2 max)
    {
        Bounds local = tilemap.localBounds;
        Vector3 a = tilemap.transform.TransformPoint(local.min);
        Vector3 b = tilemap.transform.TransformPoint(local.max);

        // 스케일/회전으로 min·max가 뒤집힐 수 있으므로 축별 정렬
        min = Vector2.Min(a, b);
        max = Vector2.Max(a, b);
        return true;
    }

    /// 제외 타일(바다 등)을 뺀 나머지 타일들의 월드 AABB를 경계로 사용
    private static bool TryCalculateExcludingTile(Tilemap tilemap, TileBase excludedTile, out Vector2 min, out Vector2 max)
    {
        bool found = false;
        min = Vector2.zero;
        max = Vector2.zero;

        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(cell);
            if (tile == null || tile == excludedTile) continue;

            Vector2 center = tilemap.GetCellCenterWorld(cell);
            if (!found)
            {
                min   = center;
                max   = center;
                found = true;
            }
            else
            {
                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }
        }

        if (!found) return false;

        Vector2 halfCell = (Vector2)tilemap.layoutGrid.cellSize * 0.5f;
        min -= halfCell;
        max += halfCell;
        return true;
    }
}
