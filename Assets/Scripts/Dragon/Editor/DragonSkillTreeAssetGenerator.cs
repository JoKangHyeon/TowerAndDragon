using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using UnityEditor;
using UnityEngine;

// 용 스킬트리 SO 에셋 30개(5속성×6노드) + 효과 12개 + 게이트 7개 + 트리 1개를 코드로 생성한다.
// 손으로 .asset YAML을 작성하지 않기 위한 에디터 전용 도구 - CLAUDE.md 커밋규칙 §5(에디터 전용 코드)
// 리터럴 제한 예외에 해당한다. 재실행 시 기존 에셋을 재사용(GUID 유지)해 멱등적으로 동작한다.
// 코스트·수치는 전부 Docs/용_스킬트리_로드맵.md §4·§9의 예시(밸런싱 대상)를 그대로 옮긴 것이다.
public static class DragonSkillTreeAssetGenerator
{
    private const string DATA_FOLDER = "Assets/Data/Dragon";
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

    // 새끼용(#102·#103) 미구현 상태의 임시 게이트값 - 로드맵 §5·§10, 일정계획 A-1.
    // 최종 목표치는 강화=2 / 궁극=4 (팀이 새끼용을 구현하면 두 KinCountGateSO 에셋의
    // _requiredKinCount만 각각 갱신하면 된다).
    private const int TEMP_REQUIRED_KIN_COUNT = 0;

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
            ActiveDescKo = "화상 틱뎀 가속 / 전역 대미지", ActiveDescEn = "Accelerate burn tick damage / global damage.",
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
            KinAreaDescKo = "점령 공격대에 포함 가능", KinAreaDescEn = "Can join conquest expeditions.",
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
                so.FindProperty("_requiredKinCount").intValue = TEMP_REQUIRED_KIN_COUNT;
                so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_count_enhance";
            });
        AddLocRow("dragon_gate_kin_count_enhance",
            "[TBD] Locked: requires {0} unlocked baby dragon nodes",
            "[미정] 잠김: 새끼용 노드 {0}개 해금 필요");

        KinCountGateSO ultimateGate = CreateOrReplace<KinCountGateSO>(
            $"{DATA_FOLDER}/DG_KinCount_Ultimate.asset",
            so =>
            {
                so.FindProperty("_requiredKinCount").intValue = TEMP_REQUIRED_KIN_COUNT;
                so.FindProperty("_lockedLocKey").stringValue = "dragon_gate_kin_count_ultimate";
            });
        AddLocRow("dragon_gate_kin_count_ultimate",
            "[TBD] Locked: requires {0} unlocked baby dragon nodes",
            "[미정] 잠김: 새끼용 노드 {0}개 해금 필요");

        // 2. 효과: 속성별 패시브/액티브 해금(각 5개) + 공유 새끼용 마커 2종
        var passiveEffects = new Dictionary<DragonType, PassiveAttributeEffectSO>();
        var activeEffects = new Dictionary<DragonType, ActiveSkillUnlockEffectSO>();
        foreach (AttributeSpec attr in attributes)
        {
            passiveEffects[attr.Type] = CreateOrReplace<PassiveAttributeEffectSO>(
                $"{DATA_FOLDER}/DE_Passive_{Capitalize(attr.Key)}.asset",
                so =>
                {
                    so.FindProperty("_attribute").enumValueIndex = (int)attr.Type;
                    so.FindProperty("_value").floatValue = 0f; // 소비처 없음 - stub, 로드맵 §8
                });

            activeEffects[attr.Type] = CreateOrReplace<ActiveSkillUnlockEffectSO>(
                $"{DATA_FOLDER}/DE_ActiveSkillUnlock_{Capitalize(attr.Key)}.asset",
                _ => { }); // _skill은 대상 SkillSO가 없어 비워둔다 - stub
        }

        KinTowerEffectSO kinTowerEffect = CreateOrReplace<KinTowerEffectSO>($"{DATA_FOLDER}/DE_KinTower.asset", _ => { });
        KinAreaEffectSO kinAreaEffect = CreateOrReplace<KinAreaEffectSO>($"{DATA_FOLDER}/DE_KinArea.asset", _ => { });

        // 3. 노드 30개(5속성 × 6종)
        var orderedNodes = new List<DragonSkillNodeData>();

        foreach (AttributeSpec attr in attributes)
        {
            List<ResourceAmount> awakenCost = BuildCost(attr, AWAKEN_SLIME_COST, 0);
            List<ResourceAmount> activeCost = BuildCost(attr, ACTIVE_SLIME_COST, ACTIVE_SPECIAL_COST);
            List<ResourceAmount> enhanceCost = BuildCost(attr, ENHANCE_SLIME_COST, ENHANCE_SPECIAL_COST);
            List<ResourceAmount> ultimateCost = BuildCost(attr, ULTIMATE_SLIME_COST, ULTIMATE_SPECIAL_COST);
            List<ResourceAmount> kinCost = BuildCost(attr, KIN_SLIME_COST, KIN_SPECIAL_COST);

            DragonSkillNodeData awaken = CreateNode(attr, "awaken", DragonNodeKind.MotherAwaken,
                null, null, awakenCost, new DragonSkillEffectSO[] { passiveEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Awaken", $"[TBD] Passive (while active): {attr.PassiveDescEn}",
                $"[미정] {attr.KoreanName} 각성", $"[미정] 패시브(활성 시): {attr.PassiveDescKo}");

            DragonSkillNodeData active = CreateNode(attr, "active", DragonNodeKind.MotherActive,
                new[] { awaken }, null, activeCost, new DragonSkillEffectSO[] { activeEffects[attr.Type] },
                $"[TBD] {Capitalize(attr.Key)} Active I", $"[TBD] Active skill: {attr.ActiveDescEn}",
                $"[미정] {attr.KoreanName} 액티브 I", $"[미정] 액티브 스킬: {attr.ActiveDescKo}");

            DragonSkillNodeData enhance = CreateNode(attr, "enhance", DragonNodeKind.MotherEnhance,
                new[] { active }, new ProgressionGateSO[] { enhanceGate }, enhanceCost, Array.Empty<DragonSkillEffectSO>(),
                $"[TBD] {Capitalize(attr.Key)} Enhance", "[TBD] Enhances the passive and active values above.",
                $"[미정] {attr.KoreanName} 강화", "[미정] 위 패시브·액티브 수치를 강화합니다.");

            DragonSkillNodeData ultimate = CreateNode(attr, "ultimate", DragonNodeKind.MotherUltimate,
                new[] { enhance }, new ProgressionGateSO[] { ultimateGate }, ultimateCost, Array.Empty<DragonSkillEffectSO>(),
                $"[TBD] {Capitalize(attr.Key)} Ultimate", "[TBD] Final upgrade of this attribute.",
                $"[미정] {attr.KoreanName} 궁극", "[미정] 이 속성의 최종 강화입니다.");

            DragonSkillNodeData kinTower = CreateNode(attr, "kin_tower", DragonNodeKind.KinTower,
                new[] { awaken }, new ProgressionGateSO[] { kinOwnedGates[attr.Type] }, kinCost,
                new DragonSkillEffectSO[] { kinTowerEffect },
                $"[TBD] {Capitalize(attr.Key)} Kin Tower", $"[TBD] {attr.KinTowerDescEn}",
                $"[미정] {attr.KoreanName} 새끼용 타워", $"[미정] {attr.KinTowerDescKo}");

            DragonSkillNodeData kinArea = CreateNode(attr, "kin_area", DragonNodeKind.KinArea,
                new[] { awaken }, new ProgressionGateSO[] { kinOwnedGates[attr.Type] }, kinCost,
                new DragonSkillEffectSO[] { kinAreaEffect },
                $"[TBD] {Capitalize(attr.Key)} Kin Area", $"[TBD] {attr.KinAreaDescEn}",
                $"[미정] {attr.KoreanName} 새끼용 지역", $"[미정] {attr.KinAreaDescKo}");

            orderedNodes.Add(awaken);
            orderedNodes.Add(active);
            orderedNodes.Add(enhance);
            orderedNodes.Add(ultimate);
            orderedNodes.Add(kinTower);
            orderedNodes.Add(kinArea);
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

        // 5. 공통 UI 로컬 키(헤더/상태/실패사유/코스트 포맷)
        AddCommonLocRows();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AppendLocalizationCsv();
        WriteSheetExportFragments();

        Debug.Log(
            $"[DragonSkillTreeAssetGenerator] 노드 {orderedNodes.Count}개, 로컬 키 {_locRows.Count}개 생성 완료. " +
            $"'{SHEET_EXPORT_FOLDER}/' 아래 시트 반영용 CSV 조각을 확인하세요.");
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
