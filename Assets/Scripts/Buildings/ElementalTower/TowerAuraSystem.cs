using System.Collections.Generic;
using UnityEngine;

public sealed class TowerAuraSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    // 재진입 가드 - TowerPopulation.CanOperate/StaffingRatio는 오라로 받은 보너스 인구
    // (SoulPopulation)를 포함하므로, ResolveModifiers(target)가 다른 타워의 오라 활성 여부를
    // 확인하려고 그 타워의 CanOperate를 읽으면 그 타워의 SoulPopulation이 다시 ResolveModifiers를
    // 호출해 target을 되짚어보는 상호 재귀에 빠질 수 있다(StackOverflowException).
    // 지금 이 target을 이미 처리 중이면(호출 스택에 이미 있으면) 중립값으로 즉시 끊는다.
    //
    // 주의(정밀도 트레이드오프) - 영혼 타워도 TowerPopulation을 쓰므로, 영혼 타워 두 개가 서로의
    // 오라 범위 안에 들어오면 진짜 순환(서로에게 SoulPopulation을 주는 사이)이 생긴다. 그 경우
    // 이 가드가 끊는 되짚기 값은 0이 아닐 수 있고, 어느 쪽이 먼저 계산되느냐(그리드 순회 순서)에
    // 따라 결과가 갈릴 여지가 있다 - 크래시는 절대 안 나지만, 그 특정 배치(영혼 타워끼리 겹침)에서는
    // 근사값일 수 있다는 뜻. 실제 게임플레이에서 흔한 경우는 아니라 근사로 두었다 - 눈에 보이는
    // 문제(깜빡임 등)가 확인되면 고정점 반복 방식으로 다시 봐야 한다.
    private readonly HashSet<Tower> _resolvingTargets = new();

    public TowerAuraModifiers ResolveModifiers(Tower target)
    {
        if (_gridMap == null || target == null)
        {
            return TowerAuraModifiers.Neutral;
        }

        if (!_resolvingTargets.Add(target))
        {
            return TowerAuraModifiers.Neutral;
        }

        try
        {
            TowerAuraModifiers modifiers =
                TowerAuraModifiers.Neutral;

            foreach (Building building in _gridMap.Buildings)
            {
                if (building is not Tower source ||
                    source == target ||
                    !TryGetActiveAura(
                        source,
                        out TowerAuraDataSO aura,
                        out float effectiveRadius) ||
                    !IsWithinAura(
                        source,
                        target,
                        effectiveRadius))
                {
                    continue;
                }

                modifiers = modifiers.CombineStrongest(aura);
            }

            return modifiers;
        }
        finally
        {
            _resolvingTargets.Remove(target);
        }
    }

    public static bool TryGetActiveAura(
        Tower source,
        out TowerAuraDataSO aura,
        out float effectiveRadius)
    {
        aura = null;
        effectiveRadius = 0f;

        if (source == null ||
            source.IsReviving ||
            source.IsParalyzed ||
            source.Data is not ITowerAuraDataProvider provider ||
            !provider.HasTowerAura)
        {
            return false;
        }

        aura = provider.TowerAura;
        float radiusMultiplier = 1f;

        if (source is BabyDragonTower babyDragon)
        {
            if (!babyDragon.CanOperate ||
                babyDragon.Mode != BabyDragonMode.Buff)
            {
                return false;
            }
        }
        else
        {
            ITowerStaffing staffing =
                source.GetComponent<ITowerStaffing>();

            // SoulPopulation(오라로 받은 보너스 인구)까지 포함한 실제 값을 쓴다 - 은신/시간 타워가
            // 영혼 타워로 정원을 채웠을 때도 오라 반경이 100%까지 정상적으로 커지게 하려는 것.
            // 이게 만드는 상호 재귀는 위 ResolveModifiers의 재진입 가드가 끊는다.
            if (staffing == null || !staffing.CanOperate)
            {
                return false;
            }

            if (aura.ScaleRadiusWithStaffing)
            {
                radiusMultiplier =
                    Mathf.Clamp01(staffing.StaffingRatio);
            }
        }

        effectiveRadius = aura.Radius * radiusMultiplier;
        return effectiveRadius > 0f;
    }

    private static bool IsWithinAura(
        Tower source,
        Tower target,
        float radius)
    {
        float radiusY =
            radius * IsometricMath.RADIUS_Y_RATIO;

        return IsometricMath.IsWithinEllipse(
            target.transform.position,
            source.transform.position,
            radius,
            radiusY);
    }

    public static bool TryGetPreviewRadius(
        TowerData towerData,
        out float radius)
    {
        radius = 0f;

        if (towerData is not ITowerAuraDataProvider provider ||
            !provider.HasTowerAura)
        {
            return false;
        }

        radius = provider.TowerAura.Radius;
        return radius > 0f;
    }
}
