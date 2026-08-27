using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 커서 아래의 적을 고르는 공용 판정. 스킬 타겟팅과 호버 툴팁·호버 아웃라인이 같은 규칙을 쓰게
/// 하려고 둔다 - 판정이 갈리면 "스킬은 걸리는데 툴팁은 안 뜨는" 자리가 생겨 어느 쪽이 고장인지
/// 알 수 없게 된다.
///
/// 1차로 스프라이트 몸통(SpriteHitTest 기준, BaseMonster.ContainsWorldPoint)을 우선 판정한다 -
/// 적 콜라이더는 몸통에 비해 훨씬 작고(발밑에 반경 0.1, 스케일 적용 후 0.05) 바텀 피벗이라,
/// 콜라이더만 쓰면 큰 스프라이트(보스 등)는 발밑 근처가 아니면 사실상 커서로 집을 수 없다.
/// 몸통 판정이 실패하면(실루엣 폴리곤이 없는 스프라이트 등) 기존 콜라이더 반경 판정으로 폴백한다 -
/// 지금 집히던 것은 전부 그대로 집히는 상위집합이라 스킬 타겟팅이 이 변경으로 퇴행하지 않는다.
/// </summary>
public static class MonsterPicker
{
    // 커서에서 이 반경 안의 적을 후보로 줍는다(콜라이더 폴백 경로).
    public const float PICK_RADIUS = 0.3f;

    // 스프라이트가 놓인 월드 Z 평면.
    private const float TARGET_PLANE_WORLD_Z = 0f;

    // OverlapCircleAll은 호출마다 Collider2D[]를 새로 만든다. 툴팁·스킬 타겟팅·호버 아웃라인이
    // 전부 매 프레임 부르는 경로라 그 배열이 그대로 GC 압력이 된다. 결과 버퍼를 재사용한다.
    private static readonly List<Collider2D> COLLIDER_BUFFER = new();

    // 도메인 리로드가 꺼져 있으면 이전 세션에서 파괴된 콜라이더가 버퍼에 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => COLLIDER_BUFFER.Clear();

    /// <summary>
    /// worldPoint를 가리키는, 살아 있는 적을 돌려준다. 없으면 null.
    /// </summary>
    public static BaseMonster FindUnderPointer(Vector3 worldPoint, LayerMask layers)
    {
        BaseMonster bodyHit = FindByBodyUnderPointer(worldPoint);

        return bodyHit != null ? bodyHit : FindByColliderUnderPointer(worldPoint, layers);
    }

    // 살아 있는 적 중 스프라이트 몸통이 겹치는 것을, 화면 앞쪽(DepthSortOrder가 큰 쪽) 우선으로 고른다.
    private static BaseMonster FindByBodyUnderPointer(Vector3 worldPoint)
    {
        IReadOnlyList<BaseMonster> activeMonsters = BaseMonster.ActiveMonsters;
        BaseMonster best = null;

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            BaseMonster candidate = activeMonsters[i];

            if (candidate.IsDead || candidate.IsFogHidden || !candidate.ContainsWorldPoint(worldPoint))
            {
                continue;
            }

            if (best == null || candidate.DepthSortOrder > best.DepthSortOrder)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static BaseMonster FindByColliderUnderPointer(Vector3 worldPoint, LayerMask layers)
    {
        // OverlapCircleAll과 판정이 정확히 같아야 한다 - 트리거 포함 여부는 전역 설정을 그대로 따르고
        // (Physics2D.queriesHitTriggers), 레이어는 SetLayerMask가 useLayerMask까지 함께 세운다.
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = Physics2D.queriesHitTriggers;
        filter.SetLayerMask(layers);
        filter.ClearDepth();

        Physics2D.OverlapCircle(worldPoint, PICK_RADIUS, filter, COLLIDER_BUFFER);

        BaseMonster closest = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < COLLIDER_BUFFER.Count; i++)
        {
            BaseMonster monster = COLLIDER_BUFFER[i].GetComponentInParent<BaseMonster>();

            if (monster == null || monster.IsDead || monster.IsFogHidden)
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
