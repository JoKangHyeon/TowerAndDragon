using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 루트 구조로 이관된 웨이브 데이터를 실제로 두 루트에 나눠 배치하는 일회성 도구.
/// 이관(WaveRouteMigration)은 전부 route 0으로 몰아넣기 때문에, 그대로 두면 동작이 이전과 같다.
///
/// 배치 규칙:
///  - 지상 그룹(프리팹에 GroundSplineMovement가 있는 것)만 수량을 route 0 / route 1로 반씩 나눈다.
///    홀수는 route 0이 더 갖는다.
///  - 수량이 1인 그룹(단일 힐러 등)은 나눌 수 없으므로 route 0에 둔다.
///  - 보스는 수량과 무관하게 route 0 고정. 보스가 어느 길로 오는지는 예측 가능해야 하고,
///    현재 데이터에 MD_Boss_1 x10 같은 수상한 값이 있어 그대로 갈라놓으면 안 된다.
///  - 공중 그룹은 경로를 타지 않고 성으로 직행하므로(AirDirectMovement) 전부 route 0에 둔다.
///    route 1이 공중만 갖게 되면 적이 걷지 않는 길에 선만 켜진다.
///  - 나눌 수 있는 지상 그룹이 하나도 없으면 그 포탈은 단일 루트로 남긴다.
///
/// 총 마릿수와 그룹별 간격·선행 대기는 그대로 유지된다.
/// 다만 두 루트가 병렬로 진행되므로 포탈당 편성 소요 시간은 짧아진다 — 밸런스 담당 확인 필요.
///
/// 에디터 전용이라 CLAUDE.md의 문자열/리터럴 상수 규칙 예외 대상이다.
/// </summary>
public static class WaveRouteDistribution
{
    private const string PREVIEW_MENU_PATH = "Tools/Wave/루트 배치 미리보기";
    private const string APPLY_MENU_PATH = "Tools/Wave/루트 배치 적용";
    private const string WAVE_DEFINITION_FILTER = "t:WaveDefinitionSO";

    private const string PORTAL_WAVES_FIELD = "_portalWaves";
    private const string ROUTE_WAVES_FIELD = "_routeWaves";
    private const string SPAWN_GROUPS_FIELD = "_spawnGroups";
    private const string ROUTE_INDEX_FIELD = "_routeIndex";
    private const string START_DELAY_FIELD = "_startDelay";
    private const string SPAWN_COUNT_FIELD = "_spawnCount";

    private const int MAIN_ROUTE_INDEX = 0;
    private const int SECOND_ROUTE_INDEX = 1;
    private const int MIN_SPLITTABLE_COUNT = 2;
    private const string BOSS_MONSTER_NAME_PREFIX = "MD_Boss";

    [MenuItem(PREVIEW_MENU_PATH)]
    public static void Preview() => Run(isDryRun: true);

    [MenuItem(APPLY_MENU_PATH)]
    public static void Apply() => Run(isDryRun: false);

    private static void Run(bool isDryRun)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(isDryRun ? "=== 루트 배치 미리보기 ===" : "=== 루트 배치 적용 ===");

        int changedAssetCount = 0;
        int splitPortalCount = 0;
        int singleRoutePortalCount = 0;

        foreach (string assetGuid in AssetDatabase.FindAssets(WAVE_DEFINITION_FILTER))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            WaveDefinitionSO waveDefinition =
                AssetDatabase.LoadAssetAtPath<WaveDefinitionSO>(assetPath);

            if (waveDefinition == null)
            {
                continue;
            }

            SerializedObject serializedWave = new SerializedObject(waveDefinition);
            SerializedProperty portalWaves = serializedWave.FindProperty(PORTAL_WAVES_FIELD);

            if (portalWaves == null || !portalWaves.isArray)
            {
                continue;
            }

            bool assetChanged = false;

            for (int i = 0; i < portalWaves.arraySize; i++)
            {
                SerializedProperty portalWave = portalWaves.GetArrayElementAtIndex(i);

                if (TryDistributePortalWave(
                    waveDefinition.name, portalWave, isDryRun, report))
                {
                    assetChanged = true;
                    splitPortalCount++;
                }
                else
                {
                    singleRoutePortalCount++;
                }
            }

            if (!assetChanged)
            {
                continue;
            }

            changedAssetCount++;

            if (!isDryRun)
            {
                serializedWave.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(waveDefinition);
            }
        }

        if (!isDryRun)
        {
            AssetDatabase.SaveAssets();
        }

        report.AppendLine(
            $"에셋 {changedAssetCount}개 변경 / 2루트 분산 포탈 {splitPortalCount}건 / " +
            $"단일 루트 유지 {singleRoutePortalCount}건");

        Debug.Log(report.ToString());
    }

    /// <summary>분산했으면 true. 나눌 수 있는 지상 그룹이 없어 단일 루트로 남겼으면 false.</summary>
    private static bool TryDistributePortalWave(
        string waveName,
        SerializedProperty portalWave,
        bool isDryRun,
        StringBuilder report)
    {
        SerializedProperty routeWaves = portalWave.FindPropertyRelative(ROUTE_WAVES_FIELD);

        // 이관 직후 상태(route 0 하나)만 대상으로 한다. 이미 손댄 편성은 건드리지 않는다.
        if (routeWaves == null || routeWaves.arraySize != 1)
        {
            return false;
        }

        SerializedProperty mainRoute = routeWaves.GetArrayElementAtIndex(0);

        if (mainRoute.FindPropertyRelative(ROUTE_INDEX_FIELD).intValue != MAIN_ROUTE_INDEX)
        {
            return false;
        }

        SerializedProperty mainGroups = mainRoute.FindPropertyRelative(SPAWN_GROUPS_FIELD);

        // 나눠 보낼 그룹의 (원본 인덱스, route 1 몫)
        List<(int SourceIndex, int MovedCount)> moves = new();

        for (int i = 0; i < mainGroups.arraySize; i++)
        {
            SerializedProperty group = mainGroups.GetArrayElementAtIndex(i);

            if (!IsGroundGroup(group))
            {
                continue;
            }

            if (IsBossGroup(group))
            {
                report.AppendLine($"      (보스 제외) {DescribeGroup(group)} -> route0 고정");
                continue;
            }

            int spawnCount = group.FindPropertyRelative(SPAWN_COUNT_FIELD).intValue;

            if (spawnCount < MIN_SPLITTABLE_COUNT)
            {
                continue;
            }

            moves.Add((i, spawnCount / MIN_SPLITTABLE_COUNT));
        }

        if (moves.Count == 0)
        {
            report.AppendLine($"  {waveName} | 단일 루트 유지 (나눌 지상 그룹 없음)");
            return false;
        }

        report.AppendLine($"  {waveName} | route1로 {moves.Count}개 그룹 분산");

        if (isDryRun)
        {
            AppendMovePreview(mainGroups, moves, report);
            return true;
        }

        routeWaves.arraySize = 2;
        SerializedProperty secondRoute = routeWaves.GetArrayElementAtIndex(1);
        secondRoute.FindPropertyRelative(ROUTE_INDEX_FIELD).intValue = SECOND_ROUTE_INDEX;
        secondRoute.FindPropertyRelative(START_DELAY_FIELD).floatValue =
            mainRoute.FindPropertyRelative(START_DELAY_FIELD).floatValue;

        SerializedProperty secondGroups = secondRoute.FindPropertyRelative(SPAWN_GROUPS_FIELD);
        secondGroups.arraySize = moves.Count;

        for (int i = 0; i < moves.Count; i++)
        {
            (int sourceIndex, int movedCount) = moves[i];
            SerializedProperty source = mainGroups.GetArrayElementAtIndex(sourceIndex);
            SerializedProperty destination = secondGroups.GetArrayElementAtIndex(i);

            CopySpawnGroup(source, destination);
            destination.FindPropertyRelative(SPAWN_COUNT_FIELD).intValue = movedCount;

            SerializedProperty sourceCount = source.FindPropertyRelative(SPAWN_COUNT_FIELD);
            sourceCount.intValue -= movedCount;
        }

        return true;
    }

    private static void AppendMovePreview(
        SerializedProperty mainGroups,
        List<(int SourceIndex, int MovedCount)> moves,
        StringBuilder report)
    {
        foreach ((int sourceIndex, int movedCount) in moves)
        {
            SerializedProperty group = mainGroups.GetArrayElementAtIndex(sourceIndex);
            int spawnCount = group.FindPropertyRelative(SPAWN_COUNT_FIELD).intValue;
            Object monsterData = group.FindPropertyRelative("_monsterData").objectReferenceValue;

            report.AppendLine(
                $"      {(monsterData != null ? monsterData.name : "?")} " +
                $"x{spawnCount} -> route0 x{spawnCount - movedCount}, route1 x{movedCount}");
        }
    }

    /// <summary>
    /// 보스 그룹인지. MonsterData에 보스 플래그가 없어 프로젝트 명명 규칙(MD_Boss_*)으로 판별한다.
    /// 제외한 그룹은 전부 로그에 남기므로 검수할 수 있다.
    /// </summary>
    private static bool IsBossGroup(SerializedProperty group)
    {
        Object monsterData = group.FindPropertyRelative("_monsterData").objectReferenceValue;

        return monsterData != null &&
               monsterData.name.StartsWith(BOSS_MONSTER_NAME_PREFIX);
    }

    private static string DescribeGroup(SerializedProperty group)
    {
        Object monsterData = group.FindPropertyRelative("_monsterData").objectReferenceValue;
        int spawnCount = group.FindPropertyRelative(SPAWN_COUNT_FIELD).intValue;

        return $"{(monsterData != null ? monsterData.name : "?")} x{spawnCount}";
    }

    /// <summary>경로(스플라인)를 실제로 타는 적인지. 공중적은 성으로 직행하므로 루트가 의미 없다.</summary>
    private static bool IsGroundGroup(SerializedProperty group)
    {
        Object prefabObject = group.FindPropertyRelative("_monsterPrefab").objectReferenceValue;

        if (prefabObject is not BaseMonster monsterPrefab)
        {
            return false;
        }

        return monsterPrefab.GetComponentInChildren<GroundSplineMovement>(true) != null;
    }

    private static void CopySpawnGroup(SerializedProperty source, SerializedProperty destination)
    {
        destination.FindPropertyRelative("_monsterPrefab").objectReferenceValue =
            source.FindPropertyRelative("_monsterPrefab").objectReferenceValue;
        destination.FindPropertyRelative("_monsterData").objectReferenceValue =
            source.FindPropertyRelative("_monsterData").objectReferenceValue;
        destination.FindPropertyRelative("_spawnInterval").floatValue =
            source.FindPropertyRelative("_spawnInterval").floatValue;
        destination.FindPropertyRelative("_delayBeforeGroup").floatValue =
            source.FindPropertyRelative("_delayBeforeGroup").floatValue;
    }
}
