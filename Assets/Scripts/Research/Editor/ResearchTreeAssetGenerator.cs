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

    private static NodeSpec[] BuildNodeSpecs() => new[]
    {
        // ---------------- 타워 갈래 (로드맵 §5.1) ----------------
        new NodeSpec
        {
            NodeId = "tower_damage", AssetName = "RN_TowerDamage",
            Branch = ResearchBranch.Tower, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_TowerDamageIncrease",
            NameEn = "[TBD] Basic Training", NameKo = "[미정] 기본 훈련",
            DescEn = "[TBD] All tower damage +10%.", DescKo = "[미정] 전 타워 공격력 +10%",
        },
        new NodeSpec
        {
            NodeId = "tower_range_1", AssetName = "RN_TowerRange1",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 30), Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = After("tower_damage"),
            NameEn = "[TBD] Range Extension I", NameKo = "[미정] 사거리 확장 I",
            DescEn = "[TBD] Tower range +15%.", DescKo = "[미정] 사거리 +15%",
        },
        new NodeSpec
        {
            NodeId = "tower_firerate_1", AssetName = "RN_TowerFireRate1",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛20 - 대응 자원 없음
            PrerequisiteIds = After("tower_damage"),
            NameEn = "[TBD] Rate of Fire I", NameKo = "[미정] 연사 개량 I",
            DescEn = "[TBD] Attack speed +12%.", DescKo = "[미정] 공격속도 +12%",
        },
        new NodeSpec
        {
            NodeId = "tower_manpower_1", AssetName = "RN_TowerManpower1",
            Branch = ResearchBranch.Tower, Tier = 2, ResearchPointCost = 30,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("tower_damage"),
            NameEn = "[TBD] Manpower Efficiency I", NameKo = "[미정] 인력 효율 I",
            DescEn = "[TBD] Tower population requirement -1 (min 1).",
            DescKo = "[미정] 타워 필요 인구 -1 (하한 1)",
        },
        new NodeSpec
        {
            // 로드맵 선행은 "사거리·연사 중 1"(OR)이지만 Prerequisites는 AND 판정만 지원한다.
            // 노드가 도달 불가해지지 않도록 둘 중 하나(사거리 확장 I)만 선행으로 둔다 - 팀 확인 필요.
            NodeId = "tower_elemental_unlock", AssetName = "RN_TowerElementalUnlock",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화×2종 - 종류 미정
            PrerequisiteIds = After("tower_range_1"),
            NameEn = "[TBD] Elemental Towers", NameKo = "[미정] 속성 타워 해금",
            DescEn = "[TBD] Allows building biome-specialized towers.",
            DescKo = "[미정] 바이옴 특화 타워 건설 가능",
        },
        new NodeSpec
        {
            NodeId = "tower_pierce_splash", AssetName = "RN_TowerPierceSplash",
            Branch = ResearchBranch.Tower, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛40·특화
            PrerequisiteIds = After("tower_firerate_1"),
            NameEn = "[TBD] Pierce / Splash", NameKo = "[미정] 관통/스플래시",
            DescEn = "[TBD] Unlocks multi-hit tower families.", DescKo = "[미정] 다중 타격 계열 해금",
        },
        new NodeSpec
        {
            NodeId = "tower_tier2", AssetName = "RN_TowerTier2",
            Branch = ResearchBranch.Tower, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Stone, 60)), // 鑛40 생략
            PrerequisiteIds = After("tower_elemental_unlock"),
            NameEn = "[TBD] Tower Tier 2", NameKo = "[미정] 타워 티어2 강화",
            DescEn = "[TBD] Opens higher-tier tower upgrades.", DescKo = "[미정] 상위 티어 업그레이드 개방",
        },
        new NodeSpec
        {
            NodeId = "tower_elemental_branch", AssetName = "RN_TowerElementalBranch",
            Branch = ResearchBranch.Tower, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화 다량
            PrerequisiteIds = After("tower_elemental_unlock"),
            NameEn = "[TBD] Elemental Specialization", NameKo = "[미정] 속성 특화 분기",
            DescEn = "[TBD] Strengthens per-element specialization effects.",
            DescKo = "[미정] 속성별 특화 효과 강화",
        },
        new NodeSpec
        {
            NodeId = "tower_tier3_overcharge", AssetName = "RN_TowerTier3Overcharge",
            Branch = ResearchBranch.Tower, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화·鑛 다량
            PrerequisiteIds = After("tower_tier2"),
            NameEn = "[TBD] Tower Tier 3 / Overcharge", NameKo = "[미정] 타워 티어3 / 오버차지",
            DescEn = "[TBD] Top-tier upgrade plus area overcharge.",
            DescKo = "[미정] 최상위 강화 + 광역 오버차지",
        },

        // ---------------- 생산 갈래 (로드맵 §5.2) ----------------
        new NodeSpec
        {
            NodeId = "grass_cultivation", AssetName = "RN_GrassCultivation",
            Branch = ResearchBranch.Production, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Food, 10)),
            PrerequisiteIds = Array.Empty<string>(),
            EffectAssetName = "RE_GrassYieldMultiplier",
            NameEn = "[TBD] Grassland Cultivation", NameKo = "[미정] 초원 경작",
            DescEn = "[TBD] Farm and mine production +10%.", DescKo = "[미정] 농장·광산 생산량 +10%",
        },
        new NodeSpec
        {
            NodeId = "production_storage", AssetName = "RN_ProductionStorage",
            Branch = ResearchBranch.Production, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 30)),
            PrerequisiteIds = After("grass_cultivation"),
            NameEn = "[TBD] Storage Expansion", NameKo = "[미정] 저장 확충",
            DescEn = "[TBD] Raises resource storage caps.", DescKo = "[미정] 자원 저장 한도 상향",
        },
        new NodeSpec
        {
            NodeId = "production_outer_basic", AssetName = "RN_ProductionOuterBasic",
            Branch = ResearchBranch.Production, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Wood, 30), Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("grass_cultivation"),
            NameEn = "[TBD] Outer Basic Harvesting", NameKo = "[미정] 외곽 기초자원 채집",
            DescEn = "[TBD] Unlocks basic resource harvesting in outer biomes.",
            DescKo = "[미정] 외곽 바이옴 기초 4자원 채집 해금",
        },
        new NodeSpec
        {
            NodeId = "production_specialized", AssetName = "RN_ProductionSpecialized",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화
            PrerequisiteIds = After("production_outer_basic"),
            NameEn = "[TBD] Specialized Facilities", NameKo = "[미정] 특화 생산 시설",
            DescEn = "[TBD] Biome specialty resource efficiency +20%.",
            DescKo = "[미정] 바이옴 특화 자원 효율 +20%",
        },
        new NodeSpec
        {
            NodeId = "production_slime_farm", AssetName = "RN_ProductionSlimeFarm",
            Branch = ResearchBranch.Production, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화·슬
            PrerequisiteIds = After("production_outer_basic"),
            NameEn = "[TBD] Slime Farming", NameKo = "[미정] 슬라임 양식",
            DescEn = "[TBD] Slime production in specialty areas +25%.",
            DescKo = "[미정] 특화 지역 슬라임 생산 +25%",
        },
        new NodeSpec
        {
            NodeId = "production_optimize", AssetName = "RN_ProductionOptimize",
            Branch = ResearchBranch.Production, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛40
            PrerequisiteIds = After("production_specialized"),
            NameEn = "[TBD] Production Optimization", NameKo = "[미정] 생산 최적화",
            DescEn = "[TBD] Production facility population requirement -1 (min 1).",
            DescKo = "[미정] 생산 시설 필요 인구 -1 (하한 1)",
        },
        new NodeSpec
        {
            NodeId = "production_diagonal", AssetName = "RN_ProductionDiagonal",
            Branch = ResearchBranch.Production, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화 다량
            PrerequisiteIds = After("production_specialized"),
            NameEn = "[TBD] Diagonal Zone Development", NameKo = "[미정] 대각선 지대 개발",
            DescEn = "[TBD] Production bonus in high-yield diagonal zones.",
            DescKo = "[미정] 고수익 대각선 지대 생산 보너스",
        },
        new NodeSpec
        {
            NodeId = "production_auto_line", AssetName = "RN_ProductionAutoLine",
            Branch = ResearchBranch.Production, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(), // 특화·鑛 다량
            PrerequisiteIds = After("production_optimize"),
            NameEn = "[TBD] Automated Production Line", NameKo = "[미정] 자동 생산 라인",
            DescEn = "[TBD] Some resources are auto-produced at settlement.",
            DescKo = "[미정] 정산 시 일부 자원 자동 증산",
        },

        // ---------------- 편의 갈래 (로드맵 §5.3) ----------------
        new NodeSpec
        {
            NodeId = "convenience_scout_1", AssetName = "RN_ConvenienceScout1",
            Branch = ResearchBranch.Convenience, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 15)),
            PrerequisiteIds = Array.Empty<string>(),
            NameEn = "[TBD] Scouting I", NameKo = "[미정] 정찰 I",
            DescEn = "[TBD] Fog of war vision radius +1 step.",
            DescKo = "[미정] 전장의 안개 가시 반경 +1단계",
        },
        new NodeSpec
        {
            NodeId = "convenience_lab_expand_1", AssetName = "RN_ConvenienceLabExpand1",
            Branch = ResearchBranch.Convenience, Tier = 1, ResearchPointCost = 10,
            ResourceCost = Cost(Amount(ResourceType.Wood, 20), Amount(ResourceType.Stone, 20)),
            PrerequisiteIds = Array.Empty<string>(),
            NameEn = "[TBD] Lab Expansion I", NameKo = "[미정] 연구소 증축 I",
            DescEn = "[TBD] Raises the research lab population cap (more RP per night).",
            DescKo = "[미정] 연구소 배치 가능 최대 인구 +N (RP 생성량↑)",
        },
        new NodeSpec
        {
            NodeId = "convenience_tower_move", AssetName = "RN_ConvenienceTowerMove",
            Branch = ResearchBranch.Convenience, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("convenience_scout_1"),
            NameEn = "[TBD] Tower Relocation", NameKo = "[미정] 타워 이동 해금",
            DescEn = "[TBD] Move towers without demolishing them.",
            DescKo = "[미정] 철거 없이 타워 이동 가능",
        },
        new NodeSpec
        {
            NodeId = "convenience_castle_regen_1", AssetName = "RN_ConvenienceCastleRegen1",
            Branch = ResearchBranch.Convenience, Tier = 2, ResearchPointCost = 25,
            ResourceCost = Cost(Amount(ResourceType.Stone, 20)), // 鑛10 생략
            PrerequisiteIds = After("convenience_scout_1"),
            NameEn = "[TBD] Castle Auto-Repair I", NameKo = "[미정] 성 자동 회복 I",
            DescEn = "[TBD] Castle passively regenerates health each day.",
            DescKo = "[미정] 매일 낮 성 체력 패시브 회복",
        },
        new NodeSpec
        {
            NodeId = "convenience_castle_repair", AssetName = "RN_ConvenienceCastleRepair",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)), // 鑛20 생략
            PrerequisiteIds = After("convenience_castle_regen_1"),
            NameEn = "[TBD] Emergency Castle Repair", NameKo = "[미정] 성 긴급 수리",
            DescEn = "[TBD] Improves instant daytime repair efficiency.",
            DescKo = "[미정] 낮 자원 소모 즉시 수리 효율↑",
        },
        new NodeSpec
        {
            NodeId = "convenience_expedition_logistics", AssetName = "RN_ConvenienceExpeditionLogistics",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Wood, 40), Amount(ResourceType.Food, 20)),
            PrerequisiteIds = After("convenience_tower_move"),
            NameEn = "[TBD] Expedition Logistics", NameKo = "[미정] 원정 물류",
            DescEn = "[TBD] Reduces conquest expedition cost and duration.",
            DescKo = "[미정] 점령 출격 비용/소요 감소",
        },
        new NodeSpec
        {
            NodeId = "convenience_lab_expand_2", AssetName = "RN_ConvenienceLabExpand2",
            Branch = ResearchBranch.Convenience, Tier = 3, ResearchPointCost = 50,
            ResourceCost = Cost(Amount(ResourceType.Stone, 40)), // 鑛20 생략
            PrerequisiteIds = After("convenience_lab_expand_1"),
            NameEn = "[TBD] Lab Expansion II", NameKo = "[미정] 연구소 증축 II",
            DescEn = "[TBD] Further raises the research lab population cap.",
            DescKo = "[미정] 연구소 배치 가능 최대 인구 추가 확장",
        },
        new NodeSpec
        {
            NodeId = "convenience_scout_2", AssetName = "RN_ConvenienceScout2",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛30
            PrerequisiteIds = After("convenience_scout_1"),
            NameEn = "[TBD] Scouting II", NameKo = "[미정] 정찰 II",
            DescEn = "[TBD] Greatly extends vision and marks landmarks.",
            DescKo = "[미정] 가시 반경 대폭 확장 + 랜드마크 표시",
        },
        new NodeSpec
        {
            NodeId = "convenience_batch_manage", AssetName = "RN_ConvenienceBatchManage",
            Branch = ResearchBranch.Convenience, Tier = 4, ResearchPointCost = 90,
            ResourceCost = Cost(Amount(ResourceType.Stone, 30)),
            PrerequisiteIds = After("convenience_tower_move"),
            NameEn = "[TBD] Batch Placement / Management", NameKo = "[미정] 일괄 배치/관리",
            DescEn = "[TBD] Unlocks batch tower and population placement UI.",
            DescKo = "[미정] 타워·인구 배치 편의 UI 해금",
        },
        new NodeSpec
        {
            NodeId = "convenience_auto_settle", AssetName = "RN_ConvenienceAutoSettle",
            Branch = ResearchBranch.Convenience, Tier = 5, ResearchPointCost = 150,
            ResourceCost = Array.Empty<ResourceAmount>(), // 鑛 다량
            PrerequisiteIds = After("convenience_scout_2"),
            NameEn = "[TBD] Automated Settlement", NameKo = "[미정] 자동 정산 보조",
            DescEn = "[TBD] Automates settlement and distribution.",
            DescKo = "[미정] 정산·배분 자동화 편의",
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

        ResearchEffectSO[] effects = ResolveEffects(spec.EffectAssetName);

        ResearchNodeData node = CreateOrReplace<ResearchNodeData>(assetPath, so =>
        {
            so.FindProperty("_nodeId").stringValue = spec.NodeId;
            so.FindProperty("_nameLocKey").stringValue = nameLocKey;
            so.FindProperty("_descriptionLocKey").stringValue = descLocKey;
            so.FindProperty("_branch").enumValueIndex = (int)spec.Branch;
            so.FindProperty("_tier").intValue = spec.Tier;
            so.FindProperty("_researchPointCost").intValue = spec.ResearchPointCost;

            AssignObjectArray(so.FindProperty("_effects"), effects);

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

    // 이미 만들어져 있던 효과 SO(RE_*)는 그대로 재사용한다 - 새로 만들지 않는다.
    private static ResearchEffectSO[] ResolveEffects(string effectAssetName)
    {
        if (string.IsNullOrEmpty(effectAssetName))
        {
            return Array.Empty<ResearchEffectSO>();
        }

        var effect = AssetDatabase.LoadAssetAtPath<ResearchEffectSO>(
            $"{DATA_FOLDER}/{effectAssetName}.asset");

        if (effect == null)
        {
            Debug.LogWarning(
                $"[ResearchTreeAssetGenerator] 효과 에셋 '{effectAssetName}'을 찾지 못해 비워 둡니다.");
            return Array.Empty<ResearchEffectSO>();
        }

        return new[] { effect };
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
