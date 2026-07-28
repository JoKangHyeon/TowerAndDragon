using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using UnityEditor;
using UnityEngine;

// 용 스킬트리 SO 에셋(노드 30개, 효과, 상태이상, 게이트, 스킬, 트리)을 코드로 생성한다.
// 손으로 .asset YAML을 작성하지 않기 위한 에디터 전용 도구 - CLAUDE.md 커밋규칙 §5(에디터 전용 코드)
// 리터럴 제한 예외에 해당한다. 재실행 시 기존 에셋을 재사용(GUID 유지)해 멱등적으로 동작한다.
// 코스트·수치는 전부 Docs/Sangwook/용_스킬트리_로드맵.md §4·§9의 예시(밸런싱 대상)를 그대로 옮긴 것이다.
//
// 효과 구현 범위(용 스킬 효과 구현 계획 참고):
// - 어미용 각성 = 속성별 실제 게임플레이 효과(패시브) 1종
// - 어미용 액티브 I = SkillManager가 발동하는 액티브 스킬 해금(메테오는 기존 스킬 재사용,
//   나머지 4종은 신규 SkillType)
// - 어미용 강화/궁극 = 액티브 스킬 위력·쿨다운 강화(DragonSkillPowerEffectSO). 로드맵이 말하는
//   "패시브 수치 강화"까지는 이번 범위에 넣지 않았다 - 속성마다 다른 패시브 타입을 강화 단계별로
//   중복 배선하면 GetTowerHitStatus 같은 "첫 매치 반환" 계약과 충돌해 오히려 일관성이 깨진다.
// - 새끼용 A(타워형)는 얼음·불만 구현(시간·암석·생명은 기획 [미정] - KinTowerStatusEffectSO를
//   만들되 _status를 비워 no-op으로 둔다).
// - 새끼용 B(지역형)는 얼음만 미구현(강 결빙 - 지형 기반 배치 제한 시스템 자체가 없음).
// - 생명 액티브 I(바리케이드)는 몬스터를 막을 구조물 프리팹/스프라이트가 없어 SkillSO를
//   생성하지 않는다 - 팀이 에셋을 제공하면 후속 작업으로 연결한다.
public static class DragonSkillTreeAssetGenerator
{
    private const string DATA_FOLDER = "Assets/Data/Dragon";
    private const string SKILL_SUBFOLDER = "Skills";
    private const string STATUS_SUBFOLDER = "Status";
    private const string SHEET_EXPORT_FOLDER = "Docs";

    private const int AWAKEN_SLIME_COST = 5;
    private const int ACTIVE_SLIME_COST = 10;
    private const int ACTIVE_SPECIAL_COST = 3;
    private const int ENHANCE_SLIME_COST = 20;
    private const int ENHANCE_SPECIAL_COST = 8;
    private const int ULTIMATE_SLIME_COST = 40;
    private const int ULTIMATE_SPECIAL_COST = 15;
    private const int KIN_SLIME_COST = 8;
    private const int KIN_SPECIAL_COST = 3;

    // 어미용 강화/궁극 게이트 - 새끼용(KinTower/KinArea) 해금 수가 전역 합산으로 이 값 이상.
    private const int ENHANCE_REQUIRED_KIN_COUNT = 2;
    private const int ULTIMATE_REQUIRED_KIN_COUNT = 4;

    // 강화/궁극 노드가 액티브 스킬에 주는 위력 배율 보너스·쿨다운 감소 비율(예시, 밸런싱 대상).
    private const float ENHANCE_SKILL_POWER_BONUS = 0.2f;
    private const float ENHANCE_SKILL_COOLDOWN_REDUCTION = 0.1f;
    private const float ULTIMATE_SKILL_POWER_BONUS = 0.5f;
    private const float ULTIMATE_SKILL_COOLDOWN_REDUCTION = 0.25f;

    // 상태이상 예시 수치(밸런싱 대상) - 스트링테이블과 마찬가지로 팀 확정 전 값.
    private const float ICE_SLOW_MULTIPLIER = 0.5f;
    private const float ICE_SLOW_DURATION = 3f;
    private const float FIRE_BURN_DAMAGE_PER_TICK = 2f;
    private const float FIRE_BURN_TICK_INTERVAL = 1f;
    private const float FIRE_BURN_DURATION_INFINITE = 0f; // 상시 화상 - 몬스터가 죽을 때까지 유지.

    // 어미용 시간 각성(타워 공속↑)·암석/생명 각성(생산↑) 보너스(예시, 밸런싱 대상).
    private const float TIME_ATTACK_SPEED_BONUS = 0.15f;
    private const float YIELD_BONUS_RATIO = 0.2f;

    // 새끼용 지역형(B 슬롯) 보너스(예시, 밸런싱 대상).
    private const int FIRE_KIN_VISION_BONUS_RADIUS = 2;
    private const int TIME_KIN_CONQUEST_DAYS_REDUCTION = 1;
    private const float KIN_AREA_YIELD_BONUS_RATIO = 0.25f;

    // 액티브 스킬 예시 수치(밸런싱 대상).
    private const float SKILL_DEFAULT_COOLTIME = 60f;
    private const int SKILL_UNLIMITED_USE_PER_DAY = -1;
    private const float GLOBAL_DAMAGE_PERCENT_OF_CURRENT_HEALTH = 0.3f;

    private struct AttributeSpec
    {
        public DragonType Type;
        public string Key;
        public string KoreanName;
        public ResourceType Slime;
        public ResourceType Specialized;
        public string PassiveDescKo;
        public string PassiveDescEn;
        public string ActiveDescKo;
        public string ActiveDescEn;
        public string KinTowerDescKo;
        public string KinTowerDescEn;
        public string KinAreaDescKo;
        public string KinAreaDescEn;
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
            PassiveDescKo = "타워 기본공격에 슬로우 부여", PassiveDescEn = "Tower basic attacks apply slow.",
            ActiveDescKo = "모든 적 빙결", ActiveDescEn = "Freeze all enemies.",
            KinTowerDescKo = "슬로우 타워", KinTowerDescEn = "Slow tower.",
            KinAreaDescKo = "주변 강을 얼려 건물 배치 가능", KinAreaDescEn = "Freeze nearby rivers to allow building placement.",
        },
        new AttributeSpec
        {
            Type = DragonType.Fire, Key = "fire", KoreanName = "불",
            Slime = ResourceType.VolcanoSlime, Specialized = ResourceType.FlameHeart,
            PassiveDescKo = "모든 적에게 화상 부여", PassiveDescEn = "Apply burn to all enemies.",
            ActiveDescKo = "화상 재부여 + 전역 대미지", ActiveDescEn = "Reapply burn + deal global damage.",
            KinTowerDescKo = "화상 타워", KinTowerDescEn = "Burn tower.",
            KinAreaDescKo = "시야 범위 증가", KinAreaDescEn = "Increase vision range.",
        },
        new AttributeSpec
        {
            Type = DragonType.Time, Key = "time", KoreanName = "시간",
            Slime = ResourceType.DesertSlime, Specialized = ResourceType.TimeSand,
            PassiveDescKo = "타워 공격속도 증가", PassiveDescEn = "Increase tower attack speed.",
            ActiveDescKo = "파괴된 타워 즉시 수리", ActiveDescEn = "Instantly repair destroyed towers.",
            KinTowerDescKo = "효과 미정", KinTowerDescEn = "Effect not yet defined.",
            KinAreaDescKo = "점령 소요일 감소(원정 동행 근사)", KinAreaDescEn = "Reduces conquest duration (approximates joining expeditions).",
        },
        new AttributeSpec
        {
            Type = DragonType.Stone, Key = "stone", KoreanName = "암석",
            Slime = ResourceType.RockSlime, Specialized = ResourceType.PhilosopherStone,
            PassiveDescKo = "광산 생산량 증가", PassiveDescEn = "Increase mine production.",
            ActiveDescKo = "메테오", ActiveDescEn = "Meteor.",
            KinTowerDescKo = "효과 미정", KinTowerDescEn = "Effect not yet defined.",
            KinAreaDescKo = "주변 광산 생산량 증가", KinAreaDescEn = "Increase nearby mine production.",
        },
        new AttributeSpec
        {
            Type = DragonType.Life, Key = "life", KoreanName = "생명",
            Slime = ResourceType.GrassSlime, Specialized = ResourceType.None,
            PassiveDescKo = "농장 생산량 증가", PassiveDescEn = "Increase farm production.",
            ActiveDescKo = "바리케이드", ActiveDescEn = "Barricade.",
            KinTowerDescKo = "효과 미정", KinTowerDescEn = "Effect not yet defined.",
            KinAreaDescKo = "주변 농장 생산량 증가", KinAreaDescEn = "Increase nearby farm production.",
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

        // 1. 게이트: 새끼용 보유(속성별 5개) + 새끼용 수(강화/궁극 2개)
        var kinOwnedGates = new Dictionary<DragonType, KinOwnedGateSO>();
        foreach (AttributeSpec attr in attributes)
        {
            kinOwnedGates[attr.Type] = CreateOrReplace<KinOwnedGateSO>(
                $"{DATA_FOLDER}/DG_KinOwned_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_lockedLocKey").stringValue = $"dragon_gate_kin_owned_{attr.Key}";
                });

            AddLocRow($"dragon_gate_kin_owned_{attr.Key}",
                $"[TBD] Locked: requires a {attr.Key} baby dragon",
                $"[미정] 잠김: {attr.KoreanName} 새끼용을 보유해야 합니다");

            // 방사형 창의 속성 라벨(중앙 링)에 쓰는 짧은 속성 이름.
            AddLocRow($"dragon_attribute_{attr.Key}", $"[TBD] {Capitalize(attr.Key)}", attr.KoreanName);
        }

        KinCountGateSO enhanceGate = CreateOrReplace<KinCountGateSO>(
            $"{DATA_FOLDER}/DG_KinCount_Enhance.asset",
            so =>
            {
                so.FindProperty("_requiredKinCount").intValue = ENHANCE_REQUIRED_KIN_COUNT;
                so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_count_enhance";
            });
        AddLocRow("dragon_gate_kin_count_enhance",
            "[TBD] Locked: requires {0} unlocked baby dragon nodes",
            "[미정] 잠김: 새끼용 노드 {0}개 해금 필요");

        KinCountGateSO ultimateGate = CreateOrReplace<KinCountGateSO>(
            $"{DATA_FOLDER}/DG_KinCount_Ultimate.asset",
            so =>
            {
                so.FindProperty("_requiredKinCount").intValue = ULTIMATE_REQUIRED_KIN_COUNT;
                so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_count_ultimate";
            });
        AddLocRow("dragon_gate_kin_count_ultimate",
            "[TBD] Locked: requires {0} unlocked baby dragon nodes",
            "[미정] 잠김: 새끼용 노드 {0}개 해금 필요");

        // 2. 상태이상 데이터(얼음 슬로우 · 불 화상) - 어미용 패시브와 새끼용 타워형이 공유한다.
        MoveSpeedStatusSO iceSlowStatus = CreateOrReplace<MoveSpeedStatusSO>(
            $"{DATA_FOLDER}/{STATUS_SUBFOLDER}/DS_IceSlow.asset",
            so =>
            {
                so.FindProperty("_statusId").stringValue = "dragon_ice_slow";
                so.FindProperty("_durationSeconds").floatValue = ICE_SLOW_DURATION;
                so.FindProperty("_speedMultiplier").floatValue = ICE_SLOW_MULTIPLIER;
            });

        DamageOverTimeStatusSO fireBurnStatus = CreateOrReplace<DamageOverTimeStatusSO>(
            $"{DATA_FOLDER}/{STATUS_SUBFOLDER}/DS_FireBurn.asset",
            so =>
            {
                so.FindProperty("_statusId").stringValue = "dragon_fire_burn";
                so.FindProperty("_durationSeconds").floatValue = FIRE_BURN_DURATION_INFINITE;
                so.FindProperty("_damagePerTick").floatValue = FIRE_BURN_DAMAGE_PER_TICK;
                so.FindProperty("_tickIntervalSeconds").floatValue = FIRE_BURN_TICK_INTERVAL;
            });

        // 3. 액티브 스킬 - 메테오는 기존 SkillType 재사용, 나머지는 신규. 생명(바리케이드)은
        //    대상 프리팹이 없어 만들지 않는다(팀 에셋 제공 후 후속 작업).
        var skillByAttribute = new Dictionary<DragonType, SkillSO>();

        skillByAttribute[DragonType.Ice] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_FreezeAll.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.FREEZE_ALL;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_freeze_all_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_freeze_all_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
                so.FindProperty("AppliedStatus").objectReferenceValue = iceSlowStatus;
            });

        skillByAttribute[DragonType.Fire] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_GlobalDamage.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.GLOBAL_CURRENT_HEALTH_DAMAGE;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_global_damage_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_global_damage_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
                so.FindProperty("DamagePercentOfCurrentHealth").floatValue = GLOBAL_DAMAGE_PERCENT_OF_CURRENT_HEALTH;
                so.FindProperty("AppliedStatus").objectReferenceValue = fireBurnStatus;
            });

        skillByAttribute[DragonType.Time] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_RepairTowers.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.REPAIR_TOWERS;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_repair_towers_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_repair_towers_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
            });

        skillByAttribute[DragonType.Stone] = CreateOrReplace<SkillSO>(
            $"{DATA_FOLDER}/{SKILL_SUBFOLDER}/SK_Dragon_Meteor.asset",
            so =>
            {
                so.FindProperty("Type").enumValueIndex = (int)SkillType.AREA_CURRENT_HEALTH_DAMAGE;
                so.FindProperty("NameStringKey").stringValue = "dragon_skill_meteor_name";
                so.FindProperty("DescriptionStringKey").stringValue = "dragon_skill_meteor_desc";
                so.FindProperty("DefaultCooltime").floatValue = SKILL_DEFAULT_COOLTIME;
                so.FindProperty("DefaultUsePerDay").intValue = SKILL_UNLIMITED_USE_PER_DAY;
                so.FindProperty("DamagePercentOfCurrentHealth").floatValue = GLOBAL_DAMAGE_PERCENT_OF_CURRENT_HEALTH;
            });

        AddLocRow("dragon_skill_freeze_all_name", "[TBD] Freeze All", "[미정] 모든 적 빙결");
        AddLocRow("dragon_skill_freeze_all_desc", "[TBD] Freezes all enemies currently on the field.", "[미정] 현재 필드의 모든 적을 빙결시킵니다.");
        AddLocRow("dragon_skill_global_damage_name", "[TBD] Burn Surge", "[미정] 화상 폭발");
        AddLocRow("dragon_skill_global_damage_desc", "[TBD] Deals damage to all enemies and reapplies burn.", "[미정] 모든 적에게 피해를 주고 화상을 다시 겁니다.");
        AddLocRow("dragon_skill_repair_towers_name", "[TBD] Instant Repair", "[미정] 즉시 수리");
        AddLocRow("dragon_skill_repair_towers_desc", "[TBD] Instantly revives all disabled towers.", "[미정] 비활성화된 모든 타워를 즉시 복구합니다.");
        AddLocRow("dragon_skill_meteor_name", "[TBD] Meteor", "[미정] 메테오");
        AddLocRow("dragon_skill_meteor_desc", "[TBD] Calls down a meteor on a target area.", "[미정] 지정 지역에 운석을 떨어뜨립니다.");

        // 4. 효과: 속성별 각성 패시브 + 액티브 해금 + 강화/궁극 스킬 강화 + 새끼용 A/B
        var awakenEffects = new Dictionary<DragonType, DragonSkillEffectSO>();
        var activeEffects = new Dictionary<DragonType, ActiveSkillUnlockEffectSO>();
        var enhanceEffects = new Dictionary<DragonType, DragonSkillPowerEffectSO>();
        var ultimateEffects = new Dictionary<DragonType, DragonSkillPowerEffectSO>();
        var kinTowerEffects = new Dictionary<DragonType, KinTowerStatusEffectSO>();
        var kinAreaEffects = new Dictionary<DragonType, DragonSkillEffectSO>();

        foreach (AttributeSpec attr in attributes)
        {
            awakenEffects[attr.Type] = CreateAwakenEffect(attr, iceSlowStatus, fireBurnStatus);

            activeEffects[attr.Type] = CreateOrReplace<ActiveSkillUnlockEffectSO>(
                $"{DATA_FOLDER}/DE_ActiveSkillUnlock_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    skillByAttribute.TryGetValue(attr.Type, out SkillSO skill);
                    so.FindProperty("_skill").objectReferenceValue = skill; // 생명은 skill이 없어 null 그대로 - stub.
                });

            enhanceEffects[attr.Type] = CreateOrReplace<DragonSkillPowerEffectSO>(
                $"{DATA_FOLDER}/DE_SkillPower_Enhance_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_powerBonusRatio").floatValue = ENHANCE_SKILL_POWER_BONUS;
                    so.FindProperty("_cooldownReductionRatio").floatValue = ENHANCE_SKILL_COOLDOWN_REDUCTION;
                });

            ultimateEffects[attr.Type] = CreateOrReplace<DragonSkillPowerEffectSO>(
                $"{DATA_FOLDER}/DE_SkillPower_Ultimate_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_powerBonusRatio").floatValue = ULTIMATE_SKILL_POWER_BONUS;
                    so.FindProperty("_cooldownReductionRatio").floatValue = ULTIMATE_SKILL_COOLDOWN_REDUCTION;
                });

            kinTowerEffects[attr.Type] = CreateOrReplace<KinTowerStatusEffectSO>(
                $"{DATA_FOLDER}/DE_KinTower_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;

                    // 얼음·불만 상태 배정 - 시간·암석·생명은 기획 [미정]이라 null로 둔다(no-op).
                    StatusEffectSO status = attr.Type == DragonType.Ice ? iceSlowStatus
                        : attr.Type == DragonType.Fire ? fireBurnStatus
                        : null;
                    so.FindProperty("_status").objectReferenceValue = status;
                });

            kinAreaEffects[attr.Type] = CreateKinAreaEffect(attr);
        }

        // 5. 노드 30개(5속성 × 6종)
        var orderedNodes = new List<DragonSkillNodeData>();

        foreach (AttributeSpec attr in attributes)
        {
            List<ResourceAmount> awakenCost = BuildCost(attr, AWAKEN_SLIME_COST, 0);
            List<ResourceAmount> activeCost = BuildCost(attr, ACTIVE_SLIME_COST, ACTIVE_SPECIAL_COST);
            List<ResourceAmount> enhanceCost = BuildCost(attr, ENHANCE_SLIME_COST, ENHANCE_SPECIAL_COST);
            List<ResourceAmount> ultimateCost = BuildCost(attr, ULTIMATE_SLIME_COST, ULTIMATE_SPECIAL_COST);
            List<ResourceAmount> kinCost = BuildCost(attr, KIN_SLIME_COST, KIN_SPECIAL_COST);

            DragonSkillNodeData awaken = CreateNode(attr, "awaken", DragonNodeKind.MotherAwaken,
                null, null, awakenCost, new[] { awakenEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Awaken", $"[TBD] Passive (while active): {attr.PassiveDescEn}",
                $"[미정] {attr.KoreanName} 각성", $"[미정] 패시브(활성 시): {attr.PassiveDescKo}");

            DragonSkillNodeData active = CreateNode(attr, "active", DragonNodeKind.MotherActive,
                new[] { awaken }, null, activeCost, new DragonSkillEffectSO[] { activeEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Active I", $"[TBD] Active skill: {attr.ActiveDescEn}",
                $"[미정] {attr.KoreanName} 액티브 I", $"[미정] 액티브 스킬: {attr.ActiveDescKo}");

            DragonSkillNodeData enhance = CreateNode(attr, "enhance", DragonNodeKind.MotherEnhance,
                new[] { active }, new ProgressionGateSO[] { enhanceGate }, enhanceCost,
                new DragonSkillEffectSO[] { enhanceEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Enhance", "[TBD] Enhances the active skill above.",
                $"[미정] {attr.KoreanName} 강화", "[미정] 위 액티브 스킬을 강화합니다.");

            DragonSkillNodeData ultimate = CreateNode(attr, "ultimate", DragonNodeKind.MotherUltimate,
                new[] { enhance }, new ProgressionGateSO[] { ultimateGate }, ultimateCost,
                new DragonSkillEffectSO[] { ultimateEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Ultimate", "[TBD] Final upgrade of this attribute.",
                $"[미정] {attr.KoreanName} 궁극", "[미정] 이 속성의 최종 강화입니다.");

            DragonSkillNodeData kinTower = CreateNode(attr, "kin_tower", DragonNodeKind.KinTower,
                new[] { awaken }, new ProgressionGateSO[] { kinOwnedGates[attr.Type] }, kinCost,
                new DragonSkillEffectSO[] { kinTowerEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Kin Tower", $"[TBD] {attr.KinTowerDescEn}",
                $"[미정] {attr.KoreanName} 새끼용 타워", $"[미정] {attr.KinTowerDescKo}");

            DragonSkillEffectSO[] kinAreaArray = kinAreaEffects[attr.Type] != null
                ? new[] { kinAreaEffects[attr.Type] }
                : Array.Empty<DragonSkillEffectSO>();

            DragonSkillNodeData kinArea = CreateNode(attr, "kin_area", DragonNodeKind.KinArea,
                new[] { awaken }, new ProgressionGateSO[] { kinOwnedGates[attr.Type] }, kinCost,
                kinAreaArray,
                $"[TBD] {Capitalize(attr.Key)} Kin Area", $"[TBD] {attr.KinAreaDescEn}",
                $"[미정] {attr.KoreanName} 새끼용 지역", $"[미정] {attr.KinAreaDescKo}");

            orderedNodes.Add(awaken);
            orderedNodes.Add(active);
            orderedNodes.Add(enhance);
            orderedNodes.Add(ultimate);
            orderedNodes.Add(kinTower);
            orderedNodes.Add(kinArea);
        }

        // 6. 트리
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

        // 7. 공통 UI 로컬 키(헤더/상태/실패사유/코스트 포맷)
        AddCommonLocRows();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AppendLocalizationCsv();
        WriteSheetExportFragments();

        Debug.Log(
            $"[DragonSkillTreeAssetGenerator] 노드 {orderedNodes.Count}개, 로컬 키 {_locRows.Count}개 생성 완료. " +
            $"'{SHEET_EXPORT_FOLDER}/' 아래 시트 반영용 CSV 조각을 확인하세요. " +
            "생명 액티브(바리케이드)는 SkillSO를 만들지 않았습니다 - 팀 에셋 제공 후 후속 작업.");
    }

    // 속성별 각성 패시브 효과 - 타입이 서로 달라 switch로 분기한다(각 효과 SO의 필드 스키마가 다름).
    private static DragonSkillEffectSO CreateAwakenEffect(
        AttributeSpec attr,
        MoveSpeedStatusSO iceSlowStatus,
        DamageOverTimeStatusSO fireBurnStatus)
    {
        string assetPath = $"{DATA_FOLDER}/DE_Awaken_{Capitalize(attr.Key)}.asset";

        switch (attr.Type)
        {
            case DragonType.Ice:
                return CreateOrReplace<DragonTowerHitStatusEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_status").objectReferenceValue = iceSlowStatus;
                });

            case DragonType.Fire:
                return CreateOrReplace<DragonSpawnStatusEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_status").objectReferenceValue = fireBurnStatus;
                });

            case DragonType.Time:
                return CreateOrReplace<DragonTowerStatEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_attackSpeedBonusRatio").floatValue = TIME_ATTACK_SPEED_BONUS;
                    so.FindProperty("_damageBonusRatio").floatValue = 0f;
                });

            // "광산 생산량 증가"의 광산은 실제 채석장(RPD_Quarry, Stone 생산) - attr.Slime/Specialized만
            // 넣으면 RockSlime·PhilosopherStone만 오르고 정작 채석장의 기본 Stone 생산은 그대로다.
            case DragonType.Stone:
                return CreateOrReplace<DragonYieldEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_targetResources").intValue = (int)(ResourceType.Stone | attr.Slime | attr.Specialized);
                    so.FindProperty("_bonusRatio").floatValue = YIELD_BONUS_RATIO;
                });

            // "농장 생산량 증가"의 농장은 실제 농장(RPD_FarmField, Food 생산) - GrassSlime만 넣으면
            // 정작 농장의 기본 Food 생산은 안 오른다.
            case DragonType.Life:
            default:
                return CreateOrReplace<DragonYieldEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_targetResources").intValue = (int)(ResourceType.Food | attr.Slime);
                    so.FindProperty("_bonusRatio").floatValue = YIELD_BONUS_RATIO;
                });
        }
    }

    // 새끼용 지역형(B 슬롯) 효과 - 얼음은 대상 시스템이 없어 null(노드에 효과 미배정)로 남긴다.
    private static DragonSkillEffectSO CreateKinAreaEffect(AttributeSpec attr)
    {
        string assetPath = $"{DATA_FOLDER}/DE_KinArea_{Capitalize(attr.Key)}.asset";

        switch (attr.Type)
        {
            case DragonType.Ice:
                return null; // 강 결빙 - 지형 기반 배치 제한 시스템 자체가 없어 stub.

            case DragonType.Fire:
                return CreateOrReplace<DragonVisionEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_bonusRadius").intValue = FIRE_KIN_VISION_BONUS_RADIUS;
                });

            case DragonType.Time:
                return CreateOrReplace<KinConquestEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_daysReduction").intValue = TIME_KIN_CONQUEST_DAYS_REDUCTION;
                });

            case DragonType.Stone:
            case DragonType.Life:
            default:
                return CreateOrReplace<KinAreaYieldEffectSO>(assetPath, so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_bonusRatio").floatValue = KIN_AREA_YIELD_BONUS_RATIO;
                });
        }
    }

    private static List<ResourceAmount> BuildCost(AttributeSpec attr, int slimeAmount, int specializedAmount)
    {
        var cost = new List<ResourceAmount> { new ResourceAmount { Type = attr.Slime, Amount = slimeAmount } };

        if (specializedAmount > 0 && attr.Specialized != ResourceType.None)
        {
            cost.Add(new ResourceAmount { Type = attr.Specialized, Amount = specializedAmount });
        }

        return cost;
    }

    private static DragonSkillNodeData CreateNode(
        AttributeSpec attr,
        string kindKey,
        DragonNodeKind kind,
        DragonSkillNodeData[] prerequisites,
        ProgressionGateSO[] gates,
        List<ResourceAmount> cost,
        DragonSkillEffectSO[] effects,
        string nameEn,
        string descEn,
        string nameKo,
        string descKo)
    {
        string nodeId = $"dragon_{attr.Key}_{kindKey}";
        string nameLocKey = $"dragon_node_{attr.Key}_{kindKey}_name";
        string descLocKey = $"dragon_node_{attr.Key}_{kindKey}_desc";
        string assetPath = $"{DATA_FOLDER}/DN_{Capitalize(attr.Key)}_{Capitalize(kindKey)}.asset";

        DragonSkillNodeData node = CreateOrReplace<DragonSkillNodeData>(assetPath, so =>
        {
            so.FindProperty("_nodeId").stringValue = nodeId;
            so.FindProperty("_nameLocKey").stringValue = nameLocKey;
            so.FindProperty("_descriptionLocKey").stringValue = descLocKey;
            so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
            so.FindProperty("_kind").enumValueIndex = (int)kind;

            AssignObjectArray(so.FindProperty("_prerequisites"), prerequisites);
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

        AddLocRow(nameLocKey, nameEn, nameKo);
        AddLocRow(descLocKey, descEn, descKo);

        return node;
    }

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

            newLines.Add(FormatCsvRow(row.Id, valueSelector(row)));
        }

        if (newLines.Count == 0)
        {
            return;
        }

        // 기존 파일이 줄바꿈 없이 끝나면(예: 수기 편집된 2줄짜리 CSV) 그대로 append할 경우
        // 마지막 줄과 새 줄이 한 줄로 붙어버려 CSV가 깨진다 - 반드시 먼저 줄바꿈을 보정한다.
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
