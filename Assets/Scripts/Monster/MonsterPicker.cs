using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 커서 아래의 적을 고르는 공용 판정. 스킬 타겟팅과 호버 툴팁이 같은 규칙을 쓰게 하려고 둔다 -
/// 판정이 갈리면 "스킬은 걸리는데 툴팁은 안 뜨는" 자리가 생겨 어느 쪽이 고장인지 알 수 없게 된다.
///
/// 적 콜라이더가 작아 커서로 정확히 맞히기 어려우므로 반경을 두고 주운 뒤 가장 가까운 적을 고른다.
/// </summary>
public static class MonsterPicker
{
    // 커서에서 이 반경 안의 적을 후보로 줍는다.
    public const float PICK_RADIUS = 0.3f;

    // 스프라이트가 놓인 월드 Z 평면.
    private const float TARGET_PLANE_WORLD_Z = 0f;

    /// <summary>
    /// worldPoint 주변에서 가장 가까운, 살아 있는 적을 돌려준다. 없으면 null.
    /// </summary>
    public static BaseMonster FindUnderPointer(Vector3 worldPoint, LayerMask layers)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, PICK_RADIUS, layers);
        BaseMonster closest = null;
        float closestSqrDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            BaseMonster monster = hit.GetComponentInParent<BaseMonster>();

            if (monster == null || monster.IsDead)
            {
                continue;
            }

            float sqrDistance = (monster.TargetTransform.position - worldPoint).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = monster;
            }
        }

        return closest;
    }

    /// <summary>
    /// 커서가 가리키는 스프라이트 평면 위의 월드 좌표.
    ///
    /// 오쏘그래픽 카메라의 ScreenToWorldPoint z는 "카메라로부터의 거리"이지 월드 Z가 아니다.
    /// 카메라 Z(예: -10)를 그대로 0으로 두면 카메라 자기 위치(니어클립 안쪽)가 나와
    /// 물리 판정(2D라 Z 무관)엔 문제없지만 시각 요소(LineRenderer 등)는 화면에 보이지 않는다.
    /// </summary>
    public static Vector3 GetMouseWorldPoint(Camera camera)
    {
        if (camera == null || Mouse.current == null)
        {
            return Vector3.zero;
        }

        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = TARGET_PLANE_WORLD_Z - camera.transform.position.z;

        return camera.ScreenToWorldPoint(screenPos);
    }
}
