using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// BuildingCatalog 유지보수 도구.
//
// 배치 가능한 건물 목록이 두 곳에 있다 - 플레이어에게 보여 줄 목록은 UI_BuildModeWindow._filterTabs가,
// 세이브가 되살릴 수 있는 목록은 BuildingCatalog가 들고 있다. 둘이 어긋나면 "지을 수는 있는데
// 불러오면 사라지는 건물"이 조용히 생기므로, 검증으로 그 불일치를 드러낸다.
//
// PrefabId 부여 자체는 자동화하지 않는다 - 어떤 건물을 세이브 대상으로 삼을지는 설계 판단이고
// (성·임시 방벽은 일부러 비워 둔다), 자동으로 채우면 그 판단이 지워진다.
public static class BuildingCatalogTools
{
    private const string CATALOG_PATH = "Assets/Data/BuildingCatalog.asset";
    private const string PREFAB_FILTER = "t:Prefab";
    private const string PREFAB_FIELD_NAME = "_buildingPrefabs";

    [MenuItem("Tools/Building Catalog/PrefabId가 있는 프리팹으로 카탈로그 재구성")]
    public static void RebuildFromPrefabIds()
    {
        BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CATALOG_PATH);

        if (catalog == null)
        {
            Debug.LogError($"[BuildingCatalogTools] 카탈로그 에셋을 찾지 못했습니다: {CATALOG_PATH}");
            return;
        }

        List<Building> saveable = FindSaveableBuildingPrefabs();
        saveable.Sort((left, right) => string.CompareOrdinal(left.PrefabId, right.PrefabId));

        var serialized = new SerializedObject(catalog);
        SerializedProperty prefabs = serialized.FindProperty(PREFAB_FIELD_NAME);
        prefabs.arraySize = saveable.Count;

        for (int i = 0; i < saveable.Count; i++)
        {
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = saveable[i];
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BuildingCatalogTools] 카탈로그를 {saveable.Count}개 프리팹으로 재구성했습니다.");
        Validate();
    }

    [MenuItem("Tools/Building Catalog/검증")]
    public static void Validate()
    {
        BuildingCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CATALOG_PATH);

        if (catalog == null)
        {
            Debug.LogError($"[BuildingCatalogTools] 카탈로그 에셋을 찾지 못했습니다: {CATALOG_PATH}");
            return;
        }

        var registeredIds = new Dictionary<string, Building>();
        var registeredPrefabs = new HashSet<Building>();
        int issueCount = 0;

        foreach (Building prefab in catalog.All)
        {
            if (prefab == null)
            {
                Debug.LogError("[BuildingCatalogTools] 카탈로그에 끊긴 참조가 있습니다.", catalog);
                issueCount++;
                continue;
            }

            registeredPrefabs.Add(prefab);

            if (!prefab.IsSaveable)
            {
                Debug.LogError($"[BuildingCatalogTools] '{prefab.name}'에 PrefabId가 비어 있습니다.", prefab);
                issueCount++;
                continue;
            }

            if (registeredIds.TryGetValue(prefab.PrefabId, out Building existing))
            {
                Debug.LogError(
                    $"[BuildingCatalogTools] PrefabId '{prefab.PrefabId}'가 '{existing.name}'과 '{prefab.name}'에 중복됩니다.",
                    prefab);

                issueCount++;
                continue;
            }

            registeredIds.Add(prefab.PrefabId, prefab);
        }

        // id는 붙였는데 카탈로그 등록을 잊은 경우 - 저장은 되고 복원만 실패하는 가장 알아채기 어려운 형태다.
        foreach (Building prefab in FindSaveableBuildingPrefabs())
        {
            if (!registeredPrefabs.Contains(prefab))
            {
                Debug.LogError(
                    $"[BuildingCatalogTools] '{prefab.name}'에 PrefabId '{prefab.PrefabId}'가 있는데 카탈로그에 없습니다.",
                    prefab);

                issueCount++;
            }
        }

        foreach (Building prefab in FindPlaceableBuildingPrefabs())
        {
            if (!prefab.IsSaveable)
            {
                Debug.LogWarning(
                    $"[BuildingCatalogTools] '{prefab.name}'은 빌드 메뉴에 있는데 PrefabId가 없어 저장되지 않습니다.",
                    prefab);

                issueCount++;
            }
        }

        Debug.Log($"[BuildingCatalogTools] 검증 완료 - 등록 {registeredIds.Count}개, 문제 {issueCount}건.");
    }

    private static List<Building> FindSaveableBuildingPrefabs()
    {
        var result = new List<Building>();

        foreach (string guid in AssetDatabase.FindAssets(PREFAB_FILTER))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<Building>(path);

            if (prefab != null && prefab.IsSaveable)
            {
                result.Add(prefab);
            }
        }

        return result;
    }

    // UI_BuildModeWindow가 슬롯으로 띄우는 건물들 - 플레이어가 실제로 지을 수 있는 목록이다.
    private static List<Building> FindPlaceableBuildingPrefabs()
    {
        var result = new List<Building>();

        foreach (string guid in AssetDatabase.FindAssets(PREFAB_FILTER))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
            {
                continue;
            }

            foreach (UI_BuildModeWindow window in root.GetComponentsInChildren<UI_BuildModeWindow>(true))
            {
                result.AddRange(window.PlaceableBuildings);
            }
        }

        return result;
    }
}
