using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 새 게임 +(뮤테이터)의 데이터 에셋 전체를 코드로 저작한다.
//
// 손으로 만들지 않는 이유는 두 가지다.
//   1) enemy_ferocity / enemy_tide의 EnemyEnhancementRule은 MonsterData 참조로 대상을 지정한다 -
//      몬스터 종류가 추가될 때마다 규칙이 조용히 빠진다. 이 스크립트를 다시 돌리면 전 종류가 다시 채워진다.
//   2) 13종의 단계 점수 합계를 손으로 맞추면 반드시 틀린다.
//      여기서는 저작 직후 합계를 검산하고 어긋나면 에러를 남긴다.
//
// 수치 출처: Docs/새게임플러스_설계.md §6.0(점수 체계) / §6.1(수치 계열) / §6.2(규칙 계열).
//
// 에디터 전용 코드이므로 CLAUDE.md 커밋규칙 3·4(문자열·리터럴 상수 금지)의 예외에 해당한다.
public static class RunMutatorAssetAuthoring
{
    private const string MUTATOR_FOLDER = "Assets/Data/Mutators";
    private const string ENEMY_PROFILE_FOLDER = "Assets/Data/Mutators/EnemyProfiles";
    private const string CATALOG_PATH = "Assets/Data/Mutators/RMC_RunMutatorCatalog.asset";

    // id는 세이브에 기록되므로 절대 변경 금지다. 설계서 §6.1·§6.2의 값 그대로다.
    private const string ID_ENEMY_FEROCITY = "enemy_ferocity";
    private const string ID_ENEMY_TIDE = "enemy_tide";
    private const string ID_WEAK_GROUND = "weak_ground";
    private const string ID_LEAN_HARVEST = "lean_harvest";
    private const string ID_BIG_APPETITE = "big_appetite";
    private const string ID_EMPTY_HANDS = "empty_hands";
    private const string ID_HARSH_WINTER = "harsh_winter";
    private const string ID_ROCKFALL = "rockfall";
    private const string ID_NO_MORNING_RESTORE = "no_morning_restore";
    private const string ID_LAST_STAND = "last_stand";
    private const string ID_IRONMAN = "ironman";
    private const string ID_SWORN_ELEMENT = "sworn_element";
    private const string ID_HEAVY_GRAVITY = "heavy_gravity";

    // 인스펙터의 multiplierBonus는 "1 + 값"으로 쓰인다(EnemyStatModifier.Multiplier) -
    // 0.2 = 체력·공격력 +20%.
    private static readonly float[] FEROCITY_BONUS = { 0.2f, 0.4f, 0.7f };
    private static readonly int[] FEROCITY_SCORE = { 3, 6, 9 };

    private static readonly int[] TIDE_SPAWN_BONUS = { 1, 3, 5 };
    private static readonly float[] TIDE_INTERVAL_BONUS = { -0.1f, -0.25f, -0.4f };
    private static readonly int[] TIDE_SCORE = { 3, 6, 9 };

    private static readonly float[] WEAK_GROUND_MULTIPLIER = { 0.8f, 0.6f, 0.4f };
    private static readonly int[] WEAK_GROUND_SCORE = { 2, 4, 6 };

    // 흉년·대식가는 점수 모델상 독립이지만, 실제로는 "인구 중 몇 %가 식량 생산에 묶이는가"라는
    // 하나의 합성량(대식가 배율 ÷ 흉년 배율)으로 곱해진다. 종전 값(0.6 / 3)은 이 비가 표준 대비
    // 5배가 되어 최고 단계를 함께 켜면 인구 전원을 농장에 넣어도 유지비를 못 채웠다(심연 65점 완주 불가).
    // 두 뮤테이터에 나눠 완화해 각자의 3단계 계단은 유지하면서 합성 배율만 낮춘다(약 2.1배로).
    private static readonly float[] LEAN_HARVEST_MULTIPLIER = { 0.9f, 0.8f, 0.7f };
    private static readonly int[] LEAN_HARVEST_SCORE = { 2, 4, 6 };

    private static readonly float[] BIG_APPETITE_MULTIPLIER = { 1.2f, 1.35f, 1.5f };
    private static readonly int[] BIG_APPETITE_SCORE = { 2, 4, 6 };

    private static readonly float[] EMPTY_HANDS_MULTIPLIER = { 0.75f, 0.5f, 0.25f };
    private static readonly int[] EMPTY_HANDS_SCORE = { 1, 2, 3 };

    private static readonly float[] HARSH_WINTER_SCALE = { 2f, 3f, 5f };
    private static readonly int[] HARSH_WINTER_SCORE = { 1, 2, 3 };

    private static readonly float[] ROCKFALL_SCALE = { 1.5f, 2f, 3f };
    private static readonly int[] ROCKFALL_SCORE = { 1, 2, 3 };

    private static readonly float[] LAST_STAND_MULTIPLIER = { 0.5f };
    private static readonly int[] LAST_STAND_SCORE = { 5 };

    private const int NO_MORNING_RESTORE_SCORE = 5;
    private const int IRONMAN_SCORE = 5;
    private const int SWORN_ELEMENT_SCORE = 3;
    private const int SWORN_ELEMENT_LIMIT_PER_CYCLE = 1;
    private const int HEAVY_GRAVITY_SCORE = 2;
    private const int HEAVY_GRAVITY_EXTRA_DAYS = 1;

    // 설계서 §6.0의 검산값. 저작 결과가 이것과 다르면 표가 아니라 실제 합계를 따르고 보고한다.
    private const int EXPECTED_NUMERIC_TOTAL = 45;
    private const int EXPECTED_RULE_TOTAL = 20;

    [MenuItem("Tools/NewGamePlus/뮤테이터 에셋 일괄 저작 (13종)")]
    public static void Execute()
    {
        EnsureFolder(MUTATOR_FOLDER);
        EnsureFolder(ENEMY_PROFILE_FOLDER);

        MonsterData[] monsters = LoadAllMonsterData();

        if (monsters.Length == 0)
        {
            Debug.LogError("[RunMutatorAssetAuthoring] MonsterData 에셋을 찾지 못했습니다 - 적 강화 프로필을 만들 수 없습니다.");
            return;
        }

        // 수치 계열 8종 - 설계서 §6.1 표 순서.
        List<RunMutatorSO> numeric = new List<RunMutatorSO>
        {
            CreateEnemyFerocity(monsters),
            CreateEnemyTide(monsters),
            CreateMultiplierMutator(
                ID_WEAK_GROUND, RunModifierChannel.TowerMaxHealth, WEAK_GROUND_MULTIPLIER, WEAK_GROUND_SCORE),
            CreateMultiplierMutator(
                ID_LEAN_HARVEST, RunModifierChannel.ChunkYield, LEAN_HARVEST_MULTIPLIER, LEAN_HARVEST_SCORE),
            CreateMultiplierMutator(
                ID_BIG_APPETITE, RunModifierChannel.FoodUpkeep, BIG_APPETITE_MULTIPLIER, BIG_APPETITE_SCORE),
            CreateMultiplierMutator(
                ID_EMPTY_HANDS, RunModifierChannel.StartingResource, EMPTY_HANDS_MULTIPLIER, EMPTY_HANDS_SCORE),
            CreateTerrainPenaltyMutator(
                ID_HARSH_WINTER, TerrainType.Snow, TerrainPenaltyKind.WoodUpkeep,
                HARSH_WINTER_SCALE, HARSH_WINTER_SCORE),
            CreateTerrainPenaltyMutator(
                ID_ROCKFALL, TerrainType.Rock, TerrainPenaltyKind.StoneUpkeep,
                ROCKFALL_SCALE, ROCKFALL_SCORE),
        };

        // 규칙 계열 5종 - 설계서 §6.2 표 순서.
        List<RunMutatorSO> rules = new List<RunMutatorSO>
        {
            CreateRuleMutator(ID_NO_MORNING_RESTORE, RunRuleFlag.NoMorningRestore, NO_MORNING_RESTORE_SCORE),
            CreateMultiplierMutator(
                ID_LAST_STAND, RunModifierChannel.CastleMaxHealth, LAST_STAND_MULTIPLIER, LAST_STAND_SCORE),
            CreateRuleMutator(ID_IRONMAN, RunRuleFlag.Ironman, IRONMAN_SCORE),
            CreateCounterMutator(
                ID_SWORN_ELEMENT, RunCounterChannel.DragonTypeChangeLimitPerCycle,
                SWORN_ELEMENT_LIMIT_PER_CYCLE, SWORN_ELEMENT_SCORE),
            CreateCounterMutator(
                ID_HEAVY_GRAVITY, RunCounterChannel.EggHatchDays,
                HEAVY_GRAVITY_EXTRA_DAYS, HEAVY_GRAVITY_SCORE),
        };

        List<RunMutatorSO> all = new List<RunMutatorSO>(numeric);
        all.AddRange(rules);

        RegisterInCatalog(all);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ReportScores(numeric, rules, all, monsters.Length);
    }

    // 점수 검산. 설계서 표와 다르면 표가 아니라 실제 합계를 따르고 그 사실을 남긴다.
    private static void ReportScores(
        List<RunMutatorSO> numeric, List<RunMutatorSO> rules, List<RunMutatorSO> all, int monsterCount)
    {
        int numericTotal = numeric.Sum(TopTierScore);
        int ruleTotal = rules.Sum(TopTierScore);

        Debug.Log($"[RunMutatorAssetAuthoring] 뮤테이터 {all.Count}종 저작 완료 (몬스터 {monsterCount}종에 규칙 적용). " +
            $"수치 {numericTotal} + 규칙 {ruleTotal} = {numericTotal + ruleTotal}점");

        if (numericTotal != EXPECTED_NUMERIC_TOTAL || ruleTotal != EXPECTED_RULE_TOTAL)
        {
            Debug.LogError("[RunMutatorAssetAuthoring] 점수 합계가 설계서 §6.0과 다릅니다 - " +
                $"기대 {EXPECTED_NUMERIC_TOTAL}+{EXPECTED_RULE_TOTAL}, 실제 {numericTotal}+{ruleTotal}.");
        }
    }

    private static MonsterData[] LoadAllMonsterData()
    {
        return AssetDatabase.FindAssets("t:MonsterData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<MonsterData>)
            .Where(data => data != null)
            .ToArray();
    }

    private static RunMutatorSO CreateEnemyFerocity(MonsterData[] monsters)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[FEROCITY_BONUS.Length];

        for (int i = 0; i < FEROCITY_BONUS.Length; i++)
        {
            int tier = i + 1;
            EnemyEnhancementProfileSO profile = CreateEnemyProfile(
                $"EEP_{ID_ENEMY_FEROCITY}_tier{tier}",
                monsters,
                healthBonus: FEROCITY_BONUS[i],
                attackBonus: FEROCITY_BONUS[i],
                spawnCountBonus: 0,
                spawnIntervalBonus: 0f);

            tiers[i] = MakeTier(FEROCITY_SCORE[i], ID_ENEMY_FEROCITY, tier, MakeEnemyProfileEffect(profile));
        }

        return SaveMutator(ID_ENEMY_FEROCITY, tiers);
    }

    private static RunMutatorSO CreateEnemyTide(MonsterData[] monsters)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[TIDE_SPAWN_BONUS.Length];

        for (int i = 0; i < TIDE_SPAWN_BONUS.Length; i++)
        {
            int tier = i + 1;
            EnemyEnhancementProfileSO profile = CreateEnemyProfile(
                $"EEP_{ID_ENEMY_TIDE}_tier{tier}",
                monsters,
                healthBonus: 0f,
                attackBonus: 0f,
                spawnCountBonus: TIDE_SPAWN_BONUS[i],
                spawnIntervalBonus: TIDE_INTERVAL_BONUS[i]);

            tiers[i] = MakeTier(TIDE_SCORE[i], ID_ENEMY_TIDE, tier, MakeEnemyProfileEffect(profile));
        }

        return SaveMutator(ID_ENEMY_TIDE, tiers);
    }

    private static RunMutatorSO CreateMultiplierMutator(
        string id, RunModifierChannel channel, float[] multipliers, int[] scores)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            RunMultiplierEffectSO effect = ScriptableObject.CreateInstance<RunMultiplierEffectSO>();
            effect.name = EffectName(id, i + 1, multipliers.Length);
            effect.Configure(channel, multipliers[i]);
            tiers[i] = MakeTier(scores[i], id, i + 1, effect);
        }

        return SaveMutator(id, tiers);
    }

    private static RunMutatorSO CreateTerrainPenaltyMutator(
        string id, TerrainType terrain, TerrainPenaltyKind kind, float[] scales, int[] scores)
    {
        RunMutatorTier[] tiers = new RunMutatorTier[scales.Length];

        for (int i = 0; i < scales.Length; i++)
        {
            RunTerrainPenaltyEffectSO effect = ScriptableObject.CreateInstance<RunTerrainPenaltyEffectSO>();
            effect.name = EffectName(id, i + 1, scales.Length);
            effect.Configure(terrain, kind, scales[i]);
            tiers[i] = MakeTier(scores[i], id, i + 1, effect);
        }

        return SaveMutator(id, tiers);
    }

    private static RunMutatorSO CreateRuleMutator(string id, RunRuleFlag flag, int score)
    {
        RunRuleEffectSO effect = ScriptableObject.CreateInstance<RunRuleEffectSO>();
        effect.name = $"RME_{id}";
        effect.Configure(flag);

        return SaveMutator(id, new[] { MakeTier(score, id, 1, effect) });
    }

    private static RunMutatorSO CreateCounterMutator(
        string id, RunCounterChannel channel, int delta, int score)
    {
        RunCounterEffectSO effect = ScriptableObject.CreateInstance<RunCounterEffectSO>();
        effect.name = $"RME_{id}";
        effect.Configure(channel, delta);

        return SaveMutator(id, new[] { MakeTier(score, id, 1, effect) });
    }

    // 단계가 하나뿐인 뮤테이터는 이름에 tier를 붙이지 않는다 - 기존 에셋(RME_last_stand)의 이름을 유지한다.
    private static string EffectName(string id, int tier, int tierCount)
    {
        return tierCount == 1 ? $"RME_{id}" : $"RME_{id}_tier{tier}";
    }

    private static RunEnemyProfileEffectSO MakeEnemyProfileEffect(EnemyEnhancementProfileSO profile)
    {
        RunEnemyProfileEffectSO effect = ScriptableObject.CreateInstance<RunEnemyProfileEffectSO>();
        effect.name = $"RME_{profile.name}";
        effect.Configure(profile);
        return effect;
    }

    private static RunMutatorTier MakeTier(int score, string id, int tier, RunMutatorEffectSO effect)
    {
        RunMutatorTier result = new RunMutatorTier();
        result.Configure(score, $"mutator_{id}_tier{tier}_desc", new[] { effect });
        return result;
    }

    // 효과 SO는 뮤테이터 에셋의 하위 에셋으로 넣는다 - 한 뮤테이터의 밸런스가 파일 하나에 모인다.
    private static RunMutatorSO SaveMutator(string id, RunMutatorTier[] tiers)
    {
        string path = $"{MUTATOR_FOLDER}/RM_{id}.asset";
        RunMutatorSO mutator = AssetDatabase.LoadAssetAtPath<RunMutatorSO>(path);
        bool isNew = mutator == null;

        if (isNew)
        {
            mutator = ScriptableObject.CreateInstance<RunMutatorSO>();
        }
        else
        {
            // 다시 돌릴 때 옛 효과 하위 에셋이 고아로 남지 않게 먼저 걷어낸다.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is RunMutatorEffectSO)
                {
                    AssetDatabase.RemoveObjectFromAsset(sub);
                    Object.DestroyImmediate(sub, true);
                }
            }
        }

        // ExclusiveGroup은 전부 비워 둔다 - 상호배타로 막으면 "전부 켜기" 조합을 만들 수 없어
        // 최고 난이도(65점)가 사라진다. 경제 조합 위험은 식량 하한 보정으로 처리한다.
        mutator.Configure(id, $"mutator_{id}_name", string.Empty, tiers);

        if (isNew)
        {
            AssetDatabase.CreateAsset(mutator, path);
        }

        foreach (RunMutatorTier tier in tiers)
        {
            foreach (RunMutatorEffectSO effect in tier.Effects)
            {
                AssetDatabase.AddObjectToAsset(effect, mutator);
            }
        }

        EditorUtility.SetDirty(mutator);
        AssetDatabase.SaveAssetIfDirty(mutator);
        return mutator;
    }

    // Configure가 없는 타입이라 SerializedObject로 저작한다.
    private static EnemyEnhancementProfileSO CreateEnemyProfile(
        string assetName,
        MonsterData[] monsters,
        float healthBonus,
        float attackBonus,
        int spawnCountBonus,
        float spawnIntervalBonus)
    {
        string path = $"{ENEMY_PROFILE_FOLDER}/{assetName}.asset";
        EnemyEnhancementProfileSO profile = AssetDatabase.LoadAssetAtPath<EnemyEnhancementProfileSO>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EnemyEnhancementProfileSO>();
            AssetDatabase.CreateAsset(profile, path);
        }

        SerializedObject serialized = new SerializedObject(profile);
        SerializedProperty rules = serialized.FindProperty("_rules");
        rules.ClearArray();

        for (int i = 0; i < monsters.Length; i++)
        {
            rules.InsertArrayElementAtIndex(i);
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            rule.FindPropertyRelative("_targetMonster").objectReferenceValue = monsters[i];
            rule.FindPropertyRelative("_spawnCountBonus").intValue = spawnCountBonus;
            SetModifier(rule, "_maxHealth", healthBonus);
            SetModifier(rule, "_attackPower", attackBonus);
            SetModifier(rule, "_shieldAmount", 0f);

            // MoveSpeed는 쓰지 않는다 - BaseMonster.Setup이 적용하지 않는데 툴팁은 적용해 표시하므로
            // 값을 넣으면 미리보기와 실제 이동속도가 어긋난다(기존 불일치, 별도 보고 항목).
            SetModifier(rule, "_moveSpeed", 0f);
            SetModifier(rule, "_spawnInterval", spawnIntervalBonus);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    private static void SetModifier(SerializedProperty rule, string fieldName, float multiplierBonus)
    {
        SerializedProperty modifier = rule.FindPropertyRelative(fieldName);
        modifier.FindPropertyRelative("_additiveBonus").floatValue = 0f;
        modifier.FindPropertyRelative("_multiplierBonus").floatValue = multiplierBonus;
    }

    // 목록을 통째로 다시 쓴다 - 이어 붙이기만 하면 순서가 실행 이력에 따라 달라진다.
    // 프리셋(_presets)은 더 이상 이 도구가 저작하지 않는다 - 항상 빈 배열로 정리해 둔다
    // (예시 조합 시스템 제거. 자유 조합만 남는다).
    private static void RegisterInCatalog(List<RunMutatorSO> ordered)
    {
        RunMutatorCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunMutatorCatalogSO>(CATALOG_PATH);

        if (catalog == null)
        {
            Debug.LogError($"[RunMutatorAssetAuthoring] 카탈로그를 찾지 못했습니다: {CATALOG_PATH}");
            return;
        }

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty mutators = serialized.FindProperty("_mutators");
        mutators.ClearArray();

        for (int i = 0; i < ordered.Count; i++)
        {
            mutators.InsertArrayElementAtIndex(i);
            mutators.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
        }

        serialized.FindProperty("_presets").ClearArray();

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    // 최고 단계 점수만 더한다 - 한 뮤테이터는 단계 하나만 켤 수 있으므로 이것이 이론상 최대치다.
    private static int TopTierScore(RunMutatorSO mutator)
    {
        int score = 0;

        foreach (RunMutatorTier tier in mutator.Tiers)
        {
            score = Mathf.Max(score, tier.DifficultyScore);
        }

        return score;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int lastSlash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, lastSlash), path.Substring(lastSlash + 1));
    }
}
