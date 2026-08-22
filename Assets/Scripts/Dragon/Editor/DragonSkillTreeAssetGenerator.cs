using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using UnityEditor;
using UnityEngine;

// 용 스킬트리 SO 에셋(노드·효과·상태이상·게이트·스킬·트리)을 코드로 생성한다.
// 손으로 .asset YAML을 작성하지 않기 위한 에디터 전용 도구 - CLAUDE.md 커밋규칙 §5(에디터 전용 코드)
// 리터럴 제한 예외에 해당한다. 재실행 시 기존 에셋을 재사용(GUID 유지)해 멱등적으로 동작한다.
//
// === 구조(v3 빌드트리) ===
// 속성 5 × 슬롯 8, 슬롯마다 랭크만큼 노드를 체인해 총 70개를 만든다.
// 랭크를 ProgressionNodeData의 필드가 아니라 "별도 노드 + 직전 랭크를 선행"으로 표현하는 이유:
// 해금 상태가 문자열 id의 HashSet이라, 이렇게 하면 ProgressionRules·세이브·연구 트리와 공유하는
// 코어를 전혀 건드리지 않는다. 효과 합산도 DragonTreeManager.Aggregate가 이미 "1 + Σ보너스"라
// 랭크별 효과 에셋을 따로 두기만 하면 누적이 자동으로 성립한다.
//
// === 빌드 다양성 장치 2가지 ===
// 1) 궁극 = "비활성 잔존". 루트가 준 기본 패시브와 같은 종류의 효과를
//    (_suppressWhileActive=true, _inactiveScale=0.5)로 하나 더 붙인다.
//    활성 중에는 기본 패시브와 이중 계산되지 않고, 다른 속성으로 갈아타도 절반이 남는다.
//    → "속성 몇 개를 동시에 굴릴 것인가"라는 축이 생긴다.
// 2) 궁극 게이트 = OR 두 경로. (그 속성 새끼용 랭크 4) 또는 (양갈래 완주).
//    → 알이 안 나온 플레이어도 자력으로 궁극에 도달할 수 있고, 두 개의 다른 빌드가 생긴다.
public static class DragonSkillTreeAssetGenerator
{
    private const string DATA_FOLDER = "Assets/Data/Dragon";
    private const string SKILL_SUBFOLDER = "Skills";
    private const string STATUS_SUBFOLDER = "Status";
    private const string SHEET_EXPORT_FOLDER = "Docs";
    private const string BARRICADE_PREFAB_PATH = "Assets/Prefabs/Building/StoneBarricade.prefab";

    // 슬롯당 랭크 - 2랭크로 두고 개당 효과를 키운다(3랭크 × 소폭%는 클릭만 늘고 결정이 안 된다).
    private const int RANK_SINGLE = 1;
    private const int RANK_BRANCH = 2;
    private const int RANK_KIN = 2;

    // 비용 곡선은 일부러 가파르다 - 선형이면 마지막 랭크가 항상 이득이라 저울질이 사라진다.
    private static readonly int[] UNLOCK_SLIME_COST = { 5 };
    private static readonly int[] UNLOCK_SPECIAL_COST = { 0 };
    private static readonly int[] BRANCH_SLIME_COST = { 6, 13 };
    private static readonly int[] BRANCH_SPECIAL_COST = { 2, 5 };
    private static readonly int[] ULTIMATE_SLIME_COST = { 20 };
    private static readonly int[] ULTIMATE_SPECIAL_COST = { 8 };
    private static readonly int[] KIN_SLIME_COST = { 5, 11 };
    private static readonly int[] KIN_SPECIAL_COST = { 2, 4 };

    // 심화 게이트 = 그 속성 새끼용 노드 2개. 궁극의 새끼용 경로 = 4개(kinA·kinB 둘 다 최대).
    private const int DEEP_REQUIRED_KIN_COUNT = 2;
    private const int ULTIMATE_REQUIRED_KIN_COUNT = 4;

    // 궁극의 비활성 잔존 비율 - 다른 속성을 켜 두어도 이만큼은 남는다.
    private const float ULTIMATE_INACTIVE_SCALE = 0.5f;

    // 랭크별 효과 수치(예시, 밸런싱 대상). 인덱스 = 랭크 - 1.
    private static readonly float[] YIELD_BONUS_PER_RANK = { 0.12f, 0.12f };
    private static readonly float[] TOWER_STAT_BONUS_PER_RANK = { 0.1f, 0.1f };
    private static readonly float[] SKILL_POWER_BONUS_PER_RANK = { 0.2f, 0.2f };
    private static readonly float[] SKILL_COOLDOWN_REDUCTION_PER_RANK = { 0.12f, 0.12f };
    private static readonly float[] KIN_TOWER_STAT_BONUS_PER_RANK = { 0.12f, 0.12f };
    private static readonly float[] KIN_AREA_YIELD_BONUS_PER_RANK = { 0.15f, 0.15f };
    private static readonly float[] KIN_BUFF_RADIUS_BONUS_PER_RANK = { 0.2f, 0.2f };
    private static readonly float[] BARRICADE_HEALTH_BONUS_PER_RANK = { 0.5f, 0.5f };

    // 각성 패시브 기본 수치(예시, 밸런싱 대상).
    private const float AWAKEN_YIELD_BONUS = 0.2f;
    private const float AWAKEN_ATTACK_SPEED_BONUS = 0.15f;

    // 궁극의 속성별 추가 효과 수치.
    private const float ULTIMATE_SKILL_POWER_BONUS = 0.3f;
    private const float ULTIMATE_SKILL_COOLDOWN_REDUCTION = 0.2f;
    private const int ULTIMATE_EXTRA_USE_PER_DAY = 1;
    private const int ULTIMATE_CONQUEST_DAYS_REDUCTION = 1;

    // 상태이상 수치(예시, 밸런싱 대상). 빙결은 기본 3초에서 랭크마다 1초씩 증가한다.
    private const float ICE_SLOW_MULTIPLIER = 0.5f;

    // 궁극의 잔존분은 "절반 세기". 상태이상은 런타임 배율을 곱할 수 없어(DragonSkillStatusEffectSO
    // 주석 참고) 약화된 에셋을 따로 만들어 표현한다. 감속 폭(1 - 배율)을 잔존 비율만큼 줄인다 -
    // 0.5 감속(속도 50%)의 절반은 0.25 감속이므로 배율 0.75가 된다.
    private const float ICE_SLOW_MULTIPLIER_PERSIST =
        1f - (1f - ICE_SLOW_MULTIPLIER) * ULTIMATE_INACTIVE_SCALE;
    private const float ICE_SLOW_DURATION = 3f;
    private const float ICE_FREEZE_DURATION = 3f;
    private const float ICE_FREEZE_DURATION_R1 = 4f;
    private const float ICE_FREEZE_DURATION_R2 = 5f;
    private const float FIRE_BURN_DAMAGE_PER_TICK = 2f;
    private const float FIRE_BURN_DAMAGE_PER_TICK_R2 = 4f;

    // 화상은 틱 피해가 곧 세기라 잔존 비율을 그대로 곱한다(ICE_SLOW_MULTIPLIER_PERSIST 주석 참고).
    private const float FIRE_BURN_DAMAGE_PER_TICK_PERSIST =
        FIRE_BURN_DAMAGE_PER_TICK * ULTIMATE_INACTIVE_SCALE;

    // 새끼용 불 화상은 어미용 상시 화상과 StatusId를 나누고 지속시간도 유한하게 둔다 -
    // 같은 id를 쓰면 두 화상이 중첩되지 않고 서로 갱신해 버려서, 어미용 불이 활성인 동안
    // 새끼용 타워형 노드를 해금해도 체감이 전혀 없다.
    private const string KIN_FIRE_BURN_STATUS_ID = "kin_fire_burn";
    private const float KIN_FIRE_BURN_DURATION = 3f;
    private const float FIRE_BURN_TICK_INTERVAL = 1f;
    private const float FIRE_BURN_DURATION_INFINITE = 0f; // 상시 화상 - 몬스터가 죽을 때까지 유지.

    // 액티브 스킬 수치(예시, 밸런싱 대상).
    private const float SKILL_DEFAULT_COOLTIME = 60f;
    private const int SKILL_UNLIMITED_USE_PER_DAY = -1;
    private const int METEOR_USE_PER_DAY = 2;
    private const float GLOBAL_DAMAGE_PERCENT_OF_CURRENT_HEALTH = 0.3f;
    private const float METEOR_FLAT_DAMAGE = 40f;
    // 밸런스 테스트에서 2 → 1.5로 줄인 값. 생성기를 다시 돌려도 되돌아가지 않도록 여기 반영한다.
    private const float METEOR_AREA_RADIUS = 1.5f;
    private const float CASTLE_HEAL_AMOUNT = 50f;

    // 속성 하나의 고정 정보. 슬롯별 문구는 SlotText가 (속성, 슬롯)으로 따로 들고 있다.
    private struct AttributeSpec
    {
        public DragonType Type;
        public string Key;
        public string KoreanName;
        public ResourceType Slime;
        public ResourceType Specialized;
        public ResourceType PassiveYieldResources;
    }

    // 슬롯 하나의 구조적 정의 - 5속성이 이 표를 공유한다.
    private struct SlotSpec
    {
        public DragonNodeKind Kind;
        public string Key;
        public int MaxRank;
        public int[] SlimeCostPerRank;
        public int[] SpecialCostPerRank;
        public DragonNodeKind? PrerequisiteSlot;
        public bool NeedsDeepGate;
        public bool NeedsKinOwnedGate;
    }

    // 트리 전체가 공유하는 상태이상 에셋 묶음 - 인자 개수를 줄이려고 하나로 묶었다.
    private sealed class StatusAssets
    {
        public MoveSpeedStatusSO IceSlow;
        public FreezeStatusSO IceFreeze;
        public FreezeStatusSO IceFreezeRank1;
        public FreezeStatusSO IceFreezeStrong;
        public DamageOverTimeStatusSO FireBurn;
        public DamageOverTimeStatusSO FireBurnStrong;

        // 궁극의 잔존 전용 - 루트 각성이 주는 상태이상의 절반 세기.
        public MoveSpeedStatusSO IceSlowPersist;
        public DamageOverTimeStatusSO FireBurnPersist;

        // 새끼용 타워형(불) 전용 - 어미용 화상과 별개 id·유한 지속.
        public DamageOverTimeStatusSO KinFireBurn;
        public DamageOverTimeStatusSO KinFireBurnStrong;
    }

    private sealed class LocRow
    {
        public string Id;
        public string En;
        public string Ko;
    }

    // StringTable.cs의 Data 클래스와 동일한 컬럼(Id/String)을 읽기 위한 매핑 타입.
    private sealed class CsvRow
    {
        public string Id { get; set; }
        public string String { get; set; }
    }

    private static readonly List<LocRow> _locRows = new();

    private static AttributeSpec[] BuildAttributeSpecs() => new[]
    {
        new AttributeSpec
        {
            Type = DragonType.Ice, Key = "ice", KoreanName = "얼음",
            Slime = ResourceType.SnowSlime, Specialized = ResourceType.SnowCrystal,
            PassiveYieldResources = ResourceType.SnowSlime | ResourceType.SnowCrystal,
        },
        new AttributeSpec
        {
            Type = DragonType.Fire, Key = "fire", KoreanName = "불",
            Slime = ResourceType.VolcanoSlime, Specialized = ResourceType.FlameHeart,
            PassiveYieldResources = ResourceType.VolcanoSlime | ResourceType.FlameHeart,
        },
        new AttributeSpec
        {
            Type = DragonType.Time, Key = "time", KoreanName = "시간",
            Slime = ResourceType.DesertSlime, Specialized = ResourceType.TimeSand,
            PassiveYieldResources = ResourceType.DesertSlime | ResourceType.TimeSand,
        },
        new AttributeSpec
        {
            // "광산 생산량 증가"의 광산은 실제 채석장(RPD_Quarry, Stone 생산) - 슬라임·특화만 넣으면
            // 정작 채석장의 기본 Stone 생산이 오르지 않는다.
            Type = DragonType.Stone, Key = "stone", KoreanName = "암석",
            Slime = ResourceType.RockSlime, Specialized = ResourceType.PhilosopherStone,
            PassiveYieldResources = ResourceType.Stone | ResourceType.RockSlime | ResourceType.PhilosopherStone,
        },
        new AttributeSpec
        {
            // 마찬가지로 "농장"은 RPD_FarmField(Food 생산)다.
            Type = DragonType.Life, Key = "life", KoreanName = "생명",
            Slime = ResourceType.GrassSlime, Specialized = ResourceType.None,
            PassiveYieldResources = ResourceType.Food | ResourceType.GrassSlime,
        },
    };

    private static SlotSpec[] BuildSlotSpecs() => new[]
    {
        new SlotSpec
        {
            Kind = DragonNodeKind.ActiveUnlock, Key = "unlock", MaxRank = RANK_SINGLE,
            SlimeCostPerRank = UNLOCK_SLIME_COST, SpecialCostPerRank = UNLOCK_SPECIAL_COST,
            PrerequisiteSlot = null,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.ActiveUp1, Key = "active1", MaxRank = RANK_BRANCH,
            SlimeCostPerRank = BRANCH_SLIME_COST, SpecialCostPerRank = BRANCH_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUnlock,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.ActiveUp2, Key = "active2", MaxRank = RANK_BRANCH,
            SlimeCostPerRank = BRANCH_SLIME_COST, SpecialCostPerRank = BRANCH_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUp1, NeedsDeepGate = true,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.PassiveUp1, Key = "passive1", MaxRank = RANK_BRANCH,
            SlimeCostPerRank = BRANCH_SLIME_COST, SpecialCostPerRank = BRANCH_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUnlock,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.PassiveUp2, Key = "passive2", MaxRank = RANK_BRANCH,
            SlimeCostPerRank = BRANCH_SLIME_COST, SpecialCostPerRank = BRANCH_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.PassiveUp1, NeedsDeepGate = true,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.Ultimate, Key = "ultimate", MaxRank = RANK_SINGLE,
            SlimeCostPerRank = ULTIMATE_SLIME_COST, SpecialCostPerRank = ULTIMATE_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUnlock,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.KinTower, Key = "kin_tower", MaxRank = RANK_KIN,
            SlimeCostPerRank = KIN_SLIME_COST, SpecialCostPerRank = KIN_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUnlock, NeedsKinOwnedGate = true,
        },
        new SlotSpec
        {
            Kind = DragonNodeKind.KinArea, Key = "kin_area", MaxRank = RANK_KIN,
            SlimeCostPerRank = KIN_SLIME_COST, SpecialCostPerRank = KIN_SPECIAL_COST,
            PrerequisiteSlot = DragonNodeKind.ActiveUnlock, NeedsKinOwnedGate = true,
        },
    };

    [MenuItem("TowerAndDragon/Dragon/Generate Skill Tree Assets")]
    public static void Generate()
    {
        _locRows.Clear();
        EnsureFolder(DATA_FOLDER);
        EnsureFolder($"{DATA_FOLDER}/{SKILL_SUBFOLDER}");
        EnsureFolder($"{DATA_FOLDER}/{STATUS_SUBFOLDER}");

        AttributeSpec[] attributes = BuildAttributeSpecs();
        SlotSpec[] slots = BuildSlotSpecs();

        StatusAssets statuses = CreateStatusAssets();
        Dictionary<DragonType, SkillSO> skillByAttribute = CreateSkills(statuses);

        // 1. 게이트 - 새끼용 보유(속성별) + 심화/궁극 새끼용 랭크(속성별)
        var kinOwnedGates = new Dictionary<DragonType, KinOwnedGateSO>();
        var deepGates = new Dictionary<DragonType, KinCountGateSO>();
        var ultimateKinGates = new Dictionary<DragonType, KinCountGateSO>();

        foreach (AttributeSpec attr in attributes)
        {
            kinOwnedGates[attr.Type] = CreateOrReplace<KinOwnedGateSO>(
                $"{DATA_FOLDER}/DG_KinOwned_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_lockedLocKey").stringValue = $"dragon_gate_kin_owned_{attr.Key}";
                });

            deepGates[attr.Type] = CreateKinCountGate(
                $"{DATA_FOLDER}/DG_KinRank_Deep_{Capitalize(attr.Key)}.asset",
                attr, DEEP_REQUIRED_KIN_COUNT, "dragon_gate_kin_rank_deep");

            ultimateKinGates[attr.Type] = CreateKinCountGate(
                $"{DATA_FOLDER}/DG_KinRank_Ultimate_{Capitalize(attr.Key)}.asset",
                attr, ULTIMATE_REQUIRED_KIN_COUNT, "dragon_gate_kin_rank_ultimate");

            AddLocRow($"dragon_gate_kin_owned_{attr.Key}",
                $"[TBD] Locked: requires a {attr.Key} baby dragon",
                $"[미정] 잠김: {attr.KoreanName} 새끼용을 보유해야 합니다");

            AddLocRow($"dragon_attribute_{attr.Key}", $"[TBD] {Capitalize(attr.Key)}", attr.KoreanName);
        }

        AddLocRow("dragon_gate_kin_rank_deep",
            "[TBD] Locked: strengthen this attribute's baby dragon nodes",
            "[미정] 잠김: 이 속성의 새끼용 노드를 더 강화해야 합니다");
        AddLocRow("dragon_gate_kin_rank_ultimate",
            "[TBD] Locked: max this attribute's baby dragon nodes, or complete both branches",
            "[미정] 잠김: 이 속성 새끼용을 최대로 강화하거나, 두 갈래를 모두 완주해야 합니다");

        // 2. 노드 - 속성 5 × 슬롯 8, 슬롯마다 랭크 체인
        var nodesBySlot = new Dictionary<(DragonType, DragonNodeKind, int), DragonSkillNodeData>();
        var orderedNodes = new List<DragonSkillNodeData>();

        foreach (AttributeSpec attr in attributes)
        {
            foreach (SlotSpec slot in slots)
            {
                for (int rank = 1; rank <= slot.MaxRank; rank++)
                {
                    DragonSkillNodeData prerequisite = ResolvePrerequisite(nodesBySlot, slots, attr.Type, slot, rank);

                    var gates = new List<ProgressionGateSO>();

                    if (slot.NeedsDeepGate)
                    {
                        gates.Add(deepGates[attr.Type]);
                    }

                    if (slot.NeedsKinOwnedGate)
                    {
                        gates.Add(kinOwnedGates[attr.Type]);
                    }

                    DragonSkillEffectSO[] effects = CreateSlotEffects(attr, slot, rank, statuses, skillByAttribute);
                    DragonSkillNodeData node = CreateNode(attr, slot, rank, prerequisite, gates.ToArray(), effects);

                    nodesBySlot[(attr.Type, slot.Kind, rank)] = node;
                    orderedNodes.Add(node);
                }
            }
        }

        // 3. 궁극 게이트(2패스) - 참조할 양갈래 최종랭크 노드가 만들어진 뒤에야 배선할 수 있다.
        foreach (AttributeSpec attr in attributes)
        {
            NodesUnlockedGateSO bothBranchesGate = CreateOrReplace<NodesUnlockedGateSO>(
                $"{DATA_FOLDER}/DG_BothBranches_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_requireAll").boolValue = true;
                    so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_rank_ultimate";
                    AssignObjectArray(so.FindProperty("_nodes"), new UnityEngine.Object[]
                    {
                        nodesBySlot[(attr.Type, DragonNodeKind.ActiveUp2, RANK_BRANCH)],
                        nodesBySlot[(attr.Type, DragonNodeKind.PassiveUp2, RANK_BRANCH)],
                    });
                });

            AnyOfGatesSO ultimateGate = CreateOrReplace<AnyOfGatesSO>(
                $"{DATA_FOLDER}/DG_UltimatePath_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_rank_ultimate";
                    AssignObjectArray(so.FindProperty("_options"), new UnityEngine.Object[]
                    {
                        ultimateKinGates[attr.Type],
                        bothBranchesGate,
                    });
                });

            DragonSkillNodeData ultimateNode = nodesBySlot[(attr.Type, DragonNodeKind.Ultimate, RANK_SINGLE)];
            var ultimateSo = new SerializedObject(ultimateNode);
            AssignObjectArray(ultimateSo.FindProperty("_gates"), new UnityEngine.Object[] { ultimateGate });
            ultimateSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ultimateNode);
        }

        // 4. 트리
        CreateOrReplace<DragonSkillTreeData>(
            $"{DATA_FOLDER}/DragonSkillTree.asset",
            so =>
            {
                SerializedProperty nodesProp = so.FindProperty("_nodes");
                nodesProp.arraySize = orderedNodes.Count;
                for (int i = 0; i < orderedNodes.Count; i++)
                {
                    nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = orderedNodes[i];
                }
            });

        AddCommonLocRows();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AppendLocalizationCsv();
        WriteSheetExportFragments();

        Debug.Log(
            $"[DragonSkillTreeAssetGenerator] 노드 {orderedNodes.Count}개, 로컬 키 {_locRows.Count}개 생성 완료. " +
            $"'{SHEET_EXPORT_FOLDER}/' 아래 시트 반영용 CSV 조각을 확인하세요.");
    }

    // ---------------------------------------------------------------- 상태이상 · 스킬

    private static StatusAssets CreateStatusAssets()
    {
        return new StatusAssets
        {
            IceSlow = CreateMoveSpeedStatus("DS_IceSlow", "dragon_ice_slow", ICE_SLOW_DURATION, ICE_SLOW_MULTIPLIER),
            IceFreeze = CreateFreezeStatus("DS_IceFreeze", "dragon_ice_freeze", ICE_FREEZE_DURATION),
            IceFreezeRank1 = CreateFreezeStatus("DS_IceFreeze_R1", "dragon_ice_freeze", ICE_FREEZE_DURATION_R1),

            // 랭크 상태의 StatusId를 같게 둬야 재부여가 중첩이 아니라 갱신으로 처리된다.
            IceFreezeStrong = CreateFreezeStatus("DS_IceFreeze_R2", "dragon_ice_freeze", ICE_FREEZE_DURATION_R2),

            FireBurn = CreateDotStatus(
                "DS_FireBurn", "dragon_fire_burn", FIRE_BURN_DAMAGE_PER_TICK, FIRE_BURN_DURATION_INFINITE),
            FireBurnStrong = CreateDotStatus(
                "DS_FireBurn_R2", "dragon_fire_burn", FIRE_BURN_DAMAGE_PER_TICK_R2, FIRE_BURN_DURATION_INFINITE),

            // 잔존용도 StatusId를 루트와 같게 둔다 - 속성을 바꾸는 순간 이미 걸려 있던 상태가
            // 중첩되지 않고 약한 쪽으로 갱신된다(랭크 상태와 같은 이유).
            IceSlowPersist = CreateMoveSpeedStatus(
                "DS_IceSlow_Persist", "dragon_ice_slow", ICE_SLOW_DURATION, ICE_SLOW_MULTIPLIER_PERSIST),
            FireBurnPersist = CreateDotStatus(
                "DS_FireBurn_Persist", "dragon_fire_burn", FIRE_BURN_DAMAGE_PER_TICK_PERSIST,
                FIRE_BURN_DURATION_INFINITE),

            KinFireBurn = CreateDotStatus(
                "DS_KinFireBurn_R1", KIN_FIRE_BURN_STATUS_ID, FIRE_BURN_DAMAGE_PER_TICK, KIN_FIRE_BURN_DURATION),
            KinFireBurnStrong = CreateDotStatus(
                "DS_KinFireBurn_R2", KIN_FIRE_BURN_STATUS_ID, FIRE_BURN_DAMAGE_PER_TICK_R2, KIN_FIRE_BURN_DURATION),
        };
    }

    // 빙결은 MoveSpeedStatusSO(배율 0)가 아니라 FreezeStatusSO여야 한다 - MonsterStatusReceiver의
    // IsActionBlocked가 빙결 엔트리만 보고 판정하므로, 배율 0짜리 이동속도 상태로는 이동만 멈추고
    // 공격은 계속한다. 이동속도 배율은 따로 두지 않는다(빙결이 하나라도 걸리면 수신부가 0으로 강제).
    //
    // 전역 빙결은 쿨다운으로 제한된 어미용 액티브 한 방이라 보스의 군중제어 면역을 관통한다 -
    // 이 플래그를 생성기에서 켜지 않으면 생성기를 다시 돌릴 때 조용히 꺼진다.
    private static FreezeStatusSO CreateFreezeStatus(string assetName, string statusId, float duration)
    {
        return CreateOrReplace<FreezeStatusSO>(
            $"{DATA_FOLDER}/{STATUS_SUBFOLDER}/{assetName}.asset",
            so =>
            {
                so.FindProperty("_statusId").stringValue = statusId;
                so.FindProperty("_durationSeconds").floatValue = duration;
                so.FindProperty("_piercesCrowdControlImmunity").boolValue = true;
            });
    }

    private static MoveSpeedStatusSO CreateMoveSpeedStatus(string assetName, string statusId, float duration, float multiplier)
    {
        return CreateOrReplace<MoveSpeedStatusSO>(
            $"{DATA_FOLDER}/{STATUS_SUBFOLDER}/{assetName}.asset",
            so =>
            {
                so.FindProperty("_statusId").stringValue = statusId;
                so.FindProperty("_durationSeconds").floatValue = duration;
                so.FindProperty("_speedMultiplier").floatValue = multiplier;
            });
    }

    private static DamageOverTimeStatusSO CreateDotStatus(
        string assetName, string statusId, float damagePerTick, float durationSeconds)
    {
        return CreateOrReplace<DamageOverTimeStatusSO>(
            $"{DATA_FOLDER}/{STATUS_SUBFOLDER}/{assetName}.asset",
            so =>
            {
                so.FindProperty("_statusId").stringValue = statusId;
                so.FindProperty("_durationSeconds").floatValue = durationSeconds;
                so.FindProperty("_damagePerTick").floatValue = damagePerTick;
                so.FindProperty("_tickIntervalSeconds").floatValue = FIRE_BURN_TICK_INTERVAL;
            });
    }

    private static Dictionary<DragonType, SkillSO> CreateSkills(StatusAssets statuses)
    {
        var skills = new Dictionary<DragonType, SkillSO>();

        skills[DragonType.Ice] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_FreezeAll.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.FREEZE_ALL;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_freeze_all_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_freeze_all_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;

                // 빙결은 이 상태이상이 없으면 아무 일도 하지 않는다(FreezeAllSkill이 AppliedStatus를 그대로 건다).
                // 액티브 강화 II 노드가 지속시간이 긴 판본으로 런타임에 교체한다.
                so.FindProperty("AppliedStatus").objectReferenceValue = statuses.IceFreeze;
            });

        skills[DragonType.Fire] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_GlobalDamage.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.GLOBAL_CURRENT_HEALTH_DAMAGE;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_global_damage_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_global_damage_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
                so.FindProperty("DamagePercentOfCurrentHealth").floatValue = GLOBAL_DAMAGE_PERCENT_OF_CURRENT_HEALTH;

                // 피해와 함께 화상을 다시 건다(GlobalCurrentHealthDamageSkill이 둘을 동시에 처리한다).
                so.FindProperty("AppliedStatus").objectReferenceValue = statuses.FireBurn;
            });

        skills[DragonType.Time] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_RepairTowers.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.REPAIR_TOWERS;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_repair_towers_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_repair_towers_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
            });

        // 메테오는 유일하게 횟수 제한이 있는 액티브다 - 암석 궁극(사용 횟수 +1)이 의미를 가지려면
        // 무제한(-1)이면 안 된다.
        skills[DragonType.Stone] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_Meteor.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.METEOR_BARRICADE;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_meteor_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_meteor_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = METEOR_USE_PER_DAY;

                // 체력 비례가 아니라 고정 데미지 - 방벽으로 시간을 버는 플레이와 맞물리게 한다.
                so.FindProperty("DamagePercentOfCurrentHealth").floatValue = 0f;
                so.FindProperty("FlatDamage").floatValue = METEOR_FLAT_DAMAGE;
                so.FindProperty("AreaRadius").floatValue = METEOR_AREA_RADIUS;
                so.FindProperty("TargetLayers").intValue = LayerMask.GetMask(Defines.ENEMY_LAYER_NAME);

                // 이 배선이 비어 있으면 MeteorBarricadeSkill이 방벽을 아예 설치하지 못한다.
                so.FindProperty("BarricadePrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(BARRICADE_PREFAB_PATH);
            });

        skills[DragonType.Life] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_CastleHeal.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.HEAL_CASTLE;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_castle_heal_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_castle_heal_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
                so.FindProperty("HealAmount").floatValue = CASTLE_HEAL_AMOUNT;
            });

        AddLocRow("dragon_skill_freeze_all_name", "[TBD] Freeze All", "[미정] 전역 빙결");
        AddLocRow("dragon_skill_freeze_all_desc", "[TBD] Freezes all enemies currently on the field.", "[미정] 현재 필드의 모든 적을 빙결시킵니다.");
        AddLocRow("dragon_skill_global_damage_name", "[TBD] Burn Surge", "[미정] 전역 화염 피해");
        AddLocRow("dragon_skill_global_damage_desc", "[TBD] Deals damage to all enemies.", "[미정] 모든 적에게 피해를 줍니다.");
        AddLocRow("dragon_skill_repair_towers_name", "[TBD] Instant Repair", "[미정] 즉시 재활성화");
        AddLocRow("dragon_skill_repair_towers_desc", "[TBD] Instantly revives all disabled towers.", "[미정] 비활성화된 모든 타워를 즉시 복구합니다.");
        AddLocRow("dragon_skill_meteor_name", "[TBD] Meteor & Barricade", "[미정] 메테오+방벽");
        AddLocRow("dragon_skill_meteor_desc",
            "[TBD] Strikes a target area and erects a barricade that blocks monster pathing.",
            "[미정] 지정 지역을 타격하고, 그 자리에 몬스터의 진로를 막는 방벽을 세웁니다.");
        AddLocRow("dragon_skill_castle_heal_name", "[TBD] Castle Mend", "[미정] 성 회복");
        AddLocRow("dragon_skill_castle_heal_desc", "[TBD] Instantly restores health to the castle.", "[미정] 성 체력을 즉시 회복합니다.");

        return skills;
    }

    // ---------------------------------------------------------------- 효과

    // 슬롯·랭크에 해당하는 효과 에셋들. 궁극만 2개(잔존 + 속성별 고유)를 돌려준다.
    private static DragonSkillEffectSO[] CreateSlotEffects(
        AttributeSpec attr,
        SlotSpec slot,
        int rank,
        StatusAssets statuses,
        Dictionary<DragonType, SkillSO> skillByAttribute)
    {
        string suffix = $"{Capitalize(attr.Key)}_{Capitalize(slot.Key)}_R{rank}";
        int index = rank - 1;

        switch (slot.Kind)
        {
            // 루트는 액티브 해금 + 그 속성의 기본 패시브를 함께 준다.
            // 기본 패시브를 어느 노드에도 붙이지 않으면 게임에 존재하지 않는 효과가 되고,
            // 궁극의 "비활성 잔존"도 가리킬 대상이 사라진다.
            case DragonNodeKind.ActiveUnlock:
                ActiveSkillUnlockEffectSO unlock = CreateOrReplace<ActiveSkillUnlockEffectSO>(
                    $"{DATA_FOLDER}/DE_{suffix}.asset",
                    so =>
                    {
                        so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                        skillByAttribute.TryGetValue(attr.Type, out SkillSO skill);
                        so.FindProperty("_skill").objectReferenceValue = skill;
                    });

                DragonSkillEffectSO passive = CreateAwakenEffect(attr, $"{DATA_FOLDER}/DE_{suffix}_Passive.asset", statuses);

                return new DragonSkillEffectSO[] { unlock, passive };

            case DragonNodeKind.ActiveUp1:
            case DragonNodeKind.ActiveUp2:
                return One(CreateActiveBranchEffect(attr, slot, suffix, index, statuses));

            case DragonNodeKind.PassiveUp1:
                return One(CreateYieldEffect($"{DATA_FOLDER}/DE_{suffix}.asset", attr, YIELD_BONUS_PER_RANK[index]));

            case DragonNodeKind.PassiveUp2:
                return One(CreatePassiveTowerEffect(attr, suffix, index));

            case DragonNodeKind.Ultimate:
                return CreateUltimateEffects(attr, suffix, statuses);

            case DragonNodeKind.KinTower:
                return One(CreateKinTowerEffect(attr, suffix, index, statuses));

            case DragonNodeKind.KinArea:
            default:
                return One(CreateKinAreaEffect(attr, suffix, index));
        }
    }

    // 액티브 갈래 - 속성마다 강화하는 파라미터가 다르다(위력·쿨감·상태이상·방벽체력).
    private static DragonSkillEffectSO CreateActiveBranchEffect(
        AttributeSpec attr, SlotSpec slot, string suffix, int index, StatusAssets statuses)
    {
        string path = $"{DATA_FOLDER}/DE_{suffix}.asset";
        bool isSecondSlot = slot.Kind == DragonNodeKind.ActiveUp2;

        switch (attr.Type)
        {
            // 빙결은 상태이상 부여형이라 위력 배율을 읽지 않는다 - 1갈래는 쿨감, 2갈래는 지속시간을 늘린다.
            case DragonType.Ice when isSecondSlot:
                return CreateOrReplace<DragonSkillStatusEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;

                    // 랭크마다 다른 상태를 물려 기본 3초에서 R1 4초, R2 5초로 강화한다.
                    // 둘 다 켜져 있으면 DragonTreeManager.GetSkillStatusOverride가 높은 랭크를 고른다.
                    so.FindProperty("_status").objectReferenceValue =
                        index == 0 ? statuses.IceFreezeRank1 : statuses.IceFreezeStrong;
                });

            // 방벽 체력은 SkillSO 수치가 아니라 설치되는 건물의 체력이라 별도 효과다.
            case DragonType.Stone when isSecondSlot:
                return CreateOrReplace<DragonBarricadeEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_healthBonusRatio").floatValue = BARRICADE_HEALTH_BONUS_PER_RANK[index];
                });

            default:
                // 위력 배율이 실제로 쓰이는 건 데미지·회복형뿐이다(빙결·타워수리는 안 읽는다) -
                // 그런 속성은 위력 대신 쿨감만 준다.
                bool usesPower = UsesSkillPower(attr.Type);
                bool givePower = usesPower && (attr.Type == DragonType.Fire || attr.Type == DragonType.Stone
                    ? !isSecondSlot
                    : isSecondSlot);

                return CreateOrReplace<DragonSkillPowerEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_powerBonusRatio").floatValue = givePower ? SKILL_POWER_BONUS_PER_RANK[index] : 0f;
                    so.FindProperty("_cooldownReductionRatio").floatValue =
                        givePower ? 0f : SKILL_COOLDOWN_REDUCTION_PER_RANK[index];
                });
        }
    }

    // 위력 배율(DamagePercent·FlatDamage·HealAmount)을 실제로 읽는 스킬인지.
    private static bool UsesSkillPower(DragonType type) =>
        type == DragonType.Fire || type == DragonType.Stone || type == DragonType.Life;

    // 패시브 갈래 2 "가동 강화" - 생명만 전체 타워 최대체력이고, 나머지는 자기 속성 타워 한정이다.
    private static DragonSkillEffectSO CreatePassiveTowerEffect(AttributeSpec attr, string suffix, int index)
    {
        string path = $"{DATA_FOLDER}/DE_{suffix}.asset";
        float bonus = TOWER_STAT_BONUS_PER_RANK[index];

        return CreateOrReplace<DragonTowerStatEffectSO>(path, so =>
        {
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;

            bool isLife = attr.Type == DragonType.Life;
            bool isAttackSpeed = attr.Type == DragonType.Ice || attr.Type == DragonType.Time;

            so.FindProperty("_requireMatchingElement").boolValue = !isLife;
            so.FindProperty("_maxHealthBonusRatio").floatValue = isLife ? bonus : 0f;
            so.FindProperty("_attackSpeedBonusRatio").floatValue = !isLife && isAttackSpeed ? bonus : 0f;
            so.FindProperty("_damageBonusRatio").floatValue = !isLife && !isAttackSpeed ? bonus : 0f;
        });
    }

    // 궁극 = 잔존 효과 + 속성별 고유 효과.
    private static DragonSkillEffectSO[] CreateUltimateEffects(AttributeSpec attr, string suffix, StatusAssets statuses)
    {
        // (1) 루트가 준 기본 패시브와 같은 종류의 효과를 "비활성 전용"으로 하나 더 만든다.
        DragonSkillEffectSO persist = CreateAwakenEffect(attr, $"{DATA_FOLDER}/DE_{suffix}_Persist.asset", statuses);

        var persistSo = new SerializedObject(persist);
        persistSo.FindProperty("_suppressWhileActive").boolValue = true;
        persistSo.FindProperty("_inactiveScale").floatValue = ULTIMATE_INACTIVE_SCALE;

        // 숫자 효과(공속·생산량)는 _inactiveScale이 곱해져 저절로 절반이 되지만, 상태이상은
        // 곱할 수 없다 - 얼음·불은 절반 세기 에셋으로 갈아끼워야 "절반 남는다"가 성립한다.
        // 그 두 속성의 잔존 효과에만 _status가 있으므로 프로퍼티 유무로 갈린다.
        SerializedProperty statusProp = persistSo.FindProperty("_status");
        if (statusProp != null)
        {
            statusProp.objectReferenceValue = PersistStatusFor(attr.Type, statuses);
        }

        persistSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(persist);

        // (2) 속성별 고유 효과.
        string uniquePath = $"{DATA_FOLDER}/DE_{suffix}_Unique.asset";
        DragonSkillEffectSO unique;

        switch (attr.Type)
        {
            // 시간 = 원정 소요일 -1. 이미 존재·소비 중인 훅이라 신규 코드가 필요 없다.
            case DragonType.Time:
                unique = CreateOrReplace<KinConquestEffectSO>(uniquePath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_daysReduction").intValue = ULTIMATE_CONQUEST_DAYS_REDUCTION;
                });
                break;

            // 암석 = 메테오 일일 사용 횟수 증가.
            case DragonType.Stone:
                unique = CreateOrReplace<DragonSkillPowerEffectSO>(uniquePath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_extraUsePerDay").intValue = ULTIMATE_EXTRA_USE_PER_DAY;
                });
                break;

            default:
                unique = CreateOrReplace<DragonSkillPowerEffectSO>(uniquePath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_powerBonusRatio").floatValue =
                        UsesSkillPower(attr.Type) ? ULTIMATE_SKILL_POWER_BONUS : 0f;
                    so.FindProperty("_cooldownReductionRatio").floatValue = ULTIMATE_SKILL_COOLDOWN_REDUCTION;
                });
                break;
        }

        return new[] { persist, unique };
    }

    // 잔존 전용 상태이상. 루트 각성이 상태이상을 주는 속성(얼음·불)만 대상이다.
    private static StatusEffectSO PersistStatusFor(DragonType attribute, StatusAssets statuses)
    {
        switch (attribute)
        {
            case DragonType.Ice:
                return statuses.IceSlowPersist;

            case DragonType.Fire:
                return statuses.FireBurnPersist;

            default:
                return null;
        }
    }

    // 새끼용 A(타워형) - "그 새끼용 자신의" 능력만 강화한다.
    private static DragonSkillEffectSO CreateKinTowerEffect(
        AttributeSpec attr, string suffix, int index, StatusAssets statuses)
    {
        string path = $"{DATA_FOLDER}/DE_{suffix}.asset";
        int rank = index + 1;

        switch (attr.Type)
        {
            // 얼음: 랭크1은 슬로우 부여, 랭크2는 자기 공격력.
            case DragonType.Ice when rank == 1:
                return CreateKinTowerStatusEffect(path, attr, statuses.IceSlow);

            // 불: 랭크가 오르면 더 센 화상으로 갈아끼운다(틱데미지 강화).
            case DragonType.Fire:
                return CreateKinTowerStatusEffect(
                    path, attr, rank == 1 ? statuses.KinFireBurn : statuses.KinFireBurnStrong);

            // 생명 새끼용은 공격 데이터가 없는 버프 전용 개체라 전투 스탯이 의미가 없다 - 버프 반경을 준다.
            case DragonType.Life:
                return CreateKinBuffRadiusEffect(path, attr, KIN_BUFF_RADIUS_BONUS_PER_RANK[index]);

            default:
                return CreateOrReplace<KinTowerStatEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;

                    // 시간은 템포 속성이라 공속, 나머지(얼음 R2·암석)는 공격력.
                    bool isAttackSpeed = attr.Type == DragonType.Time;
                    float bonus = KIN_TOWER_STAT_BONUS_PER_RANK[index];

                    so.FindProperty("_attackSpeedBonusRatio").floatValue = isAttackSpeed ? bonus : 0f;
                    so.FindProperty("_damageBonusRatio").floatValue = isAttackSpeed ? 0f : bonus;
                    so.FindProperty("_rangeBonusRatio").floatValue = 0f;
                });
        }
    }

    // 새끼용 B(지역형) - 반경 안에 거는 효과.
    private static DragonSkillEffectSO CreateKinAreaEffect(AttributeSpec attr, string suffix, int index)
    {
        string path = $"{DATA_FOLDER}/DE_{suffix}.asset";

        switch (attr.Type)
        {
            // 얼음의 "강 결빙"과 시간의 "사막 페널티 완화"는 둘 다 BuffRadius로 판정되므로,
            // 반경을 넓히는 것이 곧 그 효과를 강화하는 것이다(BabyDragonBuffSystem 참고).
            case DragonType.Ice:
            case DragonType.Time:
                return CreateKinBuffRadiusEffect(path, attr, KIN_BUFF_RADIUS_BONUS_PER_RANK[index]);

            // 불: 새끼용 자신의 사거리.
            case DragonType.Fire:
                return CreateOrReplace<KinTowerStatEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_rangeBonusRatio").floatValue = KIN_TOWER_STAT_BONUS_PER_RANK[index];
                    so.FindProperty("_damageBonusRatio").floatValue = 0f;
                    so.FindProperty("_attackSpeedBonusRatio").floatValue = 0f;
                });

            // 암석·생명: 반경 내 생산 보너스.
            case DragonType.Stone:
            case DragonType.Life:
            default:
                return CreateOrReplace<KinAreaYieldEffectSO>(path, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_bonusRatio").floatValue = KIN_AREA_YIELD_BONUS_PER_RANK[index];
                });
        }
    }

    private static DragonSkillEffectSO CreateKinTowerStatusEffect(string path, AttributeSpec attr, StatusEffectSO status)
    {
        return CreateOrReplace<KinTowerStatusEffectSO>(path, so =>
        {
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_status").objectReferenceValue = status;
        });
    }

    private static DragonSkillEffectSO CreateKinBuffRadiusEffect(string path, AttributeSpec attr, float bonus)
    {
        return CreateOrReplace<KinBuffRadiusEffectSO>(path, so =>
        {
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_bonusRatio").floatValue = bonus;
        });
    }

    private static DragonSkillEffectSO CreateYieldEffect(string path, AttributeSpec attr, float bonus)
    {
        return CreateOrReplace<DragonYieldEffectSO>(path, so =>
        {
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_targetResources").intValue = (int)attr.PassiveYieldResources;
            so.FindProperty("_bonusRatio").floatValue = bonus;
        });
    }

    // 속성별 기본 패시브. 루트 노드가 이 효과를 그대로 쓰고, 궁극은 같은 함수로 만든 복제본에
    // (_activeScale=0, _inactiveScale=0.5)를 덮어써 "비활성일 때만 절반" 버전으로 만든다.
    private static DragonSkillEffectSO CreateAwakenEffect(AttributeSpec attr, string assetPath, StatusAssets statuses)
    {
        switch (attr.Type)
        {
            case DragonType.Ice:
                return CreateOrReplace<DragonTowerHitStatusEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_status").objectReferenceValue = statuses.IceSlow;
                });

            case DragonType.Fire:
                return CreateOrReplace<DragonSpawnStatusEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_status").objectReferenceValue = statuses.FireBurn;
                });

            case DragonType.Time:
                return CreateOrReplace<DragonTowerStatEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_attackSpeedBonusRatio").floatValue = AWAKEN_ATTACK_SPEED_BONUS;
                    so.FindProperty("_damageBonusRatio").floatValue = 0f;
                    so.FindProperty("_maxHealthBonusRatio").floatValue = 0f;
                    so.FindProperty("_requireMatchingElement").boolValue = false;
                });

            // 생명의 "전체 타워 최대체력"은 패시브 갈래2가 담당하므로, 기본 패시브는 생산만 맡는다
            // (같은 스탯을 두 노드가 중복해서 주면 잔존 계산까지 겹쳐 밸런싱이 어려워진다).
            case DragonType.Stone:
            case DragonType.Life:
            default:
                return CreateYieldEffect(assetPath, attr, AWAKEN_YIELD_BONUS);
        }
    }

    private static DragonSkillEffectSO[] One(DragonSkillEffectSO effect) =>
        effect != null ? new[] { effect } : Array.Empty<DragonSkillEffectSO>();

    // ---------------------------------------------------------------- 노드

    // 랭크1은 선행 슬롯의 최종 랭크를, 랭크2+는 같은 슬롯의 직전 랭크를 선행으로 삼는다.
    private static DragonSkillNodeData ResolvePrerequisite(
        Dictionary<(DragonType, DragonNodeKind, int), DragonSkillNodeData> nodesBySlot,
        SlotSpec[] slots,
        DragonType attribute,
        SlotSpec slot,
        int rank)
    {
        if (rank > 1)
        {
            return nodesBySlot[(attribute, slot.Kind, rank - 1)];
        }

        if (!slot.PrerequisiteSlot.HasValue)
        {
            return null;
        }

        DragonNodeKind previousKind = slot.PrerequisiteSlot.Value;
        int previousMaxRank = 1;

        foreach (SlotSpec candidate in slots)
        {
            if (candidate.Kind == previousKind)
            {
                previousMaxRank = candidate.MaxRank;
                break;
            }
        }

        return nodesBySlot[(attribute, previousKind, previousMaxRank)];
    }

    private static List<ResourceAmount> BuildRankCost(AttributeSpec attr, SlotSpec slot, int rank)
    {
        int index = rank - 1;
        var cost = new List<ResourceAmount>();

        int slimeAmount = slot.SlimeCostPerRank[index];
        if (slimeAmount > 0)
        {
            cost.Add(new ResourceAmount { Type = attr.Slime, Amount = slimeAmount });
        }

        int specialAmount = slot.SpecialCostPerRank[index];
        if (specialAmount > 0 && attr.Specialized != ResourceType.None)
        {
            cost.Add(new ResourceAmount { Type = attr.Specialized, Amount = specialAmount });
        }

        return cost;
    }

    private static DragonSkillNodeData CreateNode(
        AttributeSpec attr,
        SlotSpec slot,
        int rank,
        DragonSkillNodeData prerequisite,
        ProgressionGateSO[] gates,
        DragonSkillEffectSO[] effects)
    {
        string nodeId = $"dragon_{attr.Key}_{slot.Key}_r{rank}";
        string assetPath = $"{DATA_FOLDER}/DN_{Capitalize(attr.Key)}_{Capitalize(slot.Key)}_R{rank}.asset";

        // 로컬 키는 슬롯 단위로만 만든다 - 랭크마다 키를 늘리면 스트링테이블이 두 배가 된다.
        string nameLocKey = $"dragon_node_{attr.Key}_{slot.Key}_name";
        string descLocKey = $"dragon_node_{attr.Key}_{slot.Key}_desc";

        List<ResourceAmount> cost = BuildRankCost(attr, slot, rank);

        DragonSkillNodeData node = CreateOrReplace<DragonSkillNodeData>(assetPath, so =>
        {
            so.FindProperty("_nodeId").stringValue = nodeId;
            so.FindProperty("_nameLocKey").stringValue = nameLocKey;
            so.FindProperty("_descriptionLocKey").stringValue = descLocKey;
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_kind").enumValueIndex = (int)slot.Kind;
            so.FindProperty("_rank").intValue = rank;
            so.FindProperty("_maxRank").intValue = slot.MaxRank;

            AssignObjectArray(so.FindProperty("_prerequisites"),
                prerequisite != null ? new UnityEngine.Object[] { prerequisite } : Array.Empty<UnityEngine.Object>());
            AssignObjectArray(so.FindProperty("_gates"), gates);
            AssignObjectArray(so.FindProperty("_effects"), effects);

            SerializedProperty costProp = so.FindProperty("_resourceCost");
            costProp.arraySize = cost.Count;
            for (int i = 0; i < cost.Count; i++)
            {
                SerializedProperty element = costProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").intValue = (int)cost[i].Type;
                element.FindPropertyRelative("Amount").intValue = cost[i].Amount;
            }
        });

        // 랭크1에서만 등록한다 - 랭크2가 같은 키를 다시 넣으면 CSV에 중복 행이 생긴다.
        if (rank == 1)
        {
            AddLocRow(nameLocKey, SlotText(attr, slot.Kind, isKorean: false, isDescription: false),
                SlotText(attr, slot.Kind, isKorean: true, isDescription: false));
            AddLocRow(descLocKey, SlotText(attr, slot.Kind, isKorean: false, isDescription: true),
                SlotText(attr, slot.Kind, isKorean: true, isDescription: true));
        }

        return node;
    }

    private static KinCountGateSO CreateKinCountGate(string path, AttributeSpec attr, int required, string lockedLocKey)
    {
        return CreateOrReplace<KinCountGateSO>(path, so =>
        {
            so.FindProperty("_requiredKinCount").intValue = required;
            so.FindProperty("_scopeToAttribute").boolValue = true;
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_lockedLocKey").stringValue = lockedLocKey;
        });
    }

    // ---------------------------------------------------------------- 노드 문구

    // 슬롯별 이름·설명. 영문은 아직 팀 확정 전이라 한국어를 기준으로 두고 [TBD] 표기만 붙인다.
    private static string SlotText(AttributeSpec attr, DragonNodeKind kind, bool isKorean, bool isDescription)
    {
        string ko = SlotTextKo(attr, kind, isDescription);
        return isKorean ? $"[미정] {ko}" : $"[TBD] {ko}";
    }

    private static string SlotTextKo(AttributeSpec attr, DragonNodeKind kind, bool isDescription)
    {
        switch (kind)
        {
            case DragonNodeKind.ActiveUnlock:
                return isDescription
                    ? $"{attr.KoreanName} 어미용의 액티브 스킬을 해금합니다."
                    : $"{attr.KoreanName} 액티브 해금";

            case DragonNodeKind.ActiveUp1:
                return isDescription ? ActiveUpDescKo(attr, isSecond: false) : $"{attr.KoreanName} 액티브 강화 I";

            case DragonNodeKind.ActiveUp2:
                return isDescription ? ActiveUpDescKo(attr, isSecond: true) : $"{attr.KoreanName} 액티브 강화 II";

            case DragonNodeKind.PassiveUp1:
                return isDescription
                    ? $"{attr.KoreanName} 활성 시 관련 자원의 생산량이 증가합니다."
                    : $"{attr.KoreanName} 생산 강화";

            case DragonNodeKind.PassiveUp2:
                return isDescription ? PassiveUpDescKo(attr) : $"{attr.KoreanName} 가동 강화";

            case DragonNodeKind.Ultimate:
                return isDescription
                    ? $"{attr.KoreanName}의 패시브가 다른 속성을 사용하는 동안에도 절반 남습니다. " + UltimateUniqueDescKo(attr)
                    : $"{attr.KoreanName} 궁극";

            case DragonNodeKind.KinTower:
                return isDescription ? KinTowerDescKo(attr) : $"{attr.KoreanName} 새끼용 숙련";

            case DragonNodeKind.KinArea:
            default:
                return isDescription ? KinAreaDescKo(attr) : $"{attr.KoreanName} 새끼용 지역 강화";
        }
    }

    private static string ActiveUpDescKo(AttributeSpec attr, bool isSecond)
    {
        switch (attr.Type)
        {
            case DragonType.Ice:
                return isSecond ? "액티브 [전역 빙결]의 빙결 지속시간이 늘어납니다." : "액티브 [전역 빙결]의 쿨다운이 줄어듭니다.";
            case DragonType.Fire:
                return isSecond ? "액티브 [전역 화염 피해]의 쿨다운이 줄어듭니다." : "액티브 [전역 화염 피해]의 위력이 강해집니다.";
            case DragonType.Time:
                return "액티브 [즉시 재활성화]의 쿨다운이 줄어듭니다.";
            case DragonType.Stone:
                return isSecond ? "설치되는 방벽의 최대 체력이 늘어납니다." : "메테오의 피해량이 늘어납니다.";
            case DragonType.Life:
            default:
                return isSecond ? "액티브 [성 회복]의 회복량이 늘어납니다." : "액티브 [성 회복]의 쿨다운이 줄어듭니다.";
        }
    }

    private static string PassiveUpDescKo(AttributeSpec attr)
    {
        switch (attr.Type)
        {
            case DragonType.Ice:
                return "얼음 활성 시, 얼음 속성 타워의 공격속도가 증가합니다(다른 속성 타워는 영향 없음).";
            case DragonType.Fire:
                return "불 활성 시, 불 속성 타워의 공격력이 증가합니다(다른 속성 타워는 영향 없음).";
            case DragonType.Time:
                return "시간 활성 시, 시간 속성 타워의 공격속도가 증가합니다(다른 속성 타워는 영향 없음).";
            case DragonType.Stone:
                return "암석 활성 시, 암석 속성 타워의 공격력이 증가합니다(다른 속성 타워는 영향 없음).";
            case DragonType.Life:
            default:
                return "생명 활성 시, 모든 타워의 최대 체력이 증가합니다.";
        }
    }

    private static string UltimateUniqueDescKo(AttributeSpec attr)
    {
        switch (attr.Type)
        {
            case DragonType.Time:
                return "또한 원정(점령)에 소모되는 일수가 1 줄어듭니다.";
            case DragonType.Stone:
                return "또한 액티브 [메테오+방벽]의 일일 사용 횟수가 늘어납니다.";
            default:
                return "또한 액티브 스킬이 최종 강화됩니다.";
        }
    }

    private static string KinTowerDescKo(AttributeSpec attr)
    {
        switch (attr.Type)
        {
            case DragonType.Ice:
                return "얼음 새끼용의 공격에 둔화가 붙고, 강화할수록 자신의 공격력이 증가합니다.";
            case DragonType.Fire:
                return "불 새끼용이 입히는 화상 피해가 증가합니다.";
            case DragonType.Time:
                return "시간 새끼용 자신의 공격속도가 증가합니다.";
            case DragonType.Stone:
                return "암석 새끼용 자신의 공격력이 증가합니다.";
            case DragonType.Life:
            default:
                return "생명 새끼용 버프모드의 효과 범위가 넓어집니다.";
        }
    }

    private static string KinAreaDescKo(AttributeSpec attr)
    {
        switch (attr.Type)
        {
            case DragonType.Ice:
                return "얼음 새끼용의 버프 범위가 넓어져 강 결빙(건설 해제) 범위가 확대됩니다.";
            case DragonType.Fire:
                return "불 새끼용 자신의 공격 사거리가 증가합니다.";
            case DragonType.Time:
                return "시간 새끼용의 버프 범위가 넓어져 사막 페널티 완화 범위가 확대됩니다.";
            case DragonType.Stone:
                return "암석 새끼용 주변 광산의 생산량이 증가합니다.";
            case DragonType.Life:
            default:
                return "생명 새끼용 주변 농장의 생산량이 증가합니다.";
        }
    }

    // ---------------------------------------------------------------- 공용 헬퍼

    private static void AssignObjectArray(SerializedProperty arrayProp, UnityEngine.Object[] values)
    {
        int count = values?.Length ?? 0;
        arrayProp.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static T CreateOrReplace<T>(string assetPath, Action<SerializedObject> configure) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        // 디스크의 에셋 타입이 T와 다르면 LoadAssetAtPath<T>가 null을 돌려주고, 아래 CreateAsset이
        // 기존 에셋을 지우고 새로 만든다 - guid가 바뀌어 그 에셋을 참조하던 씬·프리팹·다른 에셋의
        // 참조가 전부 조용히 끊긴다(이슈 #188). 손으로 타입을 바꾼 에셋을 덮어쓰기 전에 멈춘다.
        if (existing == null)
        {
            var conflicting = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (conflicting != null)
            {
                throw new InvalidOperationException(
                    $"'{assetPath}'의 실제 타입은 {conflicting.GetType().Name}인데 생성기는 {typeof(T).Name}으로 만들려 합니다. " +
                    "그대로 진행하면 에셋이 삭제·재생성되어 guid가 바뀌고 기존 참조가 끊깁니다. " +
                    "생성기와 에셋 중 어느 쪽이 맞는지 정한 뒤 다시 실행하세요.");
            }
        }

        T instance = existing != null ? existing : ScriptableObject.CreateInstance<T>();

        var serializedObject = new SerializedObject(instance);
        configure(serializedObject);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        if (existing == null)
        {
            AssetDatabase.CreateAsset(instance, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(instance);
        }

        return instance;
    }

    private static void AddLocRow(string id, string en, string ko)
    {
        _locRows.Add(new LocRow { Id = id, En = en, Ko = ko });
    }

    private static void AddCommonLocRows()
    {
        AddLocRow("dragon_window_header", "Dragons", "용");
        AddLocRow("dragon_upgrade_button", "Upgrade!", "강화!");
        AddLocRow("dragon_rank_badge", "Lv {0}/{1}", "Lv {0}/{1}");
        AddLocRow("dragon_state_invalid", "Unavailable", "이용 불가");
        AddLocRow("dragon_state_completed", "Completed", "완료");
        AddLocRow("dragon_state_unavailable_phase", "Available during the day only", "낮에만 가능");
        AddLocRow("dragon_state_prerequisite_locked", "Prerequisite Required", "선행 필요");
        AddLocRow("dragon_state_gate_locked", "Locked", "잠김");
        AddLocRow("dragon_state_insufficient_resources", "Insufficient Resources", "자원 부족");
        AddLocRow("dragon_state_available", "Available", "해금 가능");
        AddLocRow("dragon_resource_cost", "{0} {1}", "{0} {1}");
        AddLocRow("dragon_unknown_resource", "[TBD] Resource", "[미정] 자원");
    }

    private static void AppendLocalizationCsv()
    {
        string localizationDir = Path.Combine(Application.streamingAssetsPath, "Localization");
        AppendLocalizationCsv(Path.Combine(localizationDir, "en_us.csv"), row => row.En);
        AppendLocalizationCsv(Path.Combine(localizationDir, "ko_kr.csv"), row => row.Ko);
    }

    private static void AppendLocalizationCsv(string path, Func<LocRow, string> valueSelector)
    {
        var existingIds = new HashSet<string>();

        if (File.Exists(path))
        {
            using (var reader = new StreamReader(path, Encoding.UTF8))
            using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                foreach (CsvRow record in csvReader.GetRecords<CsvRow>())
                {
                    if (!string.IsNullOrEmpty(record.Id))
                    {
                        existingIds.Add(record.Id);
                    }
                }
            }
        }

        var newLines = new List<string>();
        foreach (LocRow row in _locRows)
        {
            if (existingIds.Contains(row.Id))
            {
                continue;
            }

            existingIds.Add(row.Id);
            newLines.Add(FormatCsvRow(row.Id, valueSelector(row)));
        }

        if (newLines.Count == 0)
        {
            return;
        }

        // 기존 파일이 줄바꿈 없이 끝나면 그대로 append할 때 마지막 줄과 새 줄이 붙어 CSV가 깨진다.
        bool needsLeadingNewline = File.Exists(path) && !EndsWithNewline(path);

        using (var writer = new StreamWriter(path, append: true, encoding: Encoding.UTF8))
        {
            if (needsLeadingNewline)
            {
                writer.WriteLine();
            }

            foreach (string line in newLines)
            {
                writer.WriteLine(line);
            }
        }
    }

    private static bool EndsWithNewline(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            if (stream.Length == 0)
            {
                return true;
            }

            stream.Seek(-1, SeekOrigin.End);
            int lastByte = stream.ReadByte();
            return lastByte == '\n' || lastByte == '\r';
        }
    }

    private static void WriteSheetExportFragments()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string enPath = Path.Combine(projectRoot, SHEET_EXPORT_FOLDER, "로컬라이징_용스킬트리_en_us.csv");
        string koPath = Path.Combine(projectRoot, SHEET_EXPORT_FOLDER, "로컬라이징_용스킬트리_ko_kr.csv");

        WriteFragment(enPath, row => row.En);
        WriteFragment(koPath, row => row.Ko);
    }

    private static void WriteFragment(string path, Func<LocRow, string> valueSelector)
    {
        using (var writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8))
        {
            writer.WriteLine("\"Id\",\"String\"");
            foreach (LocRow row in _locRows)
            {
                writer.WriteLine(FormatCsvRow(row.Id, valueSelector(row)));
            }
        }
    }

    private static string FormatCsvRow(string id, string value)
    {
        return $"\"{EscapeCsv(id)}\",\"{EscapeCsv(value)}\"";
    }

    private static string EscapeCsv(string value)
    {
        return (value ?? string.Empty).Replace("\"", "\"\"");
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(assetFolderPath);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string Capitalize(string key)
    {
        return string.IsNullOrEmpty(key) ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
    }
}
