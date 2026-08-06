using System.Collections.Generic;
using UnityEngine.Splines;

/// <summary>
/// 강화가 적용된 뒤 실제로 생성할 적 한 무리. 원본 SpawnGroupData에 점령 페널티 결과를 얹은 것이다.
/// </summary>
public sealed class RuntimeSpawnGroup
{
    public SpawnGroupData Source { get; }
    public EnemyEnhancementSnapshot Enhancement { get; }
    public int SpawnCount { get; }
    public float SpawnInterval { get; }
    public float DelayBeforeGroup { get; }

    /// <summary>이 무리에 포함된 적 중 점령 페널티로 추가된 개체 수.</summary>
    public int SpawnCountBonus { get; }

    public RuntimeSpawnGroup(
        SpawnGroupData source,
        EnemyEnhancementSnapshot enhancement,
        int spawnCount,
        float spawnInterval,
        float delayBeforeGroup,
        int spawnCountBonus)
    {
        Source = source;
        Enhancement = enhancement;
        SpawnCount = spawnCount;
        SpawnInterval = spawnInterval;
        DelayBeforeGroup = delayBeforeGroup;
        SpawnCountBonus = spawnCountBonus;
    }
}

/// <summary>
/// 한 포탈의 한 루트에 배정된 편성. 이 루트로 실제 적이 나간다.
/// </summary>
public sealed class RouteSpawnPlan
{
    public int RouteIndex { get; }
    public SplineContainer Path { get; }
    public float StartDelay { get; }
    public IReadOnlyList<RuntimeSpawnGroup> SpawnGroups { get; }
    public int TotalSpawnCount { get; }

    public RouteSpawnPlan(
        int routeIndex,
        SplineContainer path,
        float startDelay,
        IReadOnlyList<RuntimeSpawnGroup> spawnGroups,
        int totalSpawnCount)
    {
        RouteIndex = routeIndex;
        Path = path;
        StartDelay = startDelay;
        SpawnGroups = spawnGroups;
        TotalSpawnCount = totalSpawnCount;
    }
}

/// <summary>
/// 한 포탈의 하루치 루트 배정 결과. Routes에는 실제로 적이 배정된 루트만 담긴다.
/// WaveManager(실제 스폰)와 PortalPathVisibility(경로 표시)가 같은 결과를 보도록 하는 단일 출처다.
/// </summary>
public sealed class PortalRoutePlan
{
    public Portal Portal { get; }
    public PortalWaveData Source { get; }
    public IReadOnlyList<RouteSpawnPlan> Routes { get; }

    public bool HasSpawns => Routes.Count > 0;

    public PortalRoutePlan(
        Portal portal,
        PortalWaveData source,
        IReadOnlyList<RouteSpawnPlan> routes)
    {
        Portal = portal;
        Source = source;
        Routes = routes;
    }
}
