using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;

public class WaveRoutePlannerTests
{
    private const PortalDirection TEST_DIRECTION = PortalDirection.North;
    private const TerrainType TEST_TERRAIN = TerrainType.Snow;

    private GameObject _root;
    private Portal _portal;
    private EnemyEnhancementManager _enhancementManager;
    private readonly List<Object> _createdAssets = new();

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("WaveRoutePlannerTests");
        _root.SetActive(false);
        _enhancementManager = _root.AddComponent<EnemyEnhancementManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);

        foreach (Object asset in _createdAssets)
        {
            Object.DestroyImmediate(asset);
        }

        _createdAssets.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void BuildPortalPlan_SingleRoute_AssignsBaseCountToDesignatedRoute()
    {
        CreatePortal(routeCount: 1);
        MonsterData goblin = CreateMonsterData("Goblin");

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 5)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(plan.Routes.Count, Is.EqualTo(1));
        Assert.That(plan.Routes[0].RouteIndex, Is.EqualTo(0));
        Assert.That(plan.Routes[0].TotalSpawnCount, Is.EqualTo(5));
        Assert.That(plan.Routes[0].Path, Is.SameAs(_portal.GroundPaths[0]));
    }

    [Test]
    public void BuildPortalPlan_MultipleRoutes_KeepsBaseCountsOnDesignatedRoutes()
    {
        CreatePortal(routeCount: 2);
        MonsterData goblin = CreateMonsterData("Goblin");
        MonsterData orc = CreateMonsterData("Orc");

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 10)),
            CreateRouteWave(1, CreateSpawnGroup(orc, spawnCount: 4)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(10));
        Assert.That(CountOnRoute(plan, 1), Is.EqualTo(4));
        Assert.That(plan.Routes[0].Path, Is.SameAs(_portal.GroundPaths[0]));
        Assert.That(plan.Routes[1].Path, Is.SameAs(_portal.GroundPaths[1]));
    }

    [Test]
    public void BuildPortalPlan_SpawnCountBonus_RoundRobinsAcrossActiveRoutesOnly()
    {
        // 포탈은 루트 3개를 갖지만 오늘 편성은 0번과 2번만 쓴다.
        // 추가된 적이 편성에 없는 1번 루트로 새지 않아야 한다.
        CreatePortal(routeCount: 3);
        MonsterData goblin = CreateMonsterData("Goblin");
        ApplySpawnCountBonus(goblin, bonus: 4);

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 2)),
            CreateRouteWave(2, CreateSpawnGroup(CreateMonsterData("Orc"), spawnCount: 1)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        // 기본 2 + 보너스 2 = 4, 보너스 2 + 기본 1 = 3
        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(4));
        Assert.That(CountOnRoute(plan, 2), Is.EqualTo(3));
        Assert.That(HasRoute(plan, 1), Is.False, "편성에 없는 루트로 적이 배정되었습니다.");
    }

    [Test]
    public void BuildPortalPlan_SingleActiveRoute_KeepsAllBonusOnThatRoute()
    {
        CreatePortal(routeCount: 2);
        MonsterData goblin = CreateMonsterData("Goblin");
        ApplySpawnCountBonus(goblin, bonus: 3);

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 2)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(plan.Routes.Count, Is.EqualTo(1));
        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(5));
    }

    [Test]
    public void BuildPortalPlan_Bonus_CursorContinuesAcrossSpawnGroups()
    {
        CreatePortal(routeCount: 2);
        MonsterData goblin = CreateMonsterData("Goblin");
        MonsterData orc = CreateMonsterData("Orc");
        ApplySpawnCountBonus(goblin, bonus: 1);
        ApplySpawnCountBonus(orc, bonus: 1);

        // 커서가 그룹마다 초기화되면 두 보너스 모두 루트 0으로 몰린다.
        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(
                0,
                CreateSpawnGroup(goblin, spawnCount: 1),
                CreateSpawnGroup(orc, spawnCount: 1)),
            CreateRouteWave(1, CreateSpawnGroup(CreateMonsterData("Bat"), spawnCount: 1)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(3), "고블린 기본 1 + 오크 기본 1 + 보너스 1");
        Assert.That(CountOnRoute(plan, 1), Is.EqualTo(2), "박쥐 기본 1 + 보너스 1");
    }

    [Test]
    public void BuildPortalPlan_SameMonsterData_AppliesSpawnCountBonusOnce()
    {
        CreatePortal(routeCount: 2);
        MonsterData goblin = CreateMonsterData("Goblin");
        ApplySpawnCountBonus(goblin, bonus: 2);

        // 같은 MonsterData가 두 루트에 등장해도 점령 페널티는 한 번만 붙어야 한다.
        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 3)),
            CreateRouteWave(1, CreateSpawnGroup(goblin, spawnCount: 3)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(TotalCount(plan), Is.EqualTo(8), "기본 6 + 보너스 2여야 합니다.");
    }

    [Test]
    public void BuildPortalPlan_RouteIndexOutOfRange_SkipsRouteAndKeepsOthers()
    {
        LogAssert.ignoreFailingMessages = true;

        CreatePortal(routeCount: 1);
        MonsterData goblin = CreateMonsterData("Goblin");

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 2)),
            CreateRouteWave(5, CreateSpawnGroup(CreateMonsterData("Orc"), spawnCount: 9)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(plan.Routes.Count, Is.EqualTo(1));
        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(2));
    }

    [Test]
    public void BuildPortalPlan_ZeroFinalSpawnCount_ExcludesRoute()
    {
        CreatePortal(routeCount: 2);

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(CreateMonsterData("Goblin"), spawnCount: 3)),
            CreateRouteWave(1, CreateSpawnGroup(CreateMonsterData("Orc"), spawnCount: 0)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(plan.Routes.Count, Is.EqualTo(1));
        Assert.That(HasRoute(plan, 1), Is.False);
    }

    [Test]
    public void BuildPortalPlan_NegativeBonus_ReducesSourceRouteOnly()
    {
        CreatePortal(routeCount: 2);
        MonsterData goblin = CreateMonsterData("Goblin");
        ApplySpawnCountBonus(goblin, bonus: -2);

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(goblin, spawnCount: 5)),
            CreateRouteWave(1, CreateSpawnGroup(CreateMonsterData("Orc"), spawnCount: 4)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(
            portalWave, _portal, _enhancementManager);

        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(3));
        Assert.That(CountOnRoute(plan, 1), Is.EqualTo(4));
    }

    [Test]
    public void BuildPortalPlan_NoEnhancementManager_UsesBaseCounts()
    {
        CreatePortal(routeCount: 1);

        PortalWaveData portalWave = CreatePortalWave(
            CreateRouteWave(0, CreateSpawnGroup(CreateMonsterData("Goblin"), spawnCount: 6)));

        PortalRoutePlan plan = WaveRoutePlanner.BuildPortalPlan(portalWave, _portal, null);

        Assert.That(CountOnRoute(plan, 0), Is.EqualTo(6));
    }

    // ---------- 조회 헬퍼 ----------

    private static bool HasRoute(PortalRoutePlan plan, int routeIndex)
    {
        foreach (RouteSpawnPlan route in plan.Routes)
        {
            if (route.RouteIndex == routeIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountOnRoute(PortalRoutePlan plan, int routeIndex)
    {
        foreach (RouteSpawnPlan route in plan.Routes)
        {
            if (route.RouteIndex == routeIndex)
            {
                return route.TotalSpawnCount;
            }
        }

        return 0;
    }

    private static int TotalCount(PortalRoutePlan plan)
    {
        int total = 0;

        foreach (RouteSpawnPlan route in plan.Routes)
        {
            total += route.TotalSpawnCount;
        }

        return total;
    }

    // ---------- 생성 헬퍼 ----------

    private void CreatePortal(int routeCount)
    {
        GameObject portalObject = new GameObject("Portal");
        portalObject.transform.SetParent(_root.transform);
        _portal = portalObject.AddComponent<Portal>();

        List<SplineContainer> paths = new List<SplineContainer>(routeCount);

        for (int i = 0; i < routeCount; i++)
        {
            GameObject pathObject = new GameObject($"Route{i}");
            pathObject.transform.SetParent(_root.transform);
            paths.Add(pathObject.AddComponent<SplineContainer>());
        }

        SetPrivateField(_portal, "_portalDirectionId", TEST_DIRECTION);
        SetPrivateField(_portal, "_terrainType", TEST_TERRAIN);
        SetPrivateField(_portal, "_groundPaths", paths);
    }

    private MonsterData CreateMonsterData(string monsterName)
    {
        MonsterData monsterData = ScriptableObject.CreateInstance<MonsterData>();
        monsterData.name = monsterName;
        _createdAssets.Add(monsterData);
        return monsterData;
    }

    private void ApplySpawnCountBonus(MonsterData targetMonster, int bonus)
    {
        EnemyEnhancementRule rule = new EnemyEnhancementRule();
        SetPrivateField(rule, "_targetMonster", targetMonster);
        SetPrivateField(rule, "_spawnCountBonus", bonus);

        EnemyEnhancementProfileSO profile =
            ScriptableObject.CreateInstance<EnemyEnhancementProfileSO>();
        SetPrivateField(profile, "_rules", new List<EnemyEnhancementRule> { rule });
        _createdAssets.Add(profile);

        _enhancementManager.ApplyProfile(TEST_TERRAIN, profile);
    }

    private static SpawnGroupData CreateSpawnGroup(MonsterData monsterData, int spawnCount)
    {
        SpawnGroupData spawnGroup = new SpawnGroupData();
        SetPrivateField(spawnGroup, "_monsterData", monsterData);
        SetPrivateField(spawnGroup, "_spawnCount", spawnCount);
        return spawnGroup;
    }

    private static RouteWaveData CreateRouteWave(int routeIndex, params SpawnGroupData[] spawnGroups)
    {
        RouteWaveData routeWave = new RouteWaveData();
        SetPrivateField(routeWave, "_routeIndex", routeIndex);
        SetPrivateField(routeWave, "_spawnGroups", new List<SpawnGroupData>(spawnGroups));
        return routeWave;
    }

    private static PortalWaveData CreatePortalWave(params RouteWaveData[] routeWaves)
    {
        PortalWaveData portalWave = new PortalWaveData();
        SetPrivateField(portalWave, "_portalDirectionId", TEST_DIRECTION);
        SetPrivateField(portalWave, "_routeWaves", new List<RouteWaveData>(routeWaves));
        return portalWave;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
        field.SetValue(target, value);
    }
}
