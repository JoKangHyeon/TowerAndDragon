using TMPro;
using UnityEngine;

// 검증용: 인자로 받은 건물 종류를 선택시키고, 다음 호출에서 창에 실제로 그려진 행을 읽는다.
// UI_PopulationAllocationWindow는 Update 폴링으로 선택을 관측하므로 선택과 읽기를 나눠 부른다.
public static class DumpBuildingWindow
{
    public static void SelectFactory() => Select("factory");

    public static void SelectCrossbow() => Select("crossbow");

    public static void SelectTimeTower() => Select("timetower");

    public static void SelectNone() => Select("none");

    private static void Select(string kind)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[W] 플레이 모드가 아닙니다.");
            return;
        }

        var grid = Object.FindFirstObjectByType<GridMap>();
        var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
        if (grid == null || controller == null)
        {
            Debug.LogError("[W] GridMap/BuildingPlacementController 없음");
            return;
        }

        if (kind == "none")
        {
            controller.Deselect();
            Debug.Log("[W] 선택 해제");
            return;
        }

        Building target = FindTarget(kind);
        if (target == null)
        {
            Debug.LogError($"[W] 대상 없음: {kind}");
            return;
        }

        if (!grid.TryGetOccupiedCoord(target, out Vector3Int coord))
        {
            Debug.LogError($"[W] 좌표 없음: {target.name}");
            return;
        }

        controller.SelectExistingBuildingAt(coord);
        Debug.Log($"[W] 선택 요청 {target.name} at {coord}");
    }

    public static void Dump()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[W] 플레이 모드가 아닙니다.");
            return;
        }

        var window = Object.FindFirstObjectByType<UI_PopulationAllocationWindow>();
        if (window == null)
        {
            Debug.LogError("[W] 창 없음");
            return;
        }

        Transform panel = window.transform.childCount > 0 ? window.transform.GetChild(0) : null;
        Debug.Log($"[W] 창 열림={panel != null && panel.gameObject.activeSelf}");

        Transform infoZone = FindDeep(window.transform, "InfoZone");
        if (infoZone == null)
        {
            Debug.LogError("[W] InfoZone 없음");
            return;
        }

        DumpRows(infoZone, 0);
    }

    private static void DumpRows(Transform parent, int depth)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            var slot = child.GetComponent<UI_ConquestInfoSlot>();

            if (slot == null)
            {
                DumpRows(child, depth + 1);
                continue;
            }

            var texts = child.GetComponentsInChildren<TMP_Text>(true);
            string label = texts.Length > 0 ? texts[0].text : "?";
            string value = texts.Length > 1 ? texts[1].text : "?";

            Transform iconTransform = FindDeep(child, "Icon");
            var icon = iconTransform != null
                ? iconTransform.GetComponent<UnityEngine.UI.Image>()
                : null;
            string iconName = icon != null && icon.sprite != null ? icon.sprite.name : "(no sprite)";

            Debug.Log($"[W] ROW active={child.gameObject.activeSelf,-5} " +
                      $"'{label}' = '{value}'  icon={iconName}  ({child.name})");
        }
    }

    private static Building FindTarget(string kind)
    {
        if (kind == "factory")
        {
            return Object.FindFirstObjectByType<Factory>();
        }

        foreach (Tower tower in Object.FindObjectsByType<Tower>(FindObjectsSortMode.None))
        {
            if (tower.Data == null)
            {
                continue;
            }

            if (kind == "crossbow" && tower.Data.name.Contains("CrossBow"))
            {
                return tower;
            }

            if (kind == "timetower" && tower.Data.name.Contains("TimeTower"))
            {
                return tower;
            }
        }

        return null;
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
