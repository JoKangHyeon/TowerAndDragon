using System.Collections.Generic;
using UnityEngine;

public static class AuraTargetScanner
{
    public static void FindNearbyMonsters(
        BaseMonster owner,
        float radius,
        LayerMask targetLayers,
        HashSet<BaseMonster> results    
    )
    {
        float radiusY = radius * IsometricMath.RADIUS_Y_RATIO;

        results.Clear();
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            owner.transform.position,
            radius,
            targetLayers
        );

        foreach (Collider2D candidate in candidates)
        {
            if (!IsometricMath.IsWithinEllipse(
                candidate.transform.position,
                owner.transform.position,
                radius,
                radiusY
            ))
            {
                continue;
            }

            BaseMonster target = candidate.GetComponentInParent<BaseMonster>();
            if (target == null || target == owner || target.IsDead)
            {
                continue;
            }

            

            results.Add(target);
        }
    }
}