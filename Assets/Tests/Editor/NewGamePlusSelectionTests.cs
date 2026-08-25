using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 진입 UI의 선택 규칙 검사. 카탈로그 에셋에는 아직 규칙 계열 뮤테이터도 프리셋도 없으므로
// (T6이 저작한다) Configure로 임의의 데이터를 만들어 확인한다.
public class NewGamePlusSelectionTests
{
    private const string LEAN_HARVEST_ID = "lean_harvest";
    private const string BIG_APPETITE_ID = "big_appetite";
    private const string LAST_STAND_ID = "last_stand";
    private const string UNKNOWN_ID = "not_in_catalog";
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
    public void Count_SkipsEmptyAndTierlessEntries()
    {
        RunMutatorSO tierless = CreateMutator(UNKNOWN_ID, string.Empty, new int[0]);
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }),
            tierless,
            null);

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);

        Assert.That(selection.Count, Is.EqualTo(1));
        Assert.That(selection.MutatorAt(0).Id, Is.EqualTo(LEAN_HARVEST_ID));
    }

    // 완료 기준: 3단계짜리는 단계가 3개, 규칙 계열은 1개여야 한다(칸 수는 이 값에서 나온다).
    [Test]
    public void TierCount_ComesFromData()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 }),
            CreateMutator(LAST_STAND_ID, string.Empty, new[] { 5 }));

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);

        Assert.That(selection.MutatorAt(0).TierCount, Is.EqualTo(3));
        Assert.That(selection.MutatorAt(1).TierCount, Is.EqualTo(1));
    }

    [Test]
    public void DifficultyScore_NothingSelected_IsZero()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 })));

        Assert.That(selection.DifficultyScore, Is.EqualTo(0));
        Assert.That(selection.SelectedCount, Is.EqualTo(0));
    }

    [Test]
    public void DifficultyScore_SumsSelectedTiers()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 }),
            CreateMutator(LAST_STAND_ID, string.Empty, new[] { 5 })));

        Assert.That(selection.TrySetTier(0, 3), Is.True);
        Assert.That(selection.TrySetTier(1, 1), Is.True);

        Assert.That(selection.DifficultyScore, Is.EqualTo(11));
        Assert.That(selection.SelectedCount, Is.EqualTo(2));
    }

    [Test]
    public void TrySetTier_ChangingTier_ReplacesScore()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 })));

        selection.TrySetTier(0, 1);
        Assert.That(selection.DifficultyScore, Is.EqualTo(2));

        selection.TrySetTier(0, 3);
        Assert.That(selection.DifficultyScore, Is.EqualTo(6));
    }

    [Test]
    public void TrySetTier_UnselectedTier_ClearsSelection()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 })));

        selection.TrySetTier(0, 2);
        Assert.That(selection.TrySetTier(0, RunMutatorSO.UNSELECTED_TIER), Is.True);

        Assert.That(selection.IsSelected(0), Is.False);
        Assert.That(selection.DifficultyScore, Is.EqualTo(0));
    }

    [Test]
    public void TrySetTier_OutOfRangeTier_Fails()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4 })));

        Assert.That(selection.TrySetTier(0, 3), Is.False);
        Assert.That(selection.IsSelected(0), Is.False);
    }

    // 판정은 카탈로그의 HasExclusiveConflict가 한다. 여기서는 "거부되고 상태가 안 바뀐다"를 본다.
    [Test]
    public void TrySetTier_SameExclusiveGroup_IsRejectedWithoutChangingState()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, ECONOMY_GROUP, new[] { 2, 4, 6 }),
            CreateMutator(BIG_APPETITE_ID, ECONOMY_GROUP, new[] { 2, 4, 6 })));

        Assert.That(selection.TrySetTier(0, 2), Is.True);
        Assert.That(selection.TrySetTier(1, 1), Is.False);

        Assert.That(selection.IsSelected(1), Is.False);
        Assert.That(selection.DifficultyScore, Is.EqualTo(4));
    }

    [Test]
    public void TrySetTier_EmptyExclusiveGroup_AllowsBoth()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2 }),
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2 })));

        Assert.That(selection.TrySetTier(0, 1), Is.True);
        Assert.That(selection.TrySetTier(1, 1), Is.True);
        Assert.That(selection.DifficultyScore, Is.EqualTo(4));
    }

    [Test]
    public void BuildRequest_ContainsOnlySelectedPairs()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }),
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 })));

        selection.TrySetTier(1, 2);

        List<(string Id, int Tier)> request = selection.BuildRequest();

        Assert.That(request.Count, Is.EqualTo(1));
        Assert.That(request[0].Id, Is.EqualTo(BIG_APPETITE_ID));
        Assert.That(request[0].Tier, Is.EqualTo(2));
    }

    [Test]
    public void TryApplyPreset_ValidPreset_SelectsExactCombination()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }),
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 }));

        RunMutatorCatalogSO.Preset preset = CreatePreset((LEAN_HARVEST_ID, 3), (BIG_APPETITE_ID, 1));
        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);

        Assert.That(selection.TryApplyPreset(preset), Is.True);

        Assert.That(selection.TierAt(0), Is.EqualTo(3));
        Assert.That(selection.TierAt(1), Is.EqualTo(1));
        Assert.That(selection.DifficultyScore, Is.EqualTo(8));
        Assert.That(selection.MatchesPreset(preset), Is.True);
    }

    [Test]
    public void TryApplyPreset_ReplacesPreviousSelection()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }),
            CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 }));

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);
        selection.TrySetTier(1, 3);

        Assert.That(selection.TryApplyPreset(CreatePreset((LEAN_HARVEST_ID, 1))), Is.True);

        Assert.That(selection.TierAt(0), Is.EqualTo(1));
        Assert.That(selection.TierAt(1), Is.EqualTo(RunMutatorSO.UNSELECTED_TIER));
    }

    [Test]
    public void TryApplyPreset_UnknownId_IsRejectedWithoutChangingState()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4, 6 }));

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);
        selection.TrySetTier(0, 2);

        Assert.That(selection.TryApplyPreset(CreatePreset((UNKNOWN_ID, 1))), Is.False);
        Assert.That(selection.TierAt(0), Is.EqualTo(2));
    }

    [Test]
    public void TryApplyPreset_OutOfRangeTier_IsRejected()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, string.Empty, new[] { 2, 4 }));

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);

        Assert.That(selection.TryApplyPreset(CreatePreset((LEAN_HARVEST_ID, 3))), Is.False);
        Assert.That(selection.IsSelected(0), Is.False);
    }

    [Test]
    public void TryApplyPreset_ConflictingPreset_IsRejected()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateMutator(LEAN_HARVEST_ID, ECONOMY_GROUP, new[] { 2 }),
            CreateMutator(BIG_APPETITE_ID, ECONOMY_GROUP, new[] { 2 }));

        NewGamePlusSelection selection = new NewGamePlusSelection(catalog);

        Assert.That(
            selection.TryApplyPreset(CreatePreset((LEAN_HARVEST_ID, 1), (BIG_APPETITE_ID, 1))),
            Is.False);
        Assert.That(selection.SelectedCount, Is.EqualTo(0));
    }

    [Test]
    public void ShownValues_UnselectedItem_PreviewsFirstTier()
    {
        RunMutatorSO mutator = CreateMutator(BIG_APPETITE_ID, string.Empty, new[] { 2, 4, 6 });
        NewGamePlusSelection selection = new NewGamePlusSelection(CreateCatalog(mutator));

        Assert.That(selection.ShownScoreAt(0), Is.EqualTo(2));
        Assert.That(selection.DescLocKeyAt(0), Is.EqualTo(DescKeyOf(BIG_APPETITE_ID, 1)));

        selection.TrySetTier(0, 3);

        Assert.That(selection.ShownScoreAt(0), Is.EqualTo(6));
        Assert.That(selection.DescLocKeyAt(0), Is.EqualTo(DescKeyOf(BIG_APPETITE_ID, 3)));
    }

    [Test]
    public void NullCatalog_DegradesToEmptyList()
    {
        NewGamePlusSelection selection = new NewGamePlusSelection(null);

        Assert.That(selection.Count, Is.EqualTo(0));
        Assert.That(selection.DifficultyScore, Is.EqualTo(0));
        Assert.That(selection.BuildRequest(), Is.Empty);
    }

    private static string DescKeyOf(string id, int tier) => $"{id}_tier{tier}_desc";

    private RunMutatorSO CreateMutator(string id, string exclusiveGroup, int[] tierScores)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[tierScores.Length];

        for (int i = 0; i < tierScores.Length; i++)
        {
            tiers[i] = new RunMutatorTier();
            tiers[i].Configure(tierScores[i], DescKeyOf(id, i + 1), new RunMutatorEffectSO[0]);
        }

        RunMutatorSO mutator = ScriptableObject.CreateInstance<RunMutatorSO>();
        mutator.Configure(id, $"{id}_name", exclusiveGroup, tiers);
        _created.Add(mutator);
        return mutator;
    }

    private RunMutatorCatalogSO CreateCatalog(params RunMutatorSO[] mutators)
    {
        RunMutatorCatalogSO catalog = ScriptableObject.CreateInstance<RunMutatorCatalogSO>();
        SetMutators(catalog, mutators);
        _created.Add(catalog);
        return catalog;
    }

    // 카탈로그의 목록·프리셋 필드에는 Configure가 없어(에셋 저작은 인스펙터가 한다)
    // 테스트에서만 리플렉션으로 채운다.
    private static void SetMutators(RunMutatorCatalogSO catalog, RunMutatorSO[] mutators)
    {
        typeof(RunMutatorCatalogSO)
            .GetField("_mutators", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            .SetValue(catalog, mutators);
    }

    private static RunMutatorCatalogSO.Preset CreatePreset(params (string Id, int Tier)[] entries)
    {
        RunMutatorCatalogSO.PresetEntry[] created =
            new RunMutatorCatalogSO.PresetEntry[entries.Length];

        for (int i = 0; i < entries.Length; i++)
        {
            created[i] = new RunMutatorCatalogSO.PresetEntry();
            created[i].Configure(entries[i].Id, entries[i].Tier);
        }

        RunMutatorCatalogSO.Preset preset = new RunMutatorCatalogSO.Preset();
        preset.Configure("preset_name", created);
        return preset;
    }
}
