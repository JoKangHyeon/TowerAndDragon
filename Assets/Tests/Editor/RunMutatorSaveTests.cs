using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

// 새 게임 +(뮤테이터)의 세이브 왕복 계약.
// 여기서 막고 싶은 것은 "저장 후 불러오기가 곧 치트가 되는" 경우다 - 뮤테이터가 세이브에 남지 않거나,
// 이 빌드가 모르는 뮤테이터를 조용히 빼고 복원하면 제약이 사라진 채로 런이 이어진다.
// 타워 체력도 같은 문제다 - no_morning_restore(긴 밤)로 부서진 타워가 저장되지 않으면
// 저장 -> 불러오기만으로 전 타워가 만피로 돌아온다.
//
// 씬이 필요 없는 경로만 다룬다. SaveService(철인 모드·복원 거부 배선)는 손 검증으로 남긴다.
public class RunMutatorSaveTests
{
    private const string LEAN_HARVEST_ID = "lean_harvest";
    private const string BIG_APPETITE_ID = "big_appetite";
    private const string WEAK_GROUND_ID = "weak_ground";
    private const string UNKNOWN_ID = "not_in_catalog";

    private const int TIER_1 = 1;
    private const int TIER_2 = 2;
    private const int TIER_3 = 3;

    private const int SCORE_PER_TIER = 2;

    // 굳은 맹세(sworn_element) 카운터 왕복용. 주기 번호는 웨이브 주기(1~4)라 일차와 다른 값이다.
    private const int TYPE_CHANGE_CYCLE = 2;
    private const int TYPE_CHANGE_COUNT = 1;
    private const int ONCE_PER_CYCLE = 1;
    private const float MULTIPLIER_TOLERANCE = 0.0001f;

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

    // --- 1부: 뮤테이터 왕복 ---

    [Test]
    public void RunState_MixedTiers_SurvivesSerializationRoundTrip()
    {
        var original = CreateRunState(
            (LEAN_HARVEST_ID, TIER_1),
            (BIG_APPETITE_ID, TIER_2),
            (WEAK_GROUND_ID, TIER_3));

        RunStateDto restored = RoundTrip(original);
        restored.Normalize();

        Assert.That(
            ToPairs(restored),
            Is.EquivalentTo(new[]
            {
                (LEAN_HARVEST_ID, TIER_1),
                (BIG_APPETITE_ID, TIER_2),
                (WEAK_GROUND_ID, TIER_3),
            }));
    }

    [Test]
    public void RunState_RoundTrip_ResolvesToIdenticalSnapshot()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateTieredMultiplierMutator(LEAN_HARVEST_ID, RunModifierChannel.ChunkYield),
            CreateTieredMultiplierMutator(BIG_APPETITE_ID, RunModifierChannel.FoodUpkeep),
            CreateTieredMultiplierMutator(WEAK_GROUND_ID, RunModifierChannel.TowerMaxHealth));

        var original = CreateRunState(
            (LEAN_HARVEST_ID, TIER_1),
            (BIG_APPETITE_ID, TIER_2),
            (WEAK_GROUND_ID, TIER_3));

        RunModifierSnapshot expected = ResolveSnapshot(catalog, ToPairs(original));

        RunStateDto restored = RoundTrip(original);
        restored.Normalize();

        RunModifierSnapshot actual = ResolveSnapshot(catalog, ToPairs(restored));

        // 채널 하나만 보면 단계가 뒤바뀐 복원을 놓친다 - 전 채널을 비교한다.
        foreach (RunModifierChannel channel in System.Enum.GetValues(typeof(RunModifierChannel)))
        {
            Assert.That(
                actual.GetMultiplier(channel),
                Is.EqualTo(expected.GetMultiplier(channel)).Within(MULTIPLIER_TOLERANCE),
                $"채널 {channel}의 배율이 왕복 후 달라졌습니다.");
        }

        Assert.That(
            RunModifierResolver.ResolveDifficultyScore(ResolveSelections(catalog, ToPairs(restored))),
            Is.EqualTo(RunModifierResolver.ResolveDifficultyScore(
                ResolveSelections(catalog, ToPairs(original)))));
    }

    [Test]
    public void Resolve_UnknownId_IsRejected()
    {
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateTieredMultiplierMutator(LEAN_HARVEST_ID, RunModifierChannel.ChunkYield));

        var requested = new List<(string Id, int Tier)>
        {
            (LEAN_HARVEST_ID, TIER_1),
            (UNKNOWN_ID, TIER_1),
        };

        Assert.That(catalog.Resolve(requested, out _), Is.False);
    }

    [Test]
    public void Resolve_TierBeyondRange_IsRejected()
    {
        // 단계가 1개뿐인 뮤테이터에 2단계를 요청한다.
        RunMutatorCatalogSO catalog = CreateCatalog(
            CreateRuleMutator(LEAN_HARVEST_ID, RunRuleFlag.NoMorningRestore));

        var requested = new List<(string Id, int Tier)> { (LEAN_HARVEST_ID, TIER_2) };

        Assert.That(catalog.Resolve(requested, out _), Is.False);
    }

    [Test]
    public void Normalize_NullMutators_BecomesEmptyList()
    {
        // 뮤테이터 도입 전에 저장된 v2 세이브는 이 필드가 null로 들어온다 - 표준 모드로 열려야 한다.
        var dto = new RunStateDto
        {
            CurrentCycle = SaveValidation.FIRST_DAY_NUMBER,
            Mutators = null,
        };

        dto.Normalize();

        Assert.That(dto.Mutators, Is.Not.Null);
        Assert.That(dto.Mutators, Is.Empty);
    }

    [Test]
    public void Normalize_DropsDuplicatesBlankIdsAndUnselectedTiers()
    {
        var dto = CreateRunState(
            (LEAN_HARVEST_ID, TIER_1),
            (LEAN_HARVEST_ID, TIER_3),
            (string.Empty, TIER_1),
            ("   ", TIER_1),
            (BIG_APPETITE_ID, 0),
            (WEAK_GROUND_ID, -1),
            (BIG_APPETITE_ID, TIER_2));

        dto.Mutators.Add(null);

        dto.Normalize();

        // 중복은 첫 항목만 남는다(1단계). Tier <= 0과 빈 id는 전부 사라진다.
        Assert.That(
            ToPairs(dto),
            Is.EquivalentTo(new[] { (LEAN_HARVEST_ID, TIER_1), (BIG_APPETITE_ID, TIER_2) }));
    }

    // --- 3부: 속성 변경 카운터(sworn_element) ---
    //
    // 뮤테이터 목록·타워 체력과 같은 종류의 구멍이다 - 카운터가 세이브에 남지 않으면
    // 저장 -> 불러오기만으로 그 주기의 속성 변경권이 한 번 되살아난다.

    [Test]
    public void RunState_TypeChangeCounter_SurvivesSerializationRoundTrip()
    {
        var original = CreateRunState();
        original.TypeChangeCycleNumber = TYPE_CHANGE_CYCLE;
        original.TypeChangeCountInCycle = TYPE_CHANGE_COUNT;

        RunStateDto restored = RoundTrip(original);
        restored.Normalize();

        Assert.That(restored.TypeChangeCycleNumber, Is.EqualTo(TYPE_CHANGE_CYCLE));
        Assert.That(restored.TypeChangeCountInCycle, Is.EqualTo(TYPE_CHANGE_COUNT));
    }

    [Test]
    public void Normalize_TypeChangeCounterWithoutCycle_IsDropped()
    {
        var dto = CreateRunState();
        dto.TypeChangeCycleNumber = 0;
        dto.TypeChangeCountInCycle = TYPE_CHANGE_COUNT;

        dto.Normalize();

        // "어느 주기인지 모르는데 이미 다 썼다"는 되살릴 수 없는 형태다.
        Assert.That(dto.TypeChangeCountInCycle, Is.EqualTo(0));
    }

    [Test]
    public void Normalize_NegativeTypeChangeCounter_IsClampedToZero()
    {
        var dto = CreateRunState();
        dto.TypeChangeCycleNumber = -2;
        dto.TypeChangeCountInCycle = -5;

        dto.Normalize();

        Assert.That(dto.TypeChangeCycleNumber, Is.EqualTo(0));
        Assert.That(dto.TypeChangeCountInCycle, Is.EqualTo(0));
    }

    [Test]
    public void RunState_LegacySaveWithoutCounter_ReadsAsUnused()
    {
        // 이 필드가 없던 v2 세이브는 두 값이 기본값 0으로 들어와 "아직 안 씀"이 된다.
        var original = CreateRunState();

        RunStateDto restored = RoundTrip(original);
        restored.Normalize();

        Assert.That(restored.TypeChangeCycleNumber, Is.EqualTo(0));
        Assert.That(restored.TypeChangeCountInCycle, Is.EqualTo(0));

        // 그 값을 그대로 실은 Dragon은 어느 주기에서도 제한에 걸리지 않는다.
        var dragon = new Dragon();
        dragon.RestoreTypeChangeCounter(
            restored.TypeChangeCycleNumber, restored.TypeChangeCountInCycle);

        Assert.That(dragon.IsCycleLimitReached(TYPE_CHANGE_CYCLE, ONCE_PER_CYCLE), Is.False);
    }

    // 저장값이 Dragon까지 실제로 이어지는지 - 여기가 끊기면 DTO만 멀쩡하고 제한은 풀린다.
    [Test]
    public void RunState_UsedUpCounter_RestoresIntoBlockedDragon()
    {
        var original = CreateRunState();
        original.TypeChangeCycleNumber = TYPE_CHANGE_CYCLE;
        original.TypeChangeCountInCycle = ONCE_PER_CYCLE;

        RunStateDto restored = RoundTrip(original);
        restored.Normalize();

        var dragon = new Dragon { CurrentType = DragonType.Fire };
        dragon.RestoreTypeChangeCounter(
            restored.TypeChangeCycleNumber, restored.TypeChangeCountInCycle);

        Assert.That(
            dragon.TryChangeType(
                DragonType.Ice, TYPE_CHANGE_CYCLE, ONCE_PER_CYCLE, out DragonTypeChangeBlock block),
            Is.False);
        Assert.That(block, Is.EqualTo(DragonTypeChangeBlock.CycleLimitReached));
    }

    // --- 4부: 타워 체력 DTO ---

    [Test]
    public void Placement_NonPositiveHealthWhileActive_IsTreatedAsNoRecord()
    {
        BuildingPlacementDto placement = CreatePlacement();
        placement.CurrentHealth = -5f;
        placement.IsDisabled = false;
        placement.ReviveProgress = 0.5f;

        placement.Normalize();

        // 기록 없음: 체력은 0으로 접히고(복원이 만피를 유지한다) 부활 게이지도 의미를 잃는다.
        Assert.That(placement.CurrentHealth, Is.EqualTo(0f));
        Assert.That(placement.IsDisabled, Is.False);
        Assert.That(placement.ReviveProgress, Is.EqualTo(0f));
    }

    [Test]
    public void Placement_DisabledTower_KeepsReviveProgressAndZeroHealth()
    {
        BuildingPlacementDto placement = CreatePlacement();
        placement.CurrentHealth = 120f;
        placement.IsDisabled = true;
        placement.ReviveProgress = 0.25f;

        placement.Normalize();

        // "만피인데 비활성" 같은 모순 상태를 만들지 않는다.
        Assert.That(placement.CurrentHealth, Is.EqualTo(0f));
        Assert.That(placement.IsDisabled, Is.True);
        Assert.That(placement.ReviveProgress, Is.EqualTo(0.25f).Within(MULTIPLIER_TOLERANCE));
    }

    [Test]
    public void Placement_ReviveProgressAboveOne_IsClamped()
    {
        BuildingPlacementDto placement = CreatePlacement();
        placement.IsDisabled = true;
        placement.ReviveProgress = 4f;

        placement.Normalize();

        Assert.That(placement.ReviveProgress, Is.EqualTo(1f));
    }

    [Test]
    public void Placement_HealthFields_SurviveSerializationRoundTrip()
    {
        var map = new MapStateDto { Buildings = new List<BuildingPlacementDto>() };
        BuildingPlacementDto placement = CreatePlacement();
        placement.IsDisabled = true;
        placement.ReviveProgress = 0.75f;
        map.Buildings.Add(placement);

        MapStateDto restored = RoundTrip(map);
        restored.Normalize();

        Assert.That(restored.Buildings, Has.Count.EqualTo(1));
        Assert.That(restored.Buildings[0].IsDisabled, Is.True);
        Assert.That(
            restored.Buildings[0].ReviveProgress,
            Is.EqualTo(0.75f).Within(MULTIPLIER_TOLERANCE));
    }

    // --- 헬퍼 ---

    private static RunStateDto CreateRunState(params (string Id, int Tier)[] mutators)
    {
        var dto = new RunStateDto
        {
            CurrentCycle = SaveValidation.FIRST_DAY_NUMBER,
            Mutators = new List<RunMutatorSelectionDto>(),
        };

        foreach ((string id, int tier) in mutators)
        {
            dto.Mutators.Add(new RunMutatorSelectionDto { Id = id, Tier = tier });
        }

        return dto;
    }

    private static BuildingPlacementDto CreatePlacement() => new BuildingPlacementDto
    {
        PrefabId = "TP_Arrow",
        Anchor = Vector3IntDto.From(Vector3Int.zero),
        BabyDragonIndex = BuildingPlacementDto.NO_BABY_DRAGON_INDEX,
    };

    private static List<(string Id, int Tier)> ToPairs(RunStateDto dto) =>
        SaveRestore.ToMutatorSelections(dto.Mutators);

    // 실제 저장 경로와 같은 직렬화기를 태운다 - 자체 왕복 헬퍼를 쓰면 SaveJson의 설정 차이를 놓친다.
    private static T RoundTrip<T>(T value) where T : class
    {
        Assert.That(SaveJson.TrySerialize(value, out string json, out string serializeError), Is.True, serializeError);
        Assert.That(SaveJson.TryParseObject(json, out JObject root, out string parseError), Is.True, parseError);
        Assert.That(SaveJson.TryToObject(root, out T restored, out string bindError), Is.True, bindError);

        return restored;
    }

    private static RunModifierSnapshot ResolveSnapshot(
        RunMutatorCatalogSO catalog,
        List<(string Id, int Tier)> requested) =>
        RunModifierResolver.Resolve(ResolveSelections(catalog, requested));

    private static List<RunMutatorSelection> ResolveSelections(
        RunMutatorCatalogSO catalog,
        List<(string Id, int Tier)> requested)
    {
        Assert.That(catalog.Resolve(requested, out List<RunMutatorSelection> selections), Is.True);
        return selections;
    }

    // 카탈로그에는 Configure가 없어 리플렉션으로 넣는다(RunMutatorCatalogTests와 같은 방식).
    private RunMutatorCatalogSO CreateCatalog(params RunMutatorSO[] mutators)
    {
        RunMutatorCatalogSO catalog = Create<RunMutatorCatalogSO>();
        typeof(RunMutatorCatalogSO)
            .GetField("_mutators", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            .SetValue(catalog, mutators);
        return catalog;
    }

    // 단계마다 다른 배율을 주어, 단계가 뒤바뀐 복원이 스냅샷 비교에서 반드시 드러나게 한다.
    private RunMutatorSO CreateTieredMultiplierMutator(string id, RunModifierChannel channel)
    {
        var multipliers = new[] { 0.9f, 0.75f, 0.6f };
        var tiers = new RunMutatorTier[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            RunMultiplierEffectSO effect = Create<RunMultiplierEffectSO>();
            effect.Configure(channel, multipliers[i]);

            var tier = new RunMutatorTier();
            tier.Configure(SCORE_PER_TIER * (i + 1), $"{id}_tier{i + 1}_desc", new RunMutatorEffectSO[] { effect });
            tiers[i] = tier;
        }

        RunMutatorSO mutator = Create<RunMutatorSO>();
        mutator.Configure(id, $"{id}_name", string.Empty, tiers);
        return mutator;
    }

    private RunMutatorSO CreateRuleMutator(string id, RunRuleFlag rules)
    {
        RunRuleEffectSO effect = Create<RunRuleEffectSO>();
        effect.Configure(rules);

        var tier = new RunMutatorTier();
        tier.Configure(SCORE_PER_TIER, $"{id}_tier1_desc", new RunMutatorEffectSO[] { effect });

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
