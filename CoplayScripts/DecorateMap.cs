using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 알파용 맵 데코 배치 (CLAUDE.md 3·4번 예외 적용)
public static class DecorateMap
{
    const int SEED = 20260710;
    static System.Random _rng;
    static Dictionary<string, TileBase> _tiles;
    static List<(string name, Sprite sprite)> _foliage;

    public static string Execute()
    {
        _rng = new System.Random(SEED);
        var sb = new StringBuilder();

        var grid = GameObject.Find("Grid");
        if (grid == null) return "ERROR: Grid not found";

        // ---------- 1. 레이어 리네임 ----------
        var ground = RenameChild(grid.transform, "Tilemap", "Ground");
        var structures = RenameChild(grid.transform, "Tilemap (1)", "Structures");
        var props = RenameChild(grid.transform, "GameObject", "Props");
        if (ground == null || props == null) return "ERROR: expected children not found";

        var groundTm = ground.GetComponent<Tilemap>();
        var structTm = structures != null ? structures.GetComponent<Tilemap>() : null;
        var groundRen = ground.GetComponent<TilemapRenderer>();

        // ---------- 2. Decoration 타일맵 생성 (Map_2 컨벤션: z=2.5, Individual) ----------
        var decoTr = grid.transform.Find("Decoration");
        GameObject decoGo;
        if (decoTr == null)
        {
            decoGo = new GameObject("Decoration");
            Undo.RegisterCreatedObjectUndo(decoGo, "Create Decoration layer");
            decoGo.transform.SetParent(grid.transform, false);
            decoGo.transform.localPosition = new Vector3(0f, 0f, 2.5f);
            decoGo.AddComponent<Tilemap>();
            var ren = decoGo.AddComponent<TilemapRenderer>();
            ren.mode = TilemapRenderer.Mode.Individual;
            ren.sortOrder = TilemapRenderer.SortOrder.TopRight;
            ren.sharedMaterial = groundRen.sharedMaterial;
            ren.sortingLayerID = groundRen.sortingLayerID;
            ren.sortingOrder = groundRen.sortingOrder;
        }
        else
        {
            decoGo = decoTr.gameObject;
        }
        var decoTm = decoGo.GetComponent<Tilemap>();

        Undo.RegisterCompleteObjectUndo(groundTm, "Decorate map");
        Undo.RegisterCompleteObjectUndo(decoTm, "Decorate map");

        // ---------- 3. 타일 에셋 로드 ----------
        LoadTiles("Assets/Imported/GoldenSkullStudios/2D/2D_Iso_Tile_Pack_Starter");
        LoadFoliage("Assets/Imported/GoldenSkullStudios/2D_Iso_FoliagePack/Sprites");
        sb.AppendLine($"tiles={_tiles.Count} foliageSprites={_foliage.Count}");

        // 기존 프랍의 렌더러 세팅 복제용
        var refProp = props.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();

        // ---------- 4. 점유·제외 셀 수집 ----------
        var occupied = new HashSet<Vector3Int>();
        if (structTm != null)
        {
            structTm.CompressBounds();
            foreach (var p in structTm.cellBounds.allPositionsWithin)
                if (structTm.HasTile(p)) occupied.Add(new Vector3Int(p.x, p.y, 0));
        }
        foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var c = groundTm.WorldToCell(sr.transform.position);
            occupied.Add(new Vector3Int(c.x, c.y, 0));
        }

        groundTm.CompressBounds();
        var bounds = groundTm.cellBounds;

        // 도로·물·기타(배치 금지) 셀 수집
        var blocked = new HashSet<Vector3Int>();
        var biome = new Dictionary<Vector3Int, string>();
        foreach (var p in bounds.allPositionsWithin)
        {
            var t = groundTm.GetTile(p);
            if (t == null) continue;
            var cell = new Vector3Int(p.x, p.y, 0);
            switch (t.name)
            {
                case "ISO_Tile_Dirt_01_Grass_01": biome[cell] = "grass"; break;
                case "ISO_Tile_Snow_01": biome[cell] = "snow"; break;
                case "ISO_Tile_Sand_01": biome[cell] = "sand"; break;
                case "ISO_Tile_Stone_01": biome[cell] = "stone"; break;
                case "ISO_Tile_LavaCracks_01": biome[cell] = "lavacracks"; break;
                case "ISO_Tile_LavaStone_01": biome[cell] = "lavastone"; break;
                default: blocked.Add(cell); break; // 도로(Dirt_02)·물·전환타일 등
            }
        }

        // 중앙 메인 성 예정지 주변은 비워둠
        var center = groundTm.WorldToCell(new Vector3(0.5f, -0.5f, 0f));
        center = new Vector3Int(center.x, center.y, 0);
        const int CENTER_CLEAR_RADIUS = 4;

        // ---------- 5. 배치 ----------
        var counts = new Dictionary<string, int>();
        var treeCells = new HashSet<Vector3Int>();

        void Count(string k) { counts.TryGetValue(k, out int c); counts[k] = c + 1; }

        bool NearRoadOrBlocked(Vector3Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (blocked.Contains(new Vector3Int(c.x + dx, c.y + dy, 0))) return true;
            return false;
        }
        bool TreeSpacingOk(Vector3Int c)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (treeCells.Contains(new Vector3Int(c.x + dx, c.y + dy, 0))) return false;
            return true;
        }
        void PlaceDeco(Vector3Int c, string tileName, string key)
        {
            if (_tiles.TryGetValue(tileName, out var t)) { decoTm.SetTile(c, t); Count(key); }
        }
        void SwapGround(Vector3Int c, string tileName, string key)
        {
            if (_tiles.TryGetValue(tileName, out var t)) { groundTm.SetTile(c, t); Count(key); }
        }
        string Pick(params (string name, float w)[] items)
        {
            float total = items.Sum(i => i.w);
            float r = (float)_rng.NextDouble() * total;
            foreach (var i in items) { if ((r -= i.w) <= 0f) return i.name; }
            return items[items.Length - 1].name;
        }
        void PlaceProp(Vector3Int c, string[] prefixes, string key)
        {
            var candidates = _foliage.Where(f => prefixes.Any(px =>
                f.name.ToLowerInvariant().StartsWith(px.ToLowerInvariant()))).ToList();
            if (candidates.Count == 0) return;
            var pick = candidates[_rng.Next(candidates.Count)];
            var go = new GameObject(pick.name);
            Undo.RegisterCreatedObjectUndo(go, "Decorate map");
            go.transform.SetParent(props, false);
            var w = groundTm.GetCellCenterWorld(c);
            float ox = ((float)_rng.NextDouble() - 0.5f) * 0.4f;
            float oy = ((float)_rng.NextDouble() - 0.5f) * 0.2f;
            go.transform.position = new Vector3(w.x + ox, w.y + oy, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pick.sprite;
            sr.spriteSortPoint = SpriteSortPoint.Center;
            if (refProp != null)
            {
                sr.sharedMaterial = refProp.sharedMaterial;
                sr.sortingLayerID = refProp.sortingLayerID;
                sr.sortingOrder = refProp.sortingOrder;
            }
            occupied.Add(c);
            Count(key);
        }

        string[] forestProps = { "GSS_Foliage_Forest_small_grass", "GSS_Foliage_Forest_small_flower",
            "GSS_Foliage_Forest_small_bush", "GSS_Foliage_Forest_small_fern",
            "GSS_Foliage_Forest_small_clover", "GSS_Foliage_forest_berrybush", "GSS_Foliage_forest_bush" };
        string[] snowProps = { "GSS_Foliage_stone", "GSS_Foliage_Forest_deadBush", "GSS_Foliage_Forest_deadbush" };
        string[] desertProps = { "GSS_Foliage_Desert_small_Cactus", "GSS_Foliage_Desert_small_Agave_Cactus",
            "GSS_Foliage_Desert_small_Flower", "GSS_Foliage_Desert_small_Bush", "GSS_Foliage_desert_skull" };
        string[] ruinsProps = { "GSS_Foliage_stone", "GSS_Foliage_Forest_deadTree",
            "GSS_Foliage_Forest_deadBush", "GSS_Foliage_Forest_small_mushroom" };
        string[] lavaProps = { "GSS_Foliage_Forest_deadTree", "GSS_Foliage_desert_skull", "GSS_Foliage_stone" };

        foreach (var kv in biome.OrderBy(k => k.Key.y).ThenBy(k => k.Key.x))
        {
            var c = kv.Key;
            if (occupied.Contains(c)) continue;
            if (Mathf.Abs(c.x - center.x) + Mathf.Abs(c.y - center.y) <= CENTER_CLEAR_RADIUS) continue;
            if (decoTm.HasTile(c)) continue;

            double r = _rng.NextDouble();
            switch (kv.Value)
            {
                case "grass":
                    if (r < 0.030 && !NearRoadOrBlocked(c) && TreeSpacingOk(c))
                    {
                        PlaceDeco(c, Pick(("ISO_Tile_Tree_01", 3f), ("ISO_Tile_Tree_Birch_01", 1f)), "grass_tree");
                        treeCells.Add(c);
                    }
                    else if (r < 0.100)
                        PlaceDeco(c, Pick(("ISO_Overlay_Grass_Patch_01", 3f), ("ISO_Overlay_Grass_Patch_02", 2.5f),
                            ("ISO_Overlay_Grass_Patch_03", 2.5f), ("ISO_Overlay_Grass_01", 2f)), "grass_overlay");
                    else if (r < 0.125)
                        PlaceProp(c, forestProps, "grass_prop");
                    break;

                case "snow":
                    if (r < 0.012 && !NearRoadOrBlocked(c) && TreeSpacingOk(c))
                    {
                        PlaceDeco(c, "ISO_Tile_Tree_Birch_01", "snow_tree");
                        treeCells.Add(c);
                    }
                    else if (r < 0.045)
                        PlaceDeco(c, Pick(("ISO_Overlay_StonePieces_01", 1f), ("ISO_Overlay_StonePieces_02", 1f)), "snow_overlay");
                    else if (r < 0.105)
                        SwapGround(c, "ISO_Tile_Snow_02", "snow_var");
                    else if (r < 0.120)
                        PlaceProp(c, snowProps, "snow_prop");
                    break;

                case "sand":
                    if (r < 0.080)
                        SwapGround(c, Pick(("ISO_Tile_Sand_02", 3f), ("ISO_Tile_Sand_03", 2f), ("ISO_Tile_Sand_04", 1f)), "sand_var");
                    else if (r < 0.100)
                        PlaceDeco(c, Pick(("ISO_Overlay_StonePieces_01", 1f), ("ISO_Overlay_StonePieces_02", 1f)), "sand_overlay");
                    else if (r < 0.112)
                        PlaceDeco(c, Pick(("ISO_Overlay_Cracks_01", 1f), ("ISO_Overlay_Cracks_02", 1f)), "sand_cracks");
                    else if (r < 0.140)
                        PlaceProp(c, desertProps, "sand_prop");
                    break;

                case "stone":
                    if (r < 0.050)
                        SwapGround(c, Pick(("ISO_Tile_Stone_02", 1f), ("ISO_Tile_Stone_03", 1f)), "stone_var");
                    else if (r < 0.080)
                        PlaceDeco(c, Pick(("ISO_Overlay_Cracks_01", 1f), ("ISO_Overlay_Cracks_02", 1f)), "stone_cracks");
                    else if (r < 0.105)
                        PlaceDeco(c, Pick(("ISO_Overlay_StonePieces_01", 1f), ("ISO_Overlay_StonePieces_02", 1f)), "stone_overlay");
                    else if (r < 0.125)
                        PlaceProp(c, ruinsProps, "stone_prop");
                    break;

                case "lavacracks":
                    if (r < 0.050)
                        PlaceDeco(c, Pick(("ISO_Overlay_Glow_01", 1f), ("ISO_Overlay_Glow_02", 1f)), "lava_glow");
                    break;

                case "lavastone":
                    if (r < 0.040)
                        PlaceDeco(c, Pick(("ISO_Overlay_Cracks_01", 1f), ("ISO_Overlay_Cracks_02", 1f)), "lava_cracks");
                    else if (r < 0.055)
                        PlaceDeco(c, Pick(("ISO_Overlay_Glow_01", 1f), ("ISO_Overlay_Glow_02", 1f)), "lava_glow");
                    else if (r < 0.075)
                        PlaceProp(c, lavaProps, "lava_prop");
                    break;
            }
        }

        EditorSceneManager.MarkSceneDirty(grid.scene);

        sb.AppendLine("=== placement counts ===");
        foreach (var kv in counts.OrderBy(k => k.Key))
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        return sb.ToString();
    }

    static Transform RenameChild(Transform parent, string from, string to)
    {
        var t = parent.Find(to);
        if (t != null) return t;
        t = parent.Find(from);
        if (t == null) return null;
        Undo.RecordObject(t.gameObject, "Rename layer");
        t.gameObject.name = to;
        return t;
    }

    static void LoadTiles(string root)
    {
        _tiles = new Dictionary<string, TileBase>();
        foreach (var g in AssetDatabase.FindAssets("t:TileBase", new[] { root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(p);
            if (t != null && !_tiles.ContainsKey(t.name)) _tiles[t.name] = t;
        }
    }

    static void LoadFoliage(string root)
    {
        _foliage = new List<(string, Sprite)>();
        foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) _foliage.Add((s.name, s));
        }
    }
}
