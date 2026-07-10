using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 데코 1차 배치 문제 수정
public static class FixDecoration
{
    const int SEED = 26071001;

    public static string Execute()
    {
        var rng = new System.Random(SEED);
        var sb = new StringBuilder();

        var grid = GameObject.Find("Grid");
        var groundTm = grid.transform.Find("Ground").GetComponent<Tilemap>();
        var decoTm = grid.transform.Find("Decoration").GetComponent<Tilemap>();
        var props = grid.transform.Find("Props");

        Undo.RegisterCompleteObjectUndo(groundTm, "Fix decoration");
        Undo.RegisterCompleteObjectUndo(decoTm, "Fix decoration");

        // 타일 로드
        var tiles = new Dictionary<string, TileBase>();
        foreach (var g in AssetDatabase.FindAssets("t:TileBase",
            new[] { "Assets/Imported/GoldenSkullStudios/2D/2D_Iso_Tile_Pack_Starter" }))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null && !tiles.ContainsKey(t.name)) tiles[t.name] = t;
        }

        // 나무 스프라이트 로드
        var sprites = new List<Sprite>();
        foreach (var g in AssetDatabase.FindAssets("t:Sprite",
            new[] { "Assets/Imported/GoldenSkullStudios/2D_Iso_FoliagePack/Sprites" }))
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
            if (s != null) sprites.Add(s);
        }
        List<Sprite> Pick(string prefix) => sprites.Where(s =>
            s.name.ToLowerInvariant().StartsWith(prefix.ToLowerInvariant())).ToList();

        var leafTrees = Pick("GSS_Foliage_Forest_big_tree_leaf");
        var needleTrees = Pick("GSS_Foliage_Forest_big_tree_needle");
        var cypress = Pick("GSS_Foliage_Forest_big_cypress");
        sb.AppendLine($"trees: leaf={leafTrees.Count} needle={needleTrees.Count} cypress={cypress.Count}");

        var refProp = props.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();

        void PlaceTree(Vector3Int cell, Sprite sprite)
        {
            var go = new GameObject(sprite.name);
            Undo.RegisterCreatedObjectUndo(go, "Fix decoration");
            go.transform.SetParent(props, false);
            var w = groundTm.GetCellCenterWorld(cell);
            go.transform.position = new Vector3(w.x, w.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // 피벗이 하단이면 Pivot 소트, 아니면 Center
            bool bottomPivot = sprite.pivot.y < sprite.rect.height * 0.3f;
            sr.spriteSortPoint = bottomPivot ? SpriteSortPoint.Pivot : SpriteSortPoint.Center;
            if (refProp != null)
            {
                sr.sharedMaterial = refProp.sharedMaterial;
                sr.sortingLayerID = refProp.sortingLayerID;
                sr.sortingOrder = refProp.sortingOrder;
            }
        }

        // ---------- 1. 문제 타일 제거 ----------
        decoTm.CompressBounds();
        int removedGlow = 0, removedLavaCracks = 0, treesGrass = 0, treesSnow = 0;
        var toRemove = new List<Vector3Int>();
        foreach (var p in decoTm.cellBounds.allPositionsWithin)
        {
            var t = decoTm.GetTile(p);
            if (t == null) continue;
            var groundName = groundTm.GetTile(new Vector3Int(p.x, p.y, 0))?.name ?? "";

            if (t.name == "ISO_Tile_Tree_01" || t.name == "ISO_Tile_Tree_Birch_01")
            {
                toRemove.Add(p);
                if (groundName == "ISO_Tile_Dirt_01_Grass_01")
                {
                    var pool = rng.NextDouble() < 0.7 ? leafTrees : (rng.NextDouble() < 0.5 ? needleTrees : cypress);
                    if (pool.Count > 0) { PlaceTree(new Vector3Int(p.x, p.y, 0), pool[rng.Next(pool.Count)]); treesGrass++; }
                }
                else if (groundName.StartsWith("ISO_Tile_Snow"))
                {
                    if (needleTrees.Count > 0) { PlaceTree(new Vector3Int(p.x, p.y, 0), needleTrees[rng.Next(needleTrees.Count)]); treesSnow++; }
                }
            }
            else if (t.name.StartsWith("ISO_Overlay_Glow"))
            {
                toRemove.Add(p);
                removedGlow++;
            }
            else if (t.name.StartsWith("ISO_Overlay_Cracks") && groundName.StartsWith("ISO_Tile_Lava"))
            {
                toRemove.Add(p);
                removedLavaCracks++;
            }
        }
        foreach (var p in toRemove) decoTm.SetTile(p, null);

        // ---------- 2. 용암 웅덩이 스왑 (LavaCracks → Lava) ----------
        int lavaPools = 0;
        groundTm.CompressBounds();
        foreach (var p in groundTm.cellBounds.allPositionsWithin)
        {
            var t = groundTm.GetTile(p);
            if (t == null || t.name != "ISO_Tile_LavaCracks_01") continue;
            if (rng.NextDouble() < 0.025)
            {
                var name = rng.NextDouble() < 0.6 ? "ISO_Tile_Lava_01" : "ISO_Tile_Lava_02";
                if (tiles.TryGetValue(name, out var lava)) { groundTm.SetTile(p, lava); lavaPools++; }
            }
        }

        EditorSceneManager.MarkSceneDirty(grid.scene);
        sb.AppendLine($"removed: trunkTiles={toRemove.Count - removedGlow - removedLavaCracks} glow={removedGlow} lavaCracks={removedLavaCracks}");
        sb.AppendLine($"placed: grassTrees={treesGrass} snowTrees={treesSnow} lavaPools={lavaPools}");
        return sb.ToString();
    }
}
