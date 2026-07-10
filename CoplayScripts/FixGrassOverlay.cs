using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 색이 묻혀 안 보이는 풀 오버레이를 GrassPatch 지면 스왑으로 교체
public static class FixGrassOverlay
{
    const int SEED = 26071002;

    public static string Execute()
    {
        var rng = new System.Random(SEED);
        var grid = GameObject.Find("Grid");
        var groundTm = grid.transform.Find("Ground").GetComponent<Tilemap>();
        var decoTm = grid.transform.Find("Decoration").GetComponent<Tilemap>();
        Undo.RegisterCompleteObjectUndo(groundTm, "Fix grass overlay");
        Undo.RegisterCompleteObjectUndo(decoTm, "Fix grass overlay");

        var patches = new List<TileBase>();
        foreach (var g in AssetDatabase.FindAssets("t:TileBase ISO_Tile_Dirt_01_GrassPatch",
            new[] { "Assets/Imported/GoldenSkullStudios/2D/2D_Iso_Tile_Pack_Starter" }))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null && t.name.StartsWith("ISO_Tile_Dirt_01_GrassPatch")) patches.Add(t);
        }
        if (patches.Count == 0) return "ERROR: no GrassPatch tiles found";

        decoTm.CompressBounds();
        int removed = 0, swapped = 0;
        var cells = new List<Vector3Int>();
        foreach (var p in decoTm.cellBounds.allPositionsWithin)
        {
            var t = decoTm.GetTile(p);
            if (t != null && t.name.StartsWith("ISO_Overlay_Grass")) cells.Add(p);
        }
        foreach (var p in cells)
        {
            decoTm.SetTile(p, null);
            removed++;
            var gc = new Vector3Int(p.x, p.y, 0);
            var gt = groundTm.GetTile(gc);
            if (gt != null && gt.name == "ISO_Tile_Dirt_01_Grass_01" && rng.NextDouble() < 0.6)
            {
                groundTm.SetTile(gc, patches[rng.Next(patches.Count)]);
                swapped++;
            }
        }

        EditorSceneManager.MarkSceneDirty(grid.scene);
        return $"removed grass overlays={removed}, swapped to GrassPatch={swapped}";
    }
}
