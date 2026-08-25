using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunMutatorCatalogTests
{
    private const string LEAN_HARVEST_ID = "lean_harvest";
    private const string BIG_APPETITE_ID = "big_appetite";
    private const string ECONOMY_GROUP = "economy";

    private readonly List<ScriptableObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (ScriptableObject asset in _created)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        _created.Clear();
    }

    [Test]
    public void Resolve_KnownIdsAndTiers_Succeeds()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }));

        bool resolved = catalog.Resolve(
            new List<(string, int)> { (LEAN_HARVEST_ID, 3) },
            out List<RunMutatorSelection> selections);

        Assert.That(resolved, Is.True);
        Assert.That(selections.Count, Is.EqualTo(1));
        Assert.That(selections[0].Mutator.Id, Is.EqualTo(LEAN_HARVEST_ID));
        Assert.That(selections[0].Tier, Is.EqualTo(3));
    }

    [Test]
    public void Resolve_UnknownId_Fails()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }));

        bool resolved = catalog.Resolve(
            new List<(string, int)> { ("mutator_that_no_longer_exists", 1) },
            out _);

        Assert.That(resolved, Is.False);
    }

    [Test]
    public void Resolve_TierBeyondTiersLength_Fails()
    {
        // Tiers 길이가 2인 뮤테이터에 3단계를 요청한다 - 데이터에서 3단계를 지우고
        // 세이브에는 3이 남아 있는 상황이 이 경로로 들어온다.
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4 }));

        bool resolved = catalog.Resolve(
            new List<(string, int)> { (LEAN_HARVEST_ID, 3) },
            out _);

        Assert.That(resolved, Is.False);
    }

    [Test]
    public void Resolve_UnselectedTier_Fails()
    {
        // Tier는 1-기반이므로 0은 "안 켬"이고, 선택 목록에 들어와서는 안 되는 값이다.
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }));

        bool resolved = catalog.Resolve(
            new List<(string, int)> { (LEAN_HARVEST_ID, 0) },
            out _);

        Assert.That(resolved, Is.False);
    }

    [Test]
    public void HasExclusiveConflict_TwoMutatorsInSameGroup_ReturnsTrue()
    {
        RunMutatorSO leanHarvest = CreateMutator(LEAN_HARVEST_ID, ECONOMY_GROUP, new[] { 2 });
        RunMutatorSO bigAppetite = CreateMutator(BIG_APPETITE_ID, ECONOMY_GROUP, new[] { 2 });
        RunMutatorCatalogSO catalog = CreateCatalog(leanHarvest, bigAppetite);

        bool conflict = catalog.HasExclusiveConflict(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 1),
            new RunMutatorSelection(bigAppetite, 1),
        });

        Assert.That(conflict, Is.True);
    }

    [Test]
    public void HasExclusiveConflict_EmptyGroups_ReturnsFalse()
    {
        RunMutatorSO leanHarvest = CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2 });
        RunMutatorSO bigAppetite = CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2 });
        RunMutatorCatalogSO catalog = CreateCatalog(leanHarvest, bigAppetite);

        bool conflict = catalog.HasExclusiveConflict(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 1),
            new RunMutatorSelection(bigAppetite, 1),
        });

        Assert.That(conflict, Is.False);
    }

    [Test]
    public void ResolveDifficultyScore_SumsSelectedTierScores()
    {
        RunMutatorSO leanHarvest = CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 });
        RunMutatorSO bigAppetite = CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 });
        RunMutatorCatalogSO catalog = CreateCatalog(leanHarvest, bigAppetite);

        bool resolved = catalog.Resolve(
            new List<(string, int)> { (LEAN_HARVEST_ID, 3), (BIG_APPETITE_ID, 1) },
            out List<RunMutatorSelection> selections);

        Assert.That(resolved, Is.True);

        // 3단계 6점 + 1단계 2점. 뮤테이터 단위가 아니라 선택된 단계 단위의 합이어야 한다.
        Assert.That(RunModifierResolver.ResolveDifficultyScore(selections), Is.EqualTo(8));
    }

    [Test]
    public void TryFind_AndContains_AgreeOnMembership()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2 }));

        Assert.That(catalog.Contains(LEAN_HARVEST_ID), Is.True);
        Assert.That(catalog.TryFind(LEAN_HARVEST_ID, out RunMutatorSO found), Is.True);
        Assert.That(found.Id, Is.EqualTo(LEAN_HARVEST_ID));

        Assert.That(catalog.Contains(BIG_APPETITE_ID), Is.False);
        Assert.That(catalog.TryFind(null, out _), Is.False);
    }

    private RunMutatorCatalogSO CreateCatalog(params RunMutatorSO[] mutators)
    {
        RunMutatorCatalogSO catalog = Create<RunMutatorCatalogSO>();
        typeof(RunMutatorCatalogSO)
            .GetField("_mutators", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            .SetValue(catalog, mutators);
        return catalog;
    }

    private RunMutatorSO CreateMutator(string id, string exclusiveGroup, int[] tierScores)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[tierScores.Length];

        for (int i = 0; i < tierScores.Length; i++)
        {
            RunMultiplierEffectSO effect = Create<RunMultiplierEffectSO>();
            effect.Configure(RunModifierChannel.ChunkYield, 1f);

            RunMutatorTier tier = new RunMutatorTier();
            tier.Configure(
                tierScores[i],
                $"mutator_{id}_tier{i + 1}_desc",
                new RunMutatorEffectSO[] { effect });
            tiers[i] = tier;
        }

        RunMutatorSO mutator = Create<RunMutatorSO>();
        mutator.Configure(id, $"mutator_{id}_name", exclusiveGroup, tiers);
        return mutator;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }
}
