using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunModifierResolverTests
{
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
    public void Resolve_EmptySelection_MatchesNeutral()
    {
        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(new List<RunMutatorSelection>());
        RunModifierSnapshot neutral = RunModifierSnapshot.Neutral;

        Assert.That(
            snapshot.GetMultiplier(RunModifierChannel.ChunkYield),
            Is.EqualTo(neutral.GetMultiplier(RunModifierChannel.ChunkYield)));
        Assert.That(
            snapshot.GetMultiplier(RunModifierChannel.FoodUpkeep),
            Is.EqualTo(neutral.GetMultiplier(RunModifierChannel.FoodUpkeep)));
        Assert.That(
            snapshot.GetCounter(RunCounterChannel.EggHatchDays),
            Is.EqualTo(neutral.GetCounter(RunCounterChannel.EggHatchDays)));
        Assert.That(snapshot.HasRule(RunRuleFlag.Ironman), Is.False);
        Assert.That(snapshot.HasRule(RunRuleFlag.NoMorningRestore), Is.False);
    }

    [Test]
    public void Resolve_NullSelection_MatchesNeutral()
    {
        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(null);

        Assert.That(snapshot.GetMultiplier(RunModifierChannel.ChunkYield), Is.EqualTo(1f));
        Assert.That(snapshot.GetCounter(RunCounterChannel.EggHatchDays), Is.EqualTo(0));
        Assert.That(snapshot.HasRule(RunRuleFlag.Ironman), Is.False);
    }

    [Test]
    public void Resolve_TwoMultiplierEffectsOnSameChannel_MultipliesThem()
    {
        RunMutatorSO first = CreateMultiplierMutator(
            "lean_a", RunModifierChannel.ChunkYield, 0.75f, 4);
        RunMutatorSO second = CreateMultiplierMutator(
            "lean_b", RunModifierChannel.ChunkYield, 0.5f, 4);

        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(first, 1),
            new RunMutatorSelection(second, 1),
        });

        Assert.That(
            snapshot.GetMultiplier(RunModifierChannel.ChunkYield),
            Is.EqualTo(0.375f).Within(0.0001f));

        // 다른 채널은 항등원이 유지되어야 한다.
        Assert.That(snapshot.GetMultiplier(RunModifierChannel.FoodUpkeep), Is.EqualTo(1f));
    }

    [Test]
    public void Resolve_CounterEffects_SumThem()
    {
        RunMutatorSO first = CreateCounterMutator("gravity_a", RunCounterChannel.EggHatchDays, 1);
        RunMutatorSO second = CreateCounterMutator("gravity_b", RunCounterChannel.EggHatchDays, 2);

        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(first, 1),
            new RunMutatorSelection(second, 1),
        });

        Assert.That(snapshot.GetCounter(RunCounterChannel.EggHatchDays), Is.EqualTo(3));
        Assert.That(snapshot.GetCounter(RunCounterChannel.DragonTypeChangeLimitPerCycle), Is.EqualTo(0));
    }

    [Test]
    public void Resolve_RuleEffects_OrsThem()
    {
        RunMutatorSO longNight = CreateRuleMutator("no_morning_restore", RunRuleFlag.NoMorningRestore);
        RunMutatorSO ironman = CreateRuleMutator("ironman", RunRuleFlag.Ironman);

        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(longNight, 1),
            new RunMutatorSelection(ironman, 1),
        });

        Assert.That(snapshot.HasRule(RunRuleFlag.NoMorningRestore), Is.True);
        Assert.That(snapshot.HasRule(RunRuleFlag.Ironman), Is.True);
    }

    [Test]
    public void Resolve_SameMutatorDifferentTier_ProducesDifferentSnapshot()
    {
        RunMutatorSO leanHarvest = CreateTieredMultiplierMutator(
            "lean_harvest",
            RunModifierChannel.ChunkYield,
            new[] { 0.9f, 0.8f, 0.7f },
            new[] { 2, 4, 6 });

        RunModifierSnapshot tier1 = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 1),
        });
        RunModifierSnapshot tier3 = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 3),
        });

        Assert.That(tier1.GetMultiplier(RunModifierChannel.ChunkYield), Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(tier3.GetMultiplier(RunModifierChannel.ChunkYield), Is.EqualTo(0.7f).Within(0.0001f));
    }

    [Test]
    public void ResolveDifficultyScore_UsesSelectedTierScore()
    {
        RunMutatorSO leanHarvest = CreateTieredMultiplierMutator(
            "lean_harvest",
            RunModifierChannel.ChunkYield,
            new[] { 0.9f, 0.8f, 0.7f },
            new[] { 2, 4, 6 });

        int tier1Score = RunModifierResolver.ResolveDifficultyScore(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 1),
        });
        int tier3Score = RunModifierResolver.ResolveDifficultyScore(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(leanHarvest, 3),
        });

        Assert.That(tier1Score, Is.EqualTo(2));
        Assert.That(tier3Score, Is.EqualTo(6));
    }

    [Test]
    public void Resolve_NullMutatorAndOutOfRangeTier_SkipsThemAndComposesTheRest()
    {
        RunMutatorSO valid = CreateMultiplierMutator(
            "lean_valid", RunModifierChannel.ChunkYield, 0.5f, 4);
        RunMutatorSO singleTier = CreateMultiplierMutator(
            "lean_single", RunModifierChannel.ChunkYield, 0.25f, 2);

        RunModifierSnapshot snapshot = RunModifierResolver.Resolve(new List<RunMutatorSelection>
        {
            new RunMutatorSelection(null, 1),
            new RunMutatorSelection(singleTier, 2), // Tiers 길이 1을 넘는 단계
            new RunMutatorSelection(singleTier, 0), // 미선택 단계
            new RunMutatorSelection(valid, 1),
        });

        Assert.That(
            snapshot.GetMultiplier(RunModifierChannel.ChunkYield),
            Is.EqualTo(0.5f).Within(0.0001f));
    }

    private RunMutatorSO CreateMultiplierMutator(
        string id,
        RunModifierChannel channel,
        float multiplier,
        int score)
    {
        return CreateTieredMultiplierMutator(
            id, channel, new[] { multiplier }, new[] { score });
    }

    private RunMutatorSO CreateTieredMultiplierMutator(
        string id,
        RunModifierChannel channel,
        float[] multipliers,
        int[] scores)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            RunMultiplierEffectSO effect = Create<RunMultiplierEffectSO>();
            effect.Configure(channel, multipliers[i]);

            RunMutatorTier tier = new RunMutatorTier();
            tier.Configure(scores[i], $"{id}_tier{i + 1}_desc", new RunMutatorEffectSO[] { effect });
            tiers[i] = tier;
        }

        RunMutatorSO mutator = Create<RunMutatorSO>();
        mutator.Configure(id, $"{id}_name", string.Empty, tiers);
        return mutator;
    }

    private RunMutatorSO CreateCounterMutator(string id, RunCounterChannel channel, int delta)
    {
        RunCounterEffectSO effect = Create<RunCounterEffectSO>();
        effect.Configure(channel, delta);

        RunMutatorTier tier = new RunMutatorTier();
        tier.Configure(2, $"{id}_tier1_desc", new RunMutatorEffectSO[] { effect });

        RunMutatorSO mutator = Create<RunMutatorSO>();
        mutator.Configure(id, $"{id}_name", string.Empty, new[] { tier });
        return mutator;
    }

    private RunMutatorSO CreateRuleMutator(string id, RunRuleFlag rules)
    {
        RunRuleEffectSO effect = Create<RunRuleEffectSO>();
        effect.Configure(rules);

        RunMutatorTier tier = new RunMutatorTier();
        tier.Configure(5, $"{id}_tier1_desc", new RunMutatorEffectSO[] { effect });

        RunMutatorSO mutator = Create<RunMutatorSO>();
        mutator.Configure(id, $"{id}_name", string.Empty, new[] { tier });
        return mutator;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }
}
