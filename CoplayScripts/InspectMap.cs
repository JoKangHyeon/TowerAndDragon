using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class InspectMap
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // 1. Scene tilemaps: used tiles + bounds
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            var b = tm.cellBounds;
            sb.AppendLine($"### {tm.transform.parent.name}/{tm.name} bounds={b.min}~{b.max} z={tm.transform.localPosition.z}");
            var counts = new Dictionary<string, int>();
            foreach (var pos in b.allPositionsWithin)
            {
                var t = tm.GetTile(pos);
                if (t == null) continue;
                counts.TryGetValue(t.name, out int c);
                counts[t.name] = c + 1;
            }
            foreach (var kv in counts.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kv.Key} x{kv.Value}");
        }

        // 2. Available tile assets in imported pack
        string root = "Assets/Imported";
        var guids = AssetDatabase.FindAssets("t:TileBase", new[] { root });
        var byFolder = new Dictionary<string, List<string>>();
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string folder = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
            if (!byFolder.TryGetValue(folder, out var list)) { list = new List<string>(); byFolder[folder] = list; }
            list.Add(System.IO.Path.GetFileNameWithoutExtension(p));
        }
        sb.AppendLine($"### TileBase assets under {root}: {guids.Length}");
        foreach (var kv in byFolder.OrderBy(k => k.Key))
        {
            sb.AppendLine($"[{kv.Key}] ({kv.Value.Count})");
            sb.AppendLine("  " + string.Join(", ", kv.Value.OrderBy(n => n)));
        }

        return sb.ToString();
    }
}
