using UnityEditor;
using UnityEngine;

/// <summary>
/// PortalWaveData의 구 구조(_spawnGroups)를 루트 구조(_routeWaves[0])로 옮기는 일회성 이관 도구.
/// 이관 후 git diff로 검수하고, 구 필드가 제거되는 커밋에서 이 스크립트도 함께 지운다.
/// 에디터 전용이라 CLAUDE.md의 문자열/리터럴 상수 규칙 예외 대상이다.
/// </summary>
public static class WaveRouteMigration
{
    private const string MENU_PATH = "Tools/Wave/포탈 웨이브 루트 구조 이관";
    private const string WAVE_DEFINITION_FILTER = "t:WaveDefinitionSO";

    private const string PORTAL_WAVES_FIELD = "_portalWaves";
    private const string ROUTE_WAVES_FIELD = "_routeWaves";
    private const string SPAWN_GROUPS_FIELD = "_spawnGroups";
    private const string ROUTE_INDEX_FIELD = "_routeIndex";
    private const string START_DELAY_FIELD = "_startDelay";

    // SpawnGroupData의 필드. 이 목록이 SpawnGroupData와 어긋나면 이관이 조용히 값을 흘린다.
    private static readonly string[] SPAWN_GROUP_OBJECT_FIELDS =
    {
        "_monsterPrefab",
        "_monsterData",
    };

    private static readonly string[] SPAWN_GROUP_INT_FIELDS =
    {
        "_spawnCount",
    };

    private static readonly string[] SPAWN_GROUP_FLOAT_FIELDS =
    {
        "_spawnInterval",
        "_delayBeforeGroup",
    };

    [MenuItem(MENU_PATH)]
    public static void MigrateAll()
    {
        string[] assetGuids = AssetDatabase.FindAssets(WAVE_DEFINITION_FILTER);
        int migratedAssetCount = 0;
        int migratedPortalCount = 0;

        foreach (string assetGuid in assetGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            WaveDefinitionSO waveDefinition =
                AssetDatabase.LoadAssetAtPath<WaveDefinitionSO>(assetPath);

            if (waveDefinition == null)
            {
                continue;
            }

            int portalCount = MigrateWaveDefinition(waveDefinition);

            if (portalCount == 0)
            {
                continue;
            }

            migratedAssetCount++;
            migratedPortalCount += portalCount;
            Debug.Log($"[WaveRouteMigration] 이관: {assetPath} (포탈 {portalCount}건)", waveDefinition);
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[WaveRouteMigration] 완료. 검사 {assetGuids.Length}개 / 이관 {migratedAssetCount}개 에셋, " +
            $"포탈 편성 {migratedPortalCount}건. git diff로 검수하세요.");
    }

    /// <summary>이관한 포탈 편성 수를 돌려준다. 0이면 손대지 않은 에셋이다.</summary>
    private static int MigrateWaveDefinition(WaveDefinitionSO waveDefinition)
    {
        SerializedObject serializedWave = new SerializedObject(waveDefinition);
        SerializedProperty portalWaves = serializedWave.FindProperty(PORTAL_WAVES_FIELD);

        if (portalWaves == null || !portalWaves.isArray)
        {
            return 0;
        }

        int migratedPortalCount = 0;

        for (int i = 0; i < portalWaves.arraySize; i++)
        {
            SerializedProperty portalWave = portalWaves.GetArrayElementAtIndex(i);
            SerializedProperty legacyGroups = portalWave.FindPropertyRelative(SPAWN_GROUPS_FIELD);
            SerializedProperty routeWaves = portalWave.FindPropertyRelative(ROUTE_WAVES_FIELD);

            if (legacyGroups == null || routeWaves == null)
            {
                continue;
            }

            // 이미 루트 구조가 있거나 옮길 게 없으면 건드리지 않는다. 재실행해도 안전하다.
            if (routeWaves.arraySize > 0 || legacyGroups.arraySize == 0)
            {
                continue;
            }

            routeWaves.arraySize = 1;
            SerializedProperty routeWave = routeWaves.GetArrayElementAtIndex(0);
            routeWave.FindPropertyRelative(ROUTE_INDEX_FIELD).intValue = 0;
            routeWave.FindPropertyRelative(START_DELAY_FIELD).floatValue = 0f;

            CopySpawnGroups(legacyGroups, routeWave.FindPropertyRelative(SPAWN_GROUPS_FIELD));
            migratedPortalCount++;
        }

        if (migratedPortalCount > 0)
        {
            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(waveDefinition);
        }

        return migratedPortalCount;
    }

    private static void CopySpawnGroups(SerializedProperty source, SerializedProperty destination)
    {
        destination.arraySize = source.arraySize;

        for (int i = 0; i < source.arraySize; i++)
        {
            SerializedProperty sourceGroup = source.GetArrayElementAtIndex(i);
            SerializedProperty destinationGroup = destination.GetArrayElementAtIndex(i);

            foreach (string fieldName in SPAWN_GROUP_OBJECT_FIELDS)
            {
                destinationGroup.FindPropertyRelative(fieldName).objectReferenceValue =
                    sourceGroup.FindPropertyRelative(fieldName).objectReferenceValue;
            }

            foreach (string fieldName in SPAWN_GROUP_INT_FIELDS)
            {
                destinationGroup.FindPropertyRelative(fieldName).intValue =
                    sourceGroup.FindPropertyRelative(fieldName).intValue;
            }

            foreach (string fieldName in SPAWN_GROUP_FLOAT_FIELDS)
            {
                destinationGroup.FindPropertyRelative(fieldName).floatValue =
                    sourceGroup.FindPropertyRelative(fieldName).floatValue;
            }
        }
    }
}
