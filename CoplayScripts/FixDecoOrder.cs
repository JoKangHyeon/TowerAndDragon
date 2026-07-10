using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — Decoration 오버레이를 팩 컨벤션(sortingOrder=1)으로
public static class FixDecoOrder
{
    const int OVERLAY_SORTING_ORDER = 1;

    public static string Execute()
    {
        var grid = GameObject.Find("Grid");
        var ren = grid.transform.Find("Decoration").GetComponent<TilemapRenderer>();
        Undo.RecordObject(ren, "Fix deco order");
        ren.sortingOrder = OVERLAY_SORTING_ORDER;
        EditorSceneManager.MarkSceneDirty(grid.scene);
        return $"Decoration sortingOrder={ren.sortingOrder}";
    }
}
