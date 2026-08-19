using System.Collections.Generic;
using UnityEngine;

/// <summary>월드 좌표가 스프라이트 몸통 안인지 판정한다. 클릭 선택(BuildingClickCycle)과
/// 호버 반투명(SpriteHoverFade)이 같은 기준을 쓰도록 한곳에 모아 둔 공용 판정이다 -
/// "반투명해진 곳 = 눌리는 곳"이 어긋나면 플레이어가 무엇을 고르게 될지 예측할 수 없다.
///
/// 경계 상자(AABB)로 먼저 거른 뒤, 스프라이트에 실루엣 폴리곤이 있으면 그것으로 정밀 판정한다.
/// 텍스처 알파는 읽을 수 없지만(스프라이트 팩이 압축 아틀라스라 non-readable) 실루엣 폴리곤은
/// 임포트 시점에 에셋에 직렬화되므로 런타임에 그대로 읽힌다. 폴리곤이 없는 스프라이트는
/// 조용히 AABB 결과를 쓰므로, 최악의 경우에도 예전 판정보다 나빠지지 않는다.</summary>
public static class SpriteHitTest
{
    // 스프라이트별 실루엣 폴리곤 캐시. SpriteHoverFade가 매 프레임 호출하므로
    // GetPhysicsShape의 리스트 채우기를 매번 돌릴 수 없다.
    private static readonly Dictionary<Sprite, Vector2[][]> _outlineCache = new();

    // GetPhysicsShape에 넘길 재사용 버퍼.
    private static readonly List<Vector2> _shapeBuffer = new();

    public static bool Contains(SpriteRenderer spriteRenderer, Vector3 worldPoint)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return false;

        Bounds bounds = spriteRenderer.bounds;
        worldPoint.z = bounds.center.z;

        if (!bounds.Contains(worldPoint))
            return false;

        // Sliced/Tiled은 그려지는 모양이 스프라이트 원본과 달라 폴리곤을 로컬 좌표에 대응시킬 수 없다.
        if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            return true;

        Vector2[][] outlines = GetOutlines(spriteRenderer.sprite);

        if (outlines.Length == 0)
            return true;

        // 실루엣 폴리곤은 피벗 기준·PPU로 나눈 단위라 렌더러의 로컬 공간과 같다.
        // InverseTransformPoint가 회전·스케일까지 함께 되돌린다.
        Vector2 localPoint = spriteRenderer.transform.InverseTransformPoint(worldPoint);

        // flipX/flipY는 transform이 아니라 렌더러가 처리하므로 역변환에 반영되지 않는다.
        if (spriteRenderer.flipX)
            localPoint.x = -localPoint.x;

        if (spriteRenderer.flipY)
            localPoint.y = -localPoint.y;

        return IsInsideOutlines(outlines, localPoint);
    }

    /// <summary>스프라이트별 폴리곤 캐시를 비운다. 도메인 리로드가 꺼져 있으면 캐시가
    /// 플레이 모드를 넘어 살아남아, 재임포트로 바뀐 실루엣이 반영되지 않는다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearCache()
    {
        _outlineCache.Clear();
        _shapeBuffer.Clear();
    }

    private static Vector2[][] GetOutlines(Sprite sprite)
    {
        if (_outlineCache.TryGetValue(sprite, out Vector2[][] cached))
            return cached;

        int shapeCount = sprite.GetPhysicsShapeCount();
        var outlines = new Vector2[shapeCount][];

        for (int i = 0; i < shapeCount; i++)
        {
            sprite.GetPhysicsShape(i, _shapeBuffer);
            outlines[i] = _shapeBuffer.ToArray();
        }

        // 폴리곤이 없는 스프라이트도 캐싱한다 - 없다는 사실을 매번 다시 물어볼 이유가 없다.
        _outlineCache[sprite] = outlines;
        return outlines;
    }

    // 점에서 +X 방향으로 반직선을 쏴, 폴리곤 변을 지나는 횟수가 홀수면 내부로 본다.
    // 경로가 여러 개면 전부 누적해 토글하므로 구멍(내부 경로)도 자연히 처리된다.
    private static bool IsInsideOutlines(Vector2[][] outlines, Vector2 point)
    {
        bool isInside = false;

        foreach (Vector2[] outline in outlines)
        {
            for (int current = 0, previous = outline.Length - 1;
                current < outline.Length;
                previous = current++)
            {
                Vector2 currentVertex = outline[current];
                Vector2 previousVertex = outline[previous];

                // 변이 반직선의 높이를 걸치지 않으면 교차할 수 없다.
                if ((currentVertex.y > point.y) == (previousVertex.y > point.y))
                    continue;

                float crossingX = currentVertex.x
                    + (previousVertex.x - currentVertex.x)
                    * (point.y - currentVertex.y)
                    / (previousVertex.y - currentVertex.y);

                if (point.x < crossingX)
                    isInside = !isInside;
            }
        }

        return isInside;
    }
}
