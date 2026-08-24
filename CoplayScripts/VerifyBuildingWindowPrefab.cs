using UnityEditor;
using UnityEngine;

public static class VerifyBuildingWindowPrefab
{
    private const string WINDOW_PREFAB = "Assets/Prefabs/UI/Window/Building_window.prefab";
    private const string CANVAS_PREFAB = "Assets/Prefabs/UI/Window/UI_Canvas.prefab";

    public static void Execute()
    {
        Report(WINDOW_PREFAB);
        Report(CANVAS_PREFAB);
    }

    private static void Report(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogError($"[V] 프리팹 로드 실패: {path}");
            return;
        }

        var window = root.GetComponentInChildren<UI_PopulationAllocationWindow>(true);
        if (window == null)
        {
            Debug.LogError($"[V] {path}: UI_PopulationAllocationWindow 없음");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Transform infoZone = FindDeep(window.transform, "InfoZone");
        string children = "(InfoZone 없음)";
        if (infoZone != null)
        {
            children = "";
            for (int i = 0; i < infoZone.childCount; i++)
            {
                children += infoZone.GetChild(i).name + " ";
            }
        }

        Debug.Log($"[V] {path}\n  InfoZone 자식: {children}");

        var so = new SerializedObject(window);
        string[] fields =
        {
            "_availablePopulationRow", "_populationRow", "_outputRowSeed",
            "_populationIcon", "_operationIcon", "_researchPointIcon",
            "_healthIcon", "_attackIcon", "_attackIntervalIcon", "_dpsIcon", "_rangeIcon",
        };

        foreach (string name in fields)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null)
            {
                Debug.Log($"[V]   {name}: (필드 없음)");
                continue;
            }

            Object v = p.objectReferenceValue;
            Debug.Log($"[V]   {name}: {(v == null ? "NULL" : v.name)}");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
