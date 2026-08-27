using UnityEngine;

/// <summary>
/// 명중 연출을 대상의 어느 시각 기준점에 놓을지 계산하는 공용 헬퍼.
/// <see cref="ProjectileVisual"/>에서 추출했다 - 근거리·보스 공격 이펙트(<see cref="MonsterAttack"/>)도
/// 같은 발밑·몸통 기준을 써야 하기 때문이다. 피해 판정 위치에는 영향을 주지 않는다 -
/// 순수하게 "어디에 그릴지"만 정한다.
/// </summary>
public static class AttackVfxPlacement
{
    /// <summary>
    /// <paramref name="placement"/>에 따라 명중 연출을 놓을 월드 좌표를 정한다.
    /// <see cref="ProjectileImpactPlacement.TargetOrigin"/>이거나 대상이 없으면 hitPosition을 그대로 쓴다.
    /// </summary>
    /// <param name="cachedMovement">
    /// 호출하는 쪽이 이미 <see cref="MonsterMovement"/>를 캐시해 뒀다면 넘긴다(예: MonsterAttack의
    /// Initialize에서 잡아 둔 참조) - <see cref="ResolveGroundY"/>가 매번 GetComponent를 다시 돌지
    /// 않는다. 비워 두면(기본값 null) 예전처럼 targetObject에서 직접 찾는다.
    /// </param>
    public static Vector3 ResolveVisualImpactPosition(
        Vector3 hitPosition,
        GameObject targetObject,
        ProjectileImpactPlacement placement,
        MonsterMovement cachedMovement = null)
    {
        if (placement == ProjectileImpactPlacement.TargetOrigin || targetObject == null)
        {
            return hitPosition;
        }

        SpriteRenderer bodyRenderer = targetObject.GetComponent<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            bodyRenderer = targetObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (bodyRenderer == null)
        {
            return hitPosition;
        }

        Bounds bounds = bodyRenderer.bounds;

        return placement == ProjectileImpactPlacement.Ground
            ? new Vector3(bounds.center.x, ResolveGroundY(targetObject, cachedMovement), hitPosition.z)
            : new Vector3(bounds.center.x, bounds.center.y, hitPosition.z);
    }

    // 발밑은 스프라이트 경계로 구하면 안 된다. `bounds.min.y`는 스프라이트 사각형의 밑변이고,
    // PixelWorld 시트는 발 위치에 커스텀 피벗을 두기 때문에 그 아래로 셀 높이의 18~25%가 남는다.
    // 보스 1(GolemGian_194, 셀 194 / PPU 32 / 피벗 0.18)은 사각형 밑변이 피벗선보다 1.09 아래고,
    // 실측한 시각 오차는 0.82 월드 유닛이었다.
    // 이동 컴포넌트의 지면 좌표가 이 프로젝트의 발밑 기준이며 지형 고저차 보정도 들어 있다.
    public static float ResolveGroundY(GameObject targetObject, MonsterMovement cachedMovement = null)
    {
        MonsterMovement movement = cachedMovement != null
            ? cachedMovement
            : targetObject.GetComponent<MonsterMovement>();

        return movement != null
            ? movement.GroundPlanePosition.y
            : targetObject.transform.position.y;
    }

    // 진행/타격 방향을 Z축 회전으로 바꾼다. Atan2 기준 0°가 +X이므로 프리팹도 +X를 앞으로 보고 만든다.
    public static Quaternion ResolveDirectionRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return Quaternion.identity;
        }

        float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        return Quaternion.Euler(0f, 0f, degrees);
    }
}
