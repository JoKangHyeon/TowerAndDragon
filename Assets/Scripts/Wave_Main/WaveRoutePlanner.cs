using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 포탈 편성을 루트별 배정으로 푼다.
///
/// - 기본 수량은 웨이브 데이터가 지정한 루트로 그대로 간다.
/// - 점령 페널티로 "추가된 개체"(EnemyEnhancementSnapshot.SpawnCountBonus)는
///   그날 활성화된 루트들에 라운드로빈으로 흩어진다. 커서는 포탈 단위로 유지되어
///   루트와 스폰 그룹을 넘어가며 이어진다.
/// - 개체 수를 깎는 음수 보너스는 흩뿌리지 않고 원래 루트의 기본 수량에서 뺀다.
///
/// WaveManager와 PortalPathVisibility가 모두 이 결과를 쓰기 때문에
/// "적이 지나갈 루트"와 "화면에 켜지는 경로"가 어긋나지 않는다.
/// </summary>
public static class WaveRoutePlanner
{
    private static readonly RouteSpawnPlan[] EMPTY_ROUTES = Array.Empty<RouteSpawnPlan>();

    public static PortalRoutePlan BuildPortalPlan(
        PortalWaveData portalWave,
        Portal portal,
        EnemyEnhancementManager enhancementManager)
    {
        if (portalWave == null || portal == null)
        {
            return new PortalRoutePlan(portal, portalWave, EMPTY_ROUTES);
        }

        List<RouteWaveData> routeWaves = CollectValidRouteWaves(portalWave, portal);

        if (routeWaves.Count == 0)
        {
            return new PortalRoutePlan(portal, portalWave, EMPTY_ROUTES);
        }

        List<int> activeRouteIndices = new List<int>(routeWaves.Count);

        foreach (RouteWaveData routeWave in routeWaves)
        {
            activeRouteIndices.Add(routeWave.RouteIndex);
        }

        IReadOnlyList<EnemyEnhancementProfileSO> profiles =
            enhancementManager != null
                ? enhancementManager.GetProfiles(portal.TerrainType)
                : Array.Empty<EnemyEnhancementProfileSO>();

        Dictionary<int, List<RuntimeSpawnGroup>> groupsByRoute = new();

        foreach (int routeIndex in activeRouteIndices)
        {
            groupsByRoute[routeIndex] = new List<RuntimeSpawnGroup>();
        }

        // 같은 MonsterData의 추가 개체는 포탈 단위로 한 번만 적용한다.
        // 루트가 여러 개라고 점령 페널티가 중복되면 안 된다.
        HashSet<MonsterData> monstersWithAllocatedSpawnBonus = new();
        Dictionary<int, int> bonusByRoute = new();
        int roundRobinCursor = 0;

        foreach (RouteWaveData routeWave in routeWaves)
        {
            foreach (SpawnGroupData spawnGroup in routeWave.SpawnGroups)
            {
                if (spawnGroup == null)
                {
                    continue;
                }

                EnemyEnhancementSnapshot enhancement =
                    EnemyEnhancementResolver.Resolve(profiles, spawnGroup.MonsterData);

                int spawnCountBonus = monstersWithAllocatedSpawnBonus.Add(spawnGroup.MonsterData)
                    ? enhancement.SpawnCountBonus
                    : 0;

                float spawnInterval = Mathf.Max(
                    0f,
                    enhancement.SpawnInterval.Apply(spawnGroup.SpawnInterval));

                // 개체를 깎는 쪽은 원래 루트에서 처리하고, 늘리는 쪽만 흩뿌린다.
                int baseCount = Mathf.Max(
                    0,
                    spawnGroup.SpawnCount + Mathf.Min(0, spawnCountBonus));

                int distributedBonus = Mathf.Max(0, spawnCountBonus);

                roundRobinCursor = DistributeBonus(
                    distributedBonus,
                    activeRouteIndices,
                    roundRobinCursor,
                    bonusByRoute);

                AppendSpawnGroups(
                    spawnGroup,
                    enhancement,
                    spawnInterval,
                    baseCount,
                    routeWave.RouteIndex,
                    activeRouteIndices,
                    bonusByRoute,
                    groupsByRoute);
            }
        }

        return new PortalRoutePlan(
            portal,
            portalWave,
            BuildRoutePlans(routeWaves, portal, groupsByRoute));
    }

    private static List<RouteWaveData> CollectValidRouteWaves(
        PortalWaveData portalWave,
        Portal portal)
    {
        List<RouteWaveData> routeWaves = new();

        if (portalWave.RouteWaves == null)
        {
            Debug.LogError(
                $"[WaveRoutePlanner] {portalWave.PortalDirectionId} 포탈의 루트 편성 목록이 없습니다.",
                portal);

            return routeWaves;
        }

        HashSet<int> seenRouteIndices = new();

        foreach (RouteWaveData routeWave in portalWave.RouteWaves)
        {
            if (routeWave == null || routeWave.SpawnGroups == null)
            {
                Debug.LogError(
                    $"[WaveRoutePlanner] {portalWave.PortalDirectionId} 포탈에 비어 있는 루트 편성이 있습니다.",
                    portal);

                continue;
            }

            if (!portal.TryGetGroundPath(routeWave.RouteIndex, out _))
            {
                Debug.LogError(
                    $"[WaveRoutePlanner] {portalWave.PortalDirectionId} 포탈에 루트 {routeWave.RouteIndex}가 " +
                    $"없습니다. 보유 루트 수={portal.RouteCount}",
                    portal);

                continue;
            }

            if (!seenRouteIndices.Add(routeWave.RouteIndex))
            {
                Debug.LogError(
                    $"[WaveRoutePlanner] {portalWave.PortalDirectionId} 포탈의 루트 " +
                    $"{routeWave.RouteIndex} 편성이 중복되었습니다.",
                    portal);

                continue;
            }

            routeWaves.Add(routeWave);
        }

        return routeWaves;
    }

    /// <summary>추가된 개체를 활성 루트에 하나씩 라운드로빈으로 배정하고, 진행된 커서를 돌려준다.</summary>
    private static int DistributeBonus(
        int bonusCount,
        IReadOnlyList<int> activeRouteIndices,
        int roundRobinCursor,
        Dictionary<int, int> bonusByRoute)
    {
        bonusByRoute.Clear();

        for (int i = 0; i < bonusCount; i++)
        {
            int targetRoute = activeRouteIndices[roundRobinCursor % activeRouteIndices.Count];
            roundRobinCursor++;

            bonusByRoute.TryGetValue(targetRoute, out int assigned);
            bonusByRoute[targetRoute] = assigned + 1;
        }

        return roundRobinCursor;
    }

    private static void AppendSpawnGroups(
        SpawnGroupData spawnGroup,
        EnemyEnhancementSnapshot enhancement,
        float spawnInterval,
        int baseCount,
        int sourceRouteIndex,
        IReadOnlyList<int> activeRouteIndices,
        Dictionary<int, int> bonusByRoute,
        Dictionary<int, List<RuntimeSpawnGroup>> groupsByRoute)
    {
        bonusByRoute.TryGetValue(sourceRouteIndex, out int ownBonus);
        int ownCount = baseCount + ownBonus;

        if (ownCount > 0)
        {
            groupsByRoute[sourceRouteIndex].Add(new RuntimeSpawnGroup(
                spawnGroup,
                enhancement,
                ownCount,
                spawnInterval,
                spawnGroup.DelayBeforeGroup,
                ownBonus));
        }

        // 다른 루트로 흩어진 몫은 별도 무리로 붙인다.
        // 원본 그룹의 선행 대기는 그 루트의 편성과 무관하므로 물려주지 않는다.
        foreach (int routeIndex in activeRouteIndices)
        {
            if (routeIndex == sourceRouteIndex)
            {
                continue;
            }

            if (!bonusByRoute.TryGetValue(routeIndex, out int bonus) || bonus == 0)
            {
                continue;
            }

            groupsByRoute[routeIndex].Add(new RuntimeSpawnGroup(
                spawnGroup,
                enhancement,
                bonus,
                spawnInterval,
                0f,
                bonus));
        }
    }

    private static List<RouteSpawnPlan> BuildRoutePlans(
        IReadOnlyList<RouteWaveData> routeWaves,
        Portal portal,
        Dictionary<int, List<RuntimeSpawnGroup>> groupsByRoute)
    {
        List<RouteSpawnPlan> routePlans = new(routeWaves.Count);

        foreach (RouteWaveData routeWave in routeWaves)
        {
            List<RuntimeSpawnGroup> spawnGroups = groupsByRoute[routeWave.RouteIndex];
            int totalSpawnCount = 0;

            foreach (RuntimeSpawnGroup spawnGroup in spawnGroups)
            {
                totalSpawnCount += spawnGroup.SpawnCount;
            }

            // 적이 한 마리도 배정되지 않은 루트는 스폰도 표시도 하지 않는다.
            if (totalSpawnCount == 0)
            {
                continue;
            }

            portal.TryGetGroundPath(routeWave.RouteIndex, out SplineContainer path);

            routePlans.Add(new RouteSpawnPlan(
                routeWave.RouteIndex,
                path,
                routeWave.StartDelay,
                spawnGroups,
                totalSpawnCount));
        }

        return routePlans;
    }
}
