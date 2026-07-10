using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 타일 에셋·현재 사용 현황 덤프
public static class DumpTileInventory
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== pack tiles ===");
        var names = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:TileBase",
            new[] { "Assets/Imported/GoldenSkullStudios/2D/2D_Iso_Tile_Pack_Starter" }))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null) names.Add(t.name);
        }
        foreach (var n in names.Distinct().OrderBy(n => n)) sb.AppendLine(n);

        var grid = GameObject.Find("Grid");
        foreach (Transform child in grid.transform)
        {
            var tm = child.GetComponent<Tilemap>();
            if (tm == null) continue;
            tm.CompressBounds();
            var counts = new Dictionary<string, int>();
            foreach (var p in tm.cellBounds.allPositionsWithin)
            {
                var t = tm.GetTile(p);
                if (t == null) continue;
                counts.TryGetValue(t.name, out int c);
                counts[t.name] = c + 1;
            }
            sb.AppendLine($"=== used in {child.name} ===");
            foreach (var kv in counts.OrderByDescending(k => k.Value))
                sb.AppendLine($"{kv.Key}: {kv.Value}");
        }

        var props = grid.transform.Find("Props");
        if (props != null)
            sb.AppendLine($"=== Props children: {props.childCount} ===");
        return sb.ToString();
    }
}
