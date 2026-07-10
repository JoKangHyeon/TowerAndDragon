using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 소팅 통일 (Map_2 컨벤션: 전 타일맵 Individual, 프랍 z=2.5)
public static class FixSorting
{
    const float PROP_Z = 2.5f;

    public static string Execute()
    {
        var sb = new StringBuilder();
        var grid = GameObject.Find("Grid");

        foreach (var name in new[] { "Ground", "Structures", "Decoration" })
        {
            var tr = grid.transform.Find(name);
            if (tr == null) continue;
            var ren = tr.GetComponent<TilemapRenderer>();
            Undo.RecordObject(ren, "Fix sorting");
            ren.mode = TilemapRenderer.Mode.Individual;
            ren.sortOrder = TilemapRenderer.SortOrder.TopRight;
            sb.AppendLine($"{name}: mode=Individual sortOrder=TopRight");
        }

        var props = grid.transform.Find("Props");
        int moved = 0, total = 0;
        foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true))
        {
            total++;
            var p = sr.transform.position;
            if (Mathf.Approximately(p.z, PROP_Z)) continue;
            Undo.RecordObject(sr.transform, "Fix sorting");
            sr.transform.position = new Vector3(p.x, p.y, PROP_Z);
            moved++;
        }
        sb.AppendLine($"props: total={total} moved={moved}");

        EditorSceneManager.MarkSceneDirty(grid.scene);
        return sb.ToString();
    }
}
