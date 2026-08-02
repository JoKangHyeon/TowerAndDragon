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
                !TryGetActiveAura(source, out TowerAuraDataSO aura) ||
                !IsWithinAura(source, target, aura.Radius))
            {
                continue;
            }

            modifiers = modifiers.CombineStrongest(aura);
        }

        return modifiers;
    }

    private static bool TryGetActiveAura(
        Tower source,
        out TowerAuraDataSO aura)
    {
        aura = null;

        if (source.IsReviving ||
            source.IsParalyzed ||
            source.Data is not ITowerAuraDataProvider provider ||
            !provider.HasTowerAura)
        {
            return false;
        }

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
        }

        aura = provider.TowerAura;
        return true;
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
}