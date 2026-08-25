using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using UnityEditor;
using UnityEngine;

// 연구 트리 SO 에셋 27개(3갈래 × 5티어) + 트리 1개를 코드로 생성한다.
// 손으로 .asset YAML을 작성하지 않기 위한 에디터 전용 도구 - CLAUDE.md 커밋규칙 §5(에디터 전용 코드)
// 리터럴 제한 예외에 해당한다. 재실행 시 기존 에셋을 재사용(GUID 유지)해 멱등적으로 동작한다.
//
// 노드·코스트·선행관계는 전부 Docs/Sangwook/연구트리_로드맵.md §5(= 연구트리_시각화.html DATA)를
// 그대로 옮긴 것이며 밸런싱 대상이다.
//
// 로드맵 코스트 표기 중 아래 두 가지는 자원 코스트에서 생략한다(RP 코스트는 그대로 반영):
//  - 鑛(광물): ResourceType에 대응 자원이 없다(기본 자원은 Food/Wood/Stone 3종).
//  - 특화·슬: "특화 다량", "특화×2종"처럼 종류·수량이 기획 [미정]이라 임의 확정하지 않는다.
//    (CLAUDE.md "기획서의 [미정] 항목은 임의로 확정 구현 금지")
public static class ResearchTreeAssetGenerator
{
    private const string DATA_FOLDER = "Assets/Data/Research";
    private const string LANDMARK_DATA_FOLDER = "Assets/Data/LandmarkData";
    private const string SHEET_EXPORT_FOLDER = "Docs";

    private struct NodeSpec
    {
        public string NodeId;
        public string AssetName;
        public ResearchBranch Branch;
        public int Tier;
        public int ResearchPointCost;
        public ResourceAmount[] ResourceCost;
        public string[] PrerequisiteIds;
        public string EffectAssetName; // null이면 효과 미구현(stub) - 로드맵 §8과 동일한 취급
        public string[] EffectAssetNames;

        // 역설계 노드 전용. 이 랜드마크를 점령해야 연구가 열린다. null이면 조건 없음.
        // 에셋 경로는 LANDMARK_DATA_FOLDER 기준이다.
        public string RequiredLandmarkAssetName;

        public string NameEn;
        public string NameKo;
        public string DescEn;
        public string DescKo;
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

    private static ResourceAmount[] Cost(params ResourceAmount[] amounts) => amounts;

    private static ResourceAmount Amount(ResourceType type, int amount) =>
        new ResourceAmount { Type = type, Amount = amount };

    private static string[] After(params string[] nodeIds) => nodeIds;

    private static string[] Effects(params string[] effectAssetNames) => effectAssetNames;

    // 티어는 Docs/연구트리_개편안.md §2의 확정 배치다. UI가 (갈래 × 티어) 격자로 자동 배치하고
    // 한 칸의 폭이 갈래 열 폭(500)을 넘으면 옆 갈래를 침범하므로 칸당 3개가 상한이다.
    // 배치를 바꿀 때는 그 상한을 먼저 확인할 것.
    //
    // RP 코스트는 티어 곡선(10 / 25 / 50 / 90 / 150)을 그대로 따른다 - 티어가 바뀐 노드는
    // 새 티어의 값으로 맞췄다(밸런싱 대상).
    private static NodeSpec[] BuildNodeSpecs() => new[]
    {
        // ================================ 타워 갈래 (12) ================================
        // --- T1 ---
        new NodeSpec
        {
            NodeId = "tower_damage", AssetName = "RN_TowerDamage",
            Branch = ResearchBranch.Tower, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_TowerDamageIncrease",
            NameEn = "[TBD] Basic Training I", NameKo = "[미정] 기본 훈련 I",
            DescEn = "[TBD] All tower damage +10%.", DescKo = "[미정] 전 타워 공격력 +10%",
        },
        new NodeSpec
        {
            NodeId = "tower_range_1", AssetName = "RN_TowerRange1",
            Branch = ResearchBranch.Tower, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_TowerRangeIncrease",
            NameEn = "[TBD] Range Extension I", NameKo = "[미정] 사거리 확장 I",
            DescEn = "[TBD] Tower range +15%.", DescKo = "[미정] 사거리 +15%",
        },
        new NodeSpec
        {
            NodeId = "tower_firerate_1", AssetName = "RN_TowerFireRate1",
            Branch = ResearchBranch.Tower, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_TowerFireRateIncrease",
            NameEn = "[TBD] Rate of Fire I", NameKo = "[미정] 연사 개량 I",
            DescEn = "[TBD] Attack speed +12%.", DescKo = "[미정] 공격속도 +12%",
        },

        // --- T2 ---
        // 속성 타워 해금을 T2에 두는 것이 배치의 핵심이다. T3로 내리면 속성 강화 노드들이
        // T4로 밀리고, 궁극 노드와 같은 칸에서 선행 간선이 생긴다.
        //
        // **생명 타워가 여기서 열리는 것은 의도다(팀 확정).** 설정상 생명은 어미용 5속성의
        // 하나이므로 속성 타워로 분류한다 - 그래서 TD_LifeTower는 ElementalTowerData이고,
        // 랜드마크 게이트가 걸린 tower_reverse_engineering이 아니라 이 노드가 해금한다.
        // 옛 배치(역설계가 생명 타워를 해금)를 기억하고 되돌리지 말 것.
        //
        // 파급 두 가지:
        //  - TowerTargetFilter.ElementalTowers를 쓰는 RE_ElementalTowerDamage(궁극의 속성)가
        //    생명 타워에도 걸린다.
        //  - DragonType.Life는 화염·얼음·암석과 값이 달라 속성별 강화(RE_FireTowerDamage 등,
        //    MatchingElement)에는 걸리지 않는다.
        new NodeSpec
        {
            NodeId = "tower_elemental_unlock", AssetName = "RN_TowerElementalUnlock",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetNames = Effects(
                "RE_FireTowerUnlock", "RE_IceTowerUnlock", "RE_StoneTowerUnlock",
                "RE_TimeTowerUnlock", "RE_LifeTowerUnlock"),
            NameEn = "[TBD] Elemental Tower Unlock", NameKo = "[미정] 속성 타워 해금",
            DescEn = "[TBD] Allows building biome-specialized towers.",
            DescKo = "[미정] 바이옴 특화 타워 건설 가능",
        },
        new NodeSpec
        {
            NodeId = "tower_reverse_engineering", AssetName = "RN_TowerReverseEngineering",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("tower_elemental_unlock"),
            // 일단 랜드마크 게이트를 해제한다 - 역설계 연구를 완료하는 것만으로
            // 은신·영혼·강화·가시 타워 4종이 해금된다.
            // 랜드마크(타워 원형) 점령 경로를 되살릴 때 아래 줄을 복구할 것.
            // RequiredLandmarkAssetName = "LM_TowerPrototype",
            EffectAssetNames = Effects(
                "RE_StealthTowerUnlock", "RE_SoulTowerUnlock",
                "RE_EnhancementTowerUnlock", "RE_ThornTowerUnlock"),
            NameEn = "[TBD] Reverse Engineering", NameKo = "[미정] 역설계",
            DescEn = "[TBD] Analyze the captured tower prototype to unlock special towers.",
            DescKo = "[미정] 점령한 타워 원형을 분석해 특수 타워를 해금한다.",
        },
        new NodeSpec
        {
            NodeId = "tower_manpower_1", AssetName = "RN_TowerManpower1",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("tower_damage"),
            EffectAssetName = "RE_TowerManpowerReduction",
            NameEn = "[TBD] Manpower Efficiency I", NameKo = "[미정] 인력 효율 I",
            DescEn = "[TBD] Tower population requirement -1 (min 1).",
            DescKo = "[미정] 타워 필요 인구 -1 (하한 1)",
        },
        new NodeSpec
        {
            NodeId = "tower_anti_air", AssetName = "RN_TowerAntiAir",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 30), Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = After("tower_range_1"),
            EffectAssetName = "RE_AntiAirTowerUnlock",
            NameEn = "[TBD] Anti-Air Tower", NameKo = "[미정] 대공 타워",
            DescEn = "[TBD] Unlocks the anti-air tower.", DescKo = "[미정] 대공 타워를 해금한다",
        },

        // --- T3: 속성별 강화. TowerTargetFilter로 그 속성 타워만 골라 올린다. ---
        new NodeSpec
        {
            NodeId = "tower_fire_damage", AssetName = "RN_TowerFireDamage",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("tower_elemental_unlock"),
            EffectAssetName = "RE_FireTowerDamage",
            NameEn = "[TBD] Fire Tower Mastery", NameKo = "[미정] 화염 타워 강화",
            DescEn = "[TBD] Fire tower damage +20%.", DescKo = "[미정] 화염 타워 공격력 +20%",
        },
        new NodeSpec
        {
            NodeId = "tower_ice_damage", AssetName = "RN_TowerIceDamage",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("tower_elemental_unlock"),
            EffectAssetName = "RE_IceTowerDamage",
            NameEn = "[TBD] Ice Tower Mastery", NameKo = "[미정] 얼음 타워 강화",
            DescEn = "[TBD] Ice tower damage +20%.", DescKo = "[미정] 얼음 타워 공격력 +20%",
        },
        new NodeSpec
        {
            NodeId = "tower_stone_damage", AssetName = "RN_TowerStoneDamage",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("tower_elemental_unlock"),
            EffectAssetName = "RE_StoneTowerDamage",
            NameEn = "[TBD] Stone Tower Mastery", NameKo = "[미정] 암석 타워 강화",
            DescEn = "[TBD] Stone tower damage +20%.", DescKo = "[미정] 암석 타워 공격력 +20%",
        },

        // --- T4 ---
        new NodeSpec
        {
            // 최대체력은 TowerAttack이 pull하지 않는 유일한 스탯이라 씬의 TowerMaxHealthApplier가
            // 밤 시작에 push한다 - 그 적용기와 TowerMaxHealthMultiplierComposite가 씬에 있어야 한다.
            NodeId = "tower_armor", AssetName = "RN_TowerArmor",
            Branch = ResearchBranch.Tower, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("tower_range_1", "tower_firerate_1"),
            EffectAssetName = "RE_TowerArmor",
            NameEn = "[TBD] Armored Tower", NameKo = "[미정] 중갑 타워",
            DescEn = "[TBD] Tower max health +20%.", DescKo = "[미정] 타워 최대 체력 +20%",
        },
        // --- T5 ---
        new NodeSpec
        {
            NodeId = "tower_elemental_master", AssetName = "RN_TowerElementalMaster",
            Branch = ResearchBranch.Tower, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(),
            PrerequisiteIds = After(
                "tower_reverse_engineering", "tower_fire_damage", "tower_ice_damage",
                "tower_stone_damage"),
            EffectAssetName = "RE_ElementalTowerDamage",
            NameEn = "[TBD] Ultimate Element", NameKo = "[미정] 궁극의 속성",
            DescEn = "[TBD] All elemental tower damage +20%.",
            DescKo = "[미정] 모든 속성 타워 공격력 +20%",
        },

        // ================================ 생산 갈래 (13) ================================
        // --- T1 ---
        new NodeSpec
        {
            NodeId = "grass_cultivation", AssetName = "RN_GrassCultivation",
            Branch = ResearchBranch.Production, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Food, 10)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_GrassYieldMultiplier",
            NameEn = "[TBD] Grassland Cultivation", NameKo = "[미정] 초원 경작",
            DescEn = "[TBD] Farm and quarry yield +20%.", DescKo = "[미정] 농장·광산 생산량 +20%",
        },
        new NodeSpec
        {
            NodeId = "production_outer_basic", AssetName = "RN_ProductionOuterBasic",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = After("grass_cultivation"),
            EffectAssetName = "RE_OuterBasicResourceUnlock",
            NameEn = "[TBD] Outer Basic Harvesting", NameKo = "[미정] 외곽 기초자원 채집",
            DescEn = "[TBD] Unlocks food, wood, and stone harvesting in outer biomes.",
            DescKo = "[미정] 외곽 바이옴 식량·목재·석재 채집 해금",
        },

        // --- T2: 자원별 인력 절감. 효과는 2차분(자원/건물별 필터 추가 후). ---
        new NodeSpec
        {
            NodeId = "production_food_1", AssetName = "RN_ProductionFood1",
            Branch = ResearchBranch.Production, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Food, 20)),
            PrerequisiteIds = After("grass_cultivation"),
            EffectAssetName = "RE_FoodManpowerReduction",
            NameEn = "[TBD] Food Production I", NameKo = "[미정] 식량 생산 강화 I",
            DescEn = "[TBD] Food facility population requirement -1.",
            DescKo = "[미정] 식량 생산에 필요한 인구 -1",
        },
        new NodeSpec
        {
            NodeId = "production_wood_1", AssetName = "RN_ProductionWood1",
            Branch = ResearchBranch.Production, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = After("grass_cultivation"),
            EffectAssetName = "RE_WoodManpowerReduction",
            NameEn = "[TBD] Lumber Production I", NameKo = "[미정] 목재 생산 강화 I",
            DescEn = "[TBD] Lumber facility population requirement -1.",
            DescKo = "[미정] 목재 생산에 필요한 인구 -1",
        },
        new NodeSpec
        {
            NodeId = "production_stone_1", AssetName = "RN_ProductionStone1",
            Branch = ResearchBranch.Production, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = After("grass_cultivation"),
            EffectAssetName = "RE_StoneManpowerReduction",
            NameEn = "[TBD] Stone Production I", NameKo = "[미정] 석재 생산 강화 I",
            DescEn = "[TBD] Stone facility population requirement -1.",
            DescKo = "[미정] 석재 생산에 필요한 인구 -1",
        },

        // --- T3: 자원별 생산량. 1차분으로 효과가 붙는다. ---
        new NodeSpec
        {
            NodeId = "production_food_2", AssetName = "RN_ProductionFood2",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Food, 40)),
            PrerequisiteIds = After("production_food_1"),
            EffectAssetName = "RE_FoodYieldMultiplier",
            NameEn = "[TBD] Food Production II", NameKo = "[미정] 식량 생산 강화 II",
            DescEn = "[TBD] Food yield +10%.", DescKo = "[미정] 식량 생산량 +10%",
        },
        new NodeSpec
        {
            NodeId = "production_wood_2", AssetName = "RN_ProductionWood2",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Wood, 40)),
            PrerequisiteIds = After("production_wood_1"),
            EffectAssetName = "RE_WoodYieldMultiplier",
            NameEn = "[TBD] Lumber Production II", NameKo = "[미정] 목재 생산 강화 II",
            DescEn = "[TBD] Lumber yield +10%.", DescKo = "[미정] 목재 생산량 +10%",
        },
        new NodeSpec
        {
            NodeId = "production_stone_2", AssetName = "RN_ProductionStone2",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("production_stone_1"),
            EffectAssetName = "RE_StoneYieldMultiplier",
            NameEn = "[TBD] Stone Production II", NameKo = "[미정] 석재 생산 강화 II",
            DescEn = "[TBD] Stone yield +10%.", DescKo = "[미정] 석재 생산량 +10%",
        },

        // --- T4 ---
        new NodeSpec
        {
            NodeId = "production_specialized", AssetName = "RN_ProductionSpecialized",
            Branch = ResearchBranch.Production, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화 다량 - 기획 [미정]
            PrerequisiteIds = After("production_outer_basic"),
            EffectAssetName = "RE_SpecializedYieldMultiplier",
            NameEn = "[TBD] Specialized Facilities", NameKo = "[미정] 특화 생산 시설",
            DescEn = "[TBD] Biome specialized resource yield +20%.",
            DescKo = "[미정] 바이옴 특화 자원 생산 효율 +20%",
        },
        new NodeSpec
        {
            NodeId = "production_resource_mastery", AssetName = "RN_ProductionResourceMastery",
            Branch = ResearchBranch.Production, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Wood, 30), Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("production_wood_2", "production_stone_2"),
            EffectAssetName = "RE_WoodStoneYieldMultiplier",
            NameEn = "[TBD] Resource Mastery I", NameKo = "[미정] 자원 생산 강화 I",
            DescEn = "[TBD] Lumber and stone yield +10%.", DescKo = "[미정] 목재·석재 생산량 +10%",
        },
        new NodeSpec
        {
            // 선행 production_specialized가 같은 티어다(같은 칸 안 간선).
            NodeId = "production_slime_farm", AssetName = "RN_ProductionSlimeFarm",
            Branch = ResearchBranch.Production, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화×2종 - 기획 [미정]
            PrerequisiteIds = After("production_specialized", "production_food_2"),
            EffectAssetName = "RE_SlimeYieldMultiplier",
            NameEn = "[TBD] Slime Farming", NameKo = "[미정] 슬라임 양식",
            DescEn = "[TBD] Specialized-area slime yield +25%.",
            DescKo = "[미정] 특화 지역 슬라임 생산 +25%",
        },

        // --- T5 ---
        new NodeSpec
        {
            NodeId = "production_resource_mastery_2", AssetName = "RN_ProductionResourceMastery2",
            Branch = ResearchBranch.Production, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(),
            PrerequisiteIds = After("production_food_2", "production_resource_mastery"),
            EffectAssetName = "RE_SpecializedYieldMultiplier2",
            NameEn = "[TBD] Resource Mastery II", NameKo = "[미정] 자원 생산 강화 II",
            DescEn = "[TBD] Specialized resource yield +20%.",
            DescKo = "[미정] 특화 자원 생산량 +20%",
        },
        new NodeSpec
        {
            // 선행 production_resource_mastery_2가 같은 티어다(같은 칸 안 간선).
            NodeId = "production_optimize", AssetName = "RN_ProductionOptimize",
            Branch = ResearchBranch.Production, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(),
            PrerequisiteIds = After("production_slime_farm", "production_resource_mastery_2"),
            EffectAssetName = "RE_ProductionManpowerReduction",
            NameEn = "[TBD] Production Optimization", NameKo = "[미정] 생산 최적화",
            DescEn = "[TBD] Production facility population requirement -1 (min 1).",
            DescKo = "[미정] 생산 시설 필요 인구 -1 (하한 1)",
        },

        // ================================ 편의 갈래 (12) ================================
        // --- T1 ---
        new NodeSpec
        {
            NodeId = "convenience_scout_1", AssetName = "RN_ConvenienceScout1",
            Branch = ResearchBranch.Convenience, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 15)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_ScoutRadius1",
            NameEn = "[TBD] Scouting I", NameKo = "[미정] 정찰 I",
            DescEn = "[TBD] Fog-of-war vision radius +1 step.",
            DescKo = "[미정] 전장의 안개 가시 반경 +1단계",
        },
        new NodeSpec
        {
            // 지금은 밤 재활성화가 무조건 동작한다. 이 노드로 잠그면 연구 전에는 부활하지 않는다 -
            // convenience_tower_move와 같은 종류의 "연구 전 제약 강화"라 기획 확정이 필요하다.
            NodeId = "convenience_tower_regeneration_unlock",
            AssetName = "RN_ConvenienceTowerRegenerationUnlock",
            Branch = ResearchBranch.Convenience, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_TowerCombatRepairUnlock",
            NameEn = "[TBD] Tower Repair", NameKo = "[미정] 타워 수리",
            DescEn = "[TBD] Allows towers to heal and reactivate during night battles.",
            DescKo = "[미정] 밤 전투 중 타워 회복 및 재활성화 가능",
        },

        // --- T2 ---
        new NodeSpec
        {
            NodeId = "convenience_lab_expand_1", AssetName = "RN_ConvenienceLabExpand1",
            Branch = ResearchBranch.Convenience, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20), Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = After("convenience_scout_1"),
            EffectAssetName = "RE_LabCapacityExpand1",
            NameEn = "[TBD] Lab Expansion I", NameKo = "[미정] 연구소 증축 I",
            DescEn = "[TBD] Research lab population cap +1.",
            DescKo = "[미정] 연구소 배치 가능 최대 인구 +1",
        },
        new NodeSpec
        {
            NodeId = "convenience_tower_move", AssetName = "RN_ConvenienceTowerMove",
            Branch = ResearchBranch.Convenience, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("convenience_tower_regeneration_unlock"),
            EffectAssetName = "RE_MoveAllowance",
            NameEn = "[TBD] Tower Relocation", NameKo = "[미정] 타워 이동 해금",
            DescEn = "[TBD] Move towers without demolishing.",
            DescKo = "[미정] 철거 없이 타워 이동 가능",
        },
        new NodeSpec
        {
            NodeId = "convenience_castle_regen_1", AssetName = "RN_ConvenienceCastleRegen1",
            Branch = ResearchBranch.Convenience, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = After("convenience_tower_regeneration_unlock"),
            EffectAssetName = "RE_CastleDailyRegen",
            NameEn = "[TBD] Castle Regeneration I", NameKo = "[미정] 성 자동 회복 I",
            DescEn = "[TBD] Castle heals 10 health each day.",
            DescKo = "[미정] 매일 낮 성 체력 10 회복",
        },

        // --- T3 ---
        new NodeSpec
        {
            NodeId = "convenience_lab_expand_2", AssetName = "RN_ConvenienceLabExpand2",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("convenience_lab_expand_1"),
            EffectAssetName = "RE_LabBuildLimitExpand",
            NameEn = "[TBD] Lab Expansion II", NameKo = "[미정] 연구소 증축 II",
            DescEn = "[TBD] Research lab build limit +1.",
            DescKo = "[미정] 건설 가능한 연구소 +1",
        },
        new NodeSpec
        {
            NodeId = "convenience_expedition_logistics", AssetName = "RN_ConvenienceExpeditionLogistics",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Wood, 40), Amount(ResourceType.Food, 20)),
            PrerequisiteIds = After("convenience_scout_1"),
            EffectAssetName = "RE_ExpeditionLogistics",
            NameEn = "[TBD] Expedition Logistics", NameKo = "[미정] 원정 물류",
            DescEn = "[TBD] Conquest expedition resource cost -20%.",
            DescKo = "[미정] 점령 출격 자원 소모량 20% 감소",
        },
        new NodeSpec
        {
            // 승리 조건(4포탈 동시 봉인)의 관문. 봉인석 본체는 이미 구현돼 있고
            // (PortalSealManager·SealStone) 해금 접점 ISealStoneUnlockQuery만 비어 있었다.
            NodeId = "convenience_seal_stone", AssetName = "RN_ConvenienceSealStone",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 60)),
            PrerequisiteIds = After("convenience_castle_regen_1"),
            EffectAssetName = "RE_SealStoneUnlock",
            NameEn = "[TBD] Seal Stone Research", NameKo = "[미정] 봉인석 연구",
            DescEn = "[TBD] Unlocks the seal stone that can seal all four portals.",
            DescKo = "[미정] 4개의 포탈을 봉인할 수 있는 봉인석 건설을 해금합니다.",
        },

        // --- T4 ---
        new NodeSpec
        {
            NodeId = "convenience_castle_regen_2", AssetName = "RN_ConvenienceCastleRegen2",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("convenience_seal_stone"),
            EffectAssetName = "RE_CastleDailyRegen2",
            NameEn = "[TBD] Castle Regeneration II", NameKo = "[미정] 성 자동 회복 II",
            DescEn = "[TBD] Castle heals an extra 10 health each day (20 total).",
            DescKo = "[미정] 매일 낮 성 체력 10 추가 회복 (합계 20)",
        },
        new NodeSpec
        {
            NodeId = "convenience_lab_expand_3", AssetName = "RN_ConvenienceLabExpand3",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)),
            PrerequisiteIds = After("convenience_lab_expand_2"),
            EffectAssetName = "RE_LabCapacityExpand3",
            NameEn = "[TBD] Lab Expansion III", NameKo = "[미정] 연구소 증축 III",
            DescEn = "[TBD] Research lab population cap +1.",
            DescKo = "[미정] 연구소 배치 가능 최대 인구 +1",
        },
        new NodeSpec
        {
            NodeId = "convenience_scout_2", AssetName = "RN_ConvenienceScout2",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛30 - 대응 자원 없음
            PrerequisiteIds = After("convenience_expedition_logistics"),
            EffectAssetName = "RE_ScoutRadius2",
            NameEn = "[TBD] Scouting II", NameKo = "[미정] 정찰 II",
            DescEn = "[TBD] Greatly extends vision and marks landmarks.",
            DescKo = "[미정] 가시 반경 대폭 확장 + 랜드마크 표시",
        },
        new NodeSpec
        {
            NodeId = "convenience_expedition_master", AssetName = "RN_ConvenienceExpeditionMaster",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Food, 40)),
            PrerequisiteIds = After("convenience_expedition_logistics", "convenience_tower_move"),
            EffectAssetName = "RE_ExpeditionMaster",
            NameEn = "[TBD] Expert Expedition Corps", NameKo = "[미정] 전문 원정대",
            DescEn = "[TBD] Expedition population requirement -1.",
            DescKo = "[미정] 원정에 필요한 인구 -1",
        },

        // --- T5 ---
        new NodeSpec
        {
            NodeId = "convenience_expedition_conqueror", AssetName = "RN_ConvenienceExpeditionConqueror",
            Branch = ResearchBranch.Convenience, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(),
            PrerequisiteIds = After(
                "convenience_lab_expand_3", "convenience_expedition_master",
                "convenience_castle_regen_2"),
            EffectAssetName = "RE_ExpeditionConqueror",
            NameEn = "[TBD] Conqueror", NameKo = "[미정] 정복자",
            DescEn = "[TBD] Removes Food, Wood, and Stone costs from conquest expeditions. Population and duration stay unchanged.",
            DescKo = "[미정] 점령 원정의 식량, 목재, 석재 비용 제거. 필요 인구와 소요일은 유지",
        },
    };

    [MenuItem("TowerAndDragon/Research/Generate Research Tree Assets")]
    public static void Generate()
    {
        _locRows.Clear();
        EnsureFolder(DATA_FOLDER);

        NodeSpec[] specs = BuildNodeSpecs();

        // 1. 노드 27개 생성(선행은 아직 비워 둔다 - 서로를 참조하므로 2패스로 채운다)
        var nodesById = new Dictionary<string, ResearchNodeData>();
        var orderedNodes = new List<ResearchNodeData>();

        foreach (NodeSpec spec in specs)
        {
            ResearchNodeData node = CreateNode(spec);
            nodesById[spec.NodeId] = node;
            orderedNodes.Add(node);
        }

        // 2. 선행관계 배선
        foreach (NodeSpec spec in specs)
        {
            var prerequisites = new List<ResearchNodeData>();

            foreach (string prerequisiteId in spec.PrerequisiteIds)
            {
                if (nodesById.TryGetValue(prerequisiteId, out ResearchNodeData prerequisite))
                {
                    prerequisites.Add(prerequisite);
                    continue;
                }

                Debug.LogError(
                    $"[ResearchTreeAssetGenerator] '{spec.NodeId}'의 선행 노드 '{prerequisiteId}'를 찾지 못했습니다.");
            }

            var serializedObject = new SerializedObject(nodesById[spec.NodeId]);
            AssignObjectArray(serializedObject.FindProperty("_prerequisites"), prerequisites.ToArray());
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nodesById[spec.NodeId]);
        }

        // 3. 트리
        CreateOrReplace<ResearchTreeData>(
            $"{DATA_FOLDER}/ResearchTree.asset",
            so => AssignObjectArray(so.FindProperty("_nodes"), orderedNodes.ToArray()));

        // 4. 공통 UI 로컬 키(헤더/상태/티어 캡션/갈래 이름 등)
        AddCommonLocRows();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AppendLocalizationCsv();
        WriteSheetExportFragments();

        Debug.Log(
            $"[ResearchTreeAssetGenerator] 노드 {orderedNodes.Count}개, 로컬 키 {_locRows.Count}개 생성 완료. " +
            $"'{SHEET_EXPORT_FOLDER}/' 아래 시트 반영용 CSV 조각을 확인하세요.");
    }

    private static ResearchNodeData CreateNode(NodeSpec spec)
    {
        string nameLocKey = $"research_node_{spec.NodeId}_name";
        string descLocKey = $"research_node_{spec.NodeId}_description";
        string assetPath = $"{DATA_FOLDER}/{spec.AssetName}.asset";

        ResearchEffectSO[] effects = ResolveEffects(spec);

        ResearchNodeData node = CreateOrReplace<ResearchNodeData>(assetPath, so =>
        {
            so.FindProperty("_nodeId").stringValue = spec.NodeId;
            so.FindProperty("_nameLocKey").stringValue = nameLocKey;
            so.FindProperty("_descriptionLocKey").stringValue = descLocKey;
            so.FindProperty("_branch").enumValueIndex = (int)spec.Branch;
            so.FindProperty("_tier").intValue = spec.Tier;
            so.FindProperty("_researchPointCost").intValue = spec.ResearchPointCost;

            AssignObjectArray(so.FindProperty("_effects"), effects);
            so.FindProperty("_requiredLandmark").objectReferenceValue =
                ResolveRequiredLandmark(spec.RequiredLandmarkAssetName);

            SerializedProperty costProp = so.FindProperty("_resourceCost");
            costProp.arraySize = spec.ResourceCost.Length;
            for (int i = 0; i < spec.ResourceCost.Length; i++)
            {
                SerializedProperty element = costProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").intValue = (int)spec.ResourceCost[i].Type;
                element.FindPropertyRelative("Amount").intValue = spec.ResourceCost[i].Amount;
            }
        });

        AddLocRow(nameLocKey, spec.NameEn, spec.NameKo);
        AddLocRow(descLocKey, spec.DescEn, spec.DescKo);

        return node;
    }

    // 랜드마크 데이터도 효과 SO와 마찬가지로 이미 있는 에셋을 참조만 한다.
    private static LandmarkDataSO ResolveRequiredLandmark(string landmarkAssetName)
    {
        if (string.IsNullOrEmpty(landmarkAssetName))
        {
            return null;
        }

        var landmark = AssetDatabase.LoadAssetAtPath<LandmarkDataSO>(
            $"{LANDMARK_DATA_FOLDER}/{landmarkAssetName}.asset");

        if (landmark == null)
        {
            Debug.LogWarning(
                $"[ResearchTreeAssetGenerator] 랜드마크 에셋 '{landmarkAssetName}'을 찾지 못해 " +
                $"조건을 비워 둡니다 - 역설계 노드가 랜드마크 없이 열립니다.");
        }

        return landmark;
    }

    // 이미 만들어져 있던 효과 SO(RE_*)는 그대로 재사용한다 - 새로 만들지 않는다.
    private static ResearchEffectSO[] ResolveEffects(NodeSpec spec)
    {
        var effectAssetNames = new List<string>();

        if (!string.IsNullOrEmpty(spec.EffectAssetName))
        {
            effectAssetNames.Add(spec.EffectAssetName);
        }

        if (spec.EffectAssetNames != null)
        {
            effectAssetNames.AddRange(spec.EffectAssetNames);
        }

        if (effectAssetNames.Count == 0)
        {
            return Array.Empty<ResearchEffectSO>();
        }

        var effects = new List<ResearchEffectSO>();
        foreach (string effectAssetName in effectAssetNames)
        {
            var effect = AssetDatabase.LoadAssetAtPath<ResearchEffectSO>(
                $"{DATA_FOLDER}/{effectAssetName}.asset");

            if (effect == null)
            {
                Debug.LogWarning(
                    $"[ResearchTreeAssetGenerator] 효과 에셋 '{effectAssetName}'을 찾지 못해 건너뜁니다.");
                continue;
            }

            effects.Add(effect);
        }

        return effects.ToArray();
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

    private static T CreateOrReplace<T>(string assetPath, Action<SerializedObject> configure)
        where T : ScriptableObject
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
        AddLocRow("research_window_header", "Research", "연구");
        AddLocRow("research_button", "Research!", "연구!");

        AddLocRow("research_branch_tower", "Tower", "타워");
        AddLocRow("research_branch_production", "Production", "생산");
        AddLocRow("research_branch_convenience", "Convenience", "편의");

        // 티어 행 왼쪽 캡션 - 시각화 HTML의 TIERS[].cap과 동일.
        AddLocRow("research_tier_caption_1", "Game Start", "게임 시작");
        AddLocRow("research_tier_caption_2", "End of Cycle 1", "1주기 종료");
        AddLocRow("research_tier_caption_3", "End of Cycle 2", "2주기 종료");
        // T5는 T4와 같은 시점에 열리므로 캡션 키를 공유한다(research_tier_caption_5 없음).
        AddLocRow("research_tier_caption_4", "End of Cycle 3", "3주기 종료");

        AddLocRow("research_tier_label", "T{0}", "T{0}");
        AddLocRow("research_tier_locked_badge", "Cycle {0}", "{0}주기");

        // 아래 키들은 en_us.csv에 이미 있으나 ko_kr.csv에는 없다.
        // AppendLocalizationCsv가 파일별로 기존 Id를 건너뛰므로 한국어만 채워진다.
        AddLocRow("research_rp_amount", "Research Points: {0}", "연구 포인트: {0}");
        AddLocRow("research_population_amount", "Research Population: {0}/{1}", "연구 인구: {0}/{1}");
        AddLocRow("research_rp_cost", "RP {0}", "RP {0}");
        AddLocRow("research_resource_cost", "{0} {1}", "{0} {1}");
        AddLocRow("research_unknown_resource", "[TBD] Resource", "[미정] 자원");
        AddLocRow("research_state_completed", "Completed", "완료");
        AddLocRow("research_state_day_only", "Research is available during the day only", "낮에만 가능");
        AddLocRow("research_state_tier_locked", "Tier Locked", "티어 잠김");
        AddLocRow("research_state_prerequisite_locked", "Prerequisite Required", "선행 필요");
        AddLocRow("research_state_insufficient_rp", "Insufficient RP", "RP 부족");
        AddLocRow("research_state_insufficient_resources", "Insufficient Resources", "자원 부족");
        AddLocRow("research_state_available", "Available", "연구 가능");
        AddLocRow("research_state_invalid", "Unavailable", "이용 불가");
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
            if (!existingIds.Add(row.Id))
            {
                continue;
            }

            newLines.Add(FormatCsvRow(row.Id, valueSelector(row)));
        }

        if (newLines.Count == 0)
        {
            return;
        }

        // 기존 파일이 줄바꿈 없이 끝나면 그대로 append할 경우 마지막 줄과 새 줄이 붙어 CSV가 깨진다.
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
        WriteFragment(
            Path.Combine(projectRoot, SHEET_EXPORT_FOLDER, "로컬라이징_연구트리_en_us.csv"),
            row => row.En);
        WriteFragment(
            Path.Combine(projectRoot, SHEET_EXPORT_FOLDER, "로컬라이징_연구트리_ko_kr.csv"),
            row => row.Ko);
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
}
