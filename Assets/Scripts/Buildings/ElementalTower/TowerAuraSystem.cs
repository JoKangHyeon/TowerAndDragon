using UnityEngine;

public sealed class TowerAuraSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    public TowerAuraModifiers ResolveModifiers(Tower target)
    {
        if (_gridMap == null || target == null)
        {
            return TowerAuraModifiers.Neutral;
        }

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
