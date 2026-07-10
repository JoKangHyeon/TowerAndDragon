using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 알파 맵 전체 재생성 v6 (CLAUDE.md 3·4번 예외 적용)
// 기준: 점령 지역 9×9칸, 성 중심 (0,0), 코어 지역그리드 7×7 (기획 기준)
// v6: 45도 회전 배치 — 진격로가 셀 축 방향 (타일이 변끼리 이어지는 직선 길).
//     포탈 (±27,0)/(0,±27) = 축 끝 지역 중심. 길 = 사분면(바이옴) 경계.
//     바이옴: 화면 북(+x,+y)=설원 / 남(-x,-y)=용암(화산) / 동(+x,-y)=사막 / 서(-x,+y)=암석.
//     섬 형태 — 육지 반지름 40 원, 초원 원형(반지름 13), 60칸 미만 지역 = 바다,
//     육지 최외곽 1~2칸 해안벽 + 직각 해안 코너 장식.
//     바다 버퍼 = 월드 XY 사각형 ±36×±18wu (카메라가 빈 배경을 못 보게, 셀 공간에선 45° 다이아몬드).
//     카메라 뷰포트를 이 사각형 안으로 클램프하면 빈 영역이 절대 안 보임.
public static class RebuildAlphaMap
{
    const int SEED = 26071007;
    const int HALF = 72;              // 캔버스 반경 (바다 다이아몬드 꼭짓점 |x±y|≤72 포함)
    const int RADIUS_SQ = 1600;       // 육지 경계: x²+y² ≤ 40²
    const float SEA_HALF_WIDTH_WU = 36f;   // 바다 사각형 월드 반폭 (육지 타원 ±28.3 + 여유)
    const float SEA_HALF_HEIGHT_WU = 18f;  // 바다 사각형 월드 반높이 (육지 타원 ±14.1 + 여유)
    const float CELL_TO_WORLD_X = 0.5f;    // iso cellSize (1, 0.5): wx = (x-y)·0.5
    const float CELL_TO_WORLD_Y = 0.25f;   // wy = (x+y)·0.25
    const int RIM_WALL_SQ = 1444;     // 해안벽 테두리: x²+y² > 38² (약 2칸 폭)
    const int GRASS_RADIUS_SQ = 169;  // 초원 원형: x²+y² ≤ 13²
    const int WATER_HALF = 1;         // 성 자리 물 3×3: |x|,|y| ≤ 1
    const int ROAD_MIN = 2;           // 진격로 시작 (물 바로 바깥)
    const int ROAD_MAX = 34;          // 진격로 끝 (포탈 패드 직전)
    const int PORTAL_CENTER = 36;     // 포탈 패드 중심 (±36,0)/(0,±36) = 외곽 링 지역 중심, 해안벽(d>38) 바로 안쪽
    const int PORTAL_PAD_HALF = 1;    // 패드 3×3
    const int REGION = 9;             // 지역 한 변 칸수
    const int REGION_COUNT_HALF = 4;  // 지역 인덱스 -4..4
    const int MIN_USABLE_CELLS = 60;  // 이 미만이면 지역 전체를 벽 처리
    const int CASTLE_CLEAR = 4;       // 성 주변 프랍 금지 반경 (맨해튼)
    const float PROP_MAX_HEIGHT = 0.9f;

    static System.Random _rng;
    static Dictionary<string, TileBase> _tiles;
    static List<(string name, Sprite sprite)> _foliage;

    enum Zone { Void, Sea, Water, PortalPad, Road, Grass, Snow, Sand, Rock, Lava }

    public static string Execute()
    {
        _rng = new System.Random(SEED);
        var sb = new StringBuilder();

        var grid = GameObject.Find("Grid");
        if (grid == null) return "ERROR: Grid not found";
        var groundTm = grid.transform.Find("Ground").GetComponent<Tilemap>();
        var decoTm = grid.transform.Find("Decoration").GetComponent<Tilemap>();
        var structTm = grid.transform.Find("Structures").GetComponent<Tilemap>();
        var props = grid.transform.Find("Props");

        var decoRen = decoTm.GetComponent<TilemapRenderer>();
        if (decoRen.sortingOrder < 1) decoRen.sortingOrder = 1;

        // ---------- 0. 기존 프랍 렌더러 세팅 백업 후 전체 클리어 ----------
        var refProp = props.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
        Material propMat = refProp != null ? refProp.sharedMaterial : null;
        int propLayer = refProp != null ? refProp.sortingLayerID : 0;
        int propOrder = refProp != null ? refProp.sortingOrder : 0;
        float propZ = refProp != null ? refProp.transform.position.z : 0f;

        Undo.RegisterCompleteObjectUndo(groundTm, "Rebuild map");
        Undo.RegisterCompleteObjectUndo(decoTm, "Rebuild map");
        Undo.RegisterCompleteObjectUndo(structTm, "Rebuild map");
        groundTm.ClearAllTiles();
        decoTm.ClearAllTiles();
        structTm.ClearAllTiles();
        for (int i = props.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(props.GetChild(i).gameObject);

        // ---------- 1. 에셋 로드 ----------
        LoadTiles("Assets/Imported/GoldenSkullStudios/2D/2D_Iso_Tile_Pack_Starter");
        LoadFoliage("Assets/Imported/GoldenSkullStudios/2D_Iso_FoliagePack/Sprites");
        sb.AppendLine($"tiles={_tiles.Count} foliage={_foliage.Count}");

        // ---------- 2. 존 분류 + 지역별 사용 가능 칸 집계 ----------
        var zones = new Dictionary<Vector3Int, Zone>();
        var regionUsable = new Dictionary<(int rx, int ry), int>();

        for (int y = -HALF; y <= HALF; y++)
        {
            for (int x = -HALF; x <= HALF; x++)
            {
                var z = Classify(x, y);
                zones[new Vector3Int(x, y, 0)] = z;
                if (z == Zone.Void || z == Zone.Sea) continue;
                var key = (RegionIndex(x), RegionIndex(y));
                regionUsable.TryGetValue(key, out int c);
                regionUsable[key] = c + 1;
            }
        }

        // 통벽 대상 지역 (0칸 초과, 기준 미만 — 단 특수 존 포함 지역은 안전상 제외)
        var walled = new HashSet<(int, int)>();
        foreach (var kv in regionUsable)
        {
            if (kv.Value >= MIN_USABLE_CELLS) continue;
            bool hasSpecial = false;
            for (int y = kv.Key.ry * REGION - REGION / 2; y <= kv.Key.ry * REGION + REGION / 2 && !hasSpecial; y++)
                for (int x = kv.Key.rx * REGION - REGION / 2; x <= kv.Key.rx * REGION + REGION / 2; x++)
                {
                    var z = zones[new Vector3Int(x, y, 0)];
                    if (z == Zone.Water || z == Zone.PortalPad || z == Zone.Road) { hasSpecial = true; break; }
                }
            if (!hasSpecial) walled.Add(kv.Key);
            else sb.AppendLine($"WARN: region {kv.Key} under threshold but has special zone — not walled");
        }

        bool IsLandZone(Zone z) =>
            z != Zone.Void && z != Zone.Sea && z != Zone.Water && z != Zone.PortalPad && z != Zone.Road;
        // 기준 미달 지역 → 바다로 가라앉힘
        bool IsRegionSunk(int x, int y, Zone z) =>
            IsLandZone(z) && walled.Contains((RegionIndex(x), RegionIndex(y)));
        // 육지 최외곽 해안벽 테두리
        bool IsRimWall(int x, int y, Zone z) =>
            IsLandZone(z) && x * x + y * y > RIM_WALL_SQ;

        // 가라앉은 지역이 만든 직각 해안 장식 벽 — 코너는 확정, 직선 구간은 확률
        const double COAST_WALL_STRAIGHT_CHANCE = 0.3;
        var coastWall = new HashSet<Vector3Int>();
        bool WaterAt(int wx, int wy)
        {
            if (Mathf.Abs(wx) > HALF || Mathf.Abs(wy) > HALF) return false;
            var wz = zones[new Vector3Int(wx, wy, 0)];
            if (wz == Zone.Sea) return true;
            return IsLandZone(wz) && walled.Contains((RegionIndex(wx), RegionIndex(wy)));
        }
        for (int y = -HALF; y <= HALF; y++)
        {
            for (int x = -HALF; x <= HALF; x++)
            {
                var c = new Vector3Int(x, y, 0);
                var z = zones[c];
                if (!IsLandZone(z)) continue;
                if (IsRegionSunk(x, y, z) || IsRimWall(x, y, z)) continue;
                int ortho = (WaterAt(x + 1, y) ? 1 : 0) + (WaterAt(x - 1, y) ? 1 : 0)
                          + (WaterAt(x, y + 1) ? 1 : 0) + (WaterAt(x, y - 1) ? 1 : 0);
                bool diag = WaterAt(x + 1, y + 1) || WaterAt(x + 1, y - 1)
                          || WaterAt(x - 1, y + 1) || WaterAt(x - 1, y - 1);
                if (ortho >= 2) coastWall.Add(c);              // 볼록 코너
                else if (ortho == 0 && diag) coastWall.Add(c); // 오목 코너
                else if (ortho == 1 && _rng.NextDouble() < COAST_WALL_STRAIGHT_CHANCE) coastWall.Add(c);
            }
        }

        // ---------- 3. 지면 페인트 ----------
        var zoneCounts = new Dictionary<Zone, int>();
        int wallCells = 0, sunkCells = 0;

        for (int y = -HALF; y <= HALF; y++)
        {
            for (int x = -HALF; x <= HALF; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var z = zones[cell];
                zoneCounts.TryGetValue(z, out int zc);
                zoneCounts[z] = zc + 1;
                if (z == Zone.Void) continue;

                if (IsRegionSunk(x, y, z))
                {
                    groundTm.SetTile(cell, PickGround(Zone.Sea));
                    sunkCells++;
                }
                else if (IsRimWall(x, y, z) || coastWall.Contains(cell))
                {
                    groundTm.SetTile(cell, PickWall(Wedge(x, y)));
                    wallCells++;
                }
                else
                {
                    groundTm.SetTile(cell, PickGround(z));
                }
            }
        }

        // ---------- 4. 데코 오버레이 + 프랍 ----------
        var treeCells = new HashSet<Vector3Int>();
        var counts = new Dictionary<string, int>();
        void Count(string k) { counts.TryGetValue(k, out int c); counts[k] = c + 1; }

        bool NearZone(Vector3Int c, Zone z, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var n = new Vector3Int(c.x + dx, c.y + dy, 0);
                    if (zones.TryGetValue(n, out var nz) && nz == z) return true;
                }
            return false;
        }
        bool TreeSpacingOk(Vector3Int c)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (treeCells.Contains(new Vector3Int(c.x + dx, c.y + dy, 0))) return false;
            return true;
        }
        void Overlay(Vector3Int c, string key)
        {
            string name = _rng.NextDouble() < 0.5 ? "ISO_Overlay_StonePieces_01" : "ISO_Overlay_StonePieces_02";
            if (_tiles.TryGetValue(name, out var t)) { decoTm.SetTile(c, t); Count(key); }
        }
        void Prop(Vector3Int c, string[] prefixes, string key, bool centered)
        {
            var candidates = _foliage.Where(f => prefixes.Any(px =>
                f.name.ToLowerInvariant().StartsWith(px.ToLowerInvariant()))).ToList();
            if (candidates.Count == 0) return;
            var pick = candidates[_rng.Next(candidates.Count)];
            var go = new GameObject(pick.name);
            Undo.RegisterCreatedObjectUndo(go, "Rebuild map");
            go.transform.SetParent(props, false);
            var w = groundTm.GetCellCenterWorld(c);
            float ox = centered ? 0f : ((float)_rng.NextDouble() - 0.5f) * 0.4f;
            float oy = centered ? 0f : ((float)_rng.NextDouble() - 0.5f) * 0.2f;
            go.transform.position = new Vector3(w.x + ox, w.y + oy, propZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pick.sprite;
            bool bottomPivot = pick.sprite.pivot.y < pick.sprite.rect.height * 0.3f;
            sr.spriteSortPoint = bottomPivot ? SpriteSortPoint.Pivot : SpriteSortPoint.Center;
            if (propMat != null) sr.sharedMaterial = propMat;
            sr.sortingLayerID = propLayer;
            sr.sortingOrder = propOrder;
            float h = sr.bounds.size.y;
            if (h > PROP_MAX_HEIGHT)
            {
                float f = PROP_MAX_HEIGHT / h;
                go.transform.localScale = new Vector3(f, f, 1f);
            }
            Count(key);
        }

        string[] leafTrees = { "GSS_Foliage_Forest_big_tree_leaf" };
        string[] needleTrees = { "GSS_Foliage_Forest_big_tree_needle" };
        string[] cypressTrees = { "GSS_Foliage_Forest_big_cypress" };
        string[] forestProps = { "GSS_Foliage_Forest_small_grass", "GSS_Foliage_Forest_small_flower",
            "GSS_Foliage_Forest_small_bush", "GSS_Foliage_Forest_small_fern",
            "GSS_Foliage_Forest_small_clover", "GSS_Foliage_forest_berrybush", "GSS_Foliage_forest_bush" };
        string[] snowProps = { "GSS_Foliage_stone", "GSS_Foliage_Forest_deadBush" };
        string[] desertProps = { "GSS_Foliage_Desert_small_Cactus", "GSS_Foliage_Desert_small_Agave_Cactus",
            "GSS_Foliage_Desert_small_Flower", "GSS_Foliage_Desert_small_Bush", "GSS_Foliage_desert_skull" };
        string[] ruinsProps = { "GSS_Foliage_stone", "GSS_Foliage_Forest_deadTree", "GSS_Foliage_Forest_small_mushroom" };
        string[] lavaProps = { "GSS_Foliage_Forest_deadTree", "GSS_Foliage_desert_skull", "GSS_Foliage_stone" };

        for (int y = -HALF; y <= HALF; y++)
        {
            for (int x = -HALF; x <= HALF; x++)
            {
                var c = new Vector3Int(x, y, 0);
                var z = zones[c];
                if (!IsLandZone(z)) continue;
                if (IsRegionSunk(x, y, z) || IsRimWall(x, y, z) || coastWall.Contains(c)) continue;
                if (Mathf.Abs(x) + Mathf.Abs(y) <= CASTLE_CLEAR) continue;
                if (NearZone(c, Zone.Road, 1) || NearZone(c, Zone.PortalPad, 1)) continue;

                double r = _rng.NextDouble();
                switch (z)
                {
                    case Zone.Grass:
                        if (r < 0.030 && TreeSpacingOk(c))
                        {
                            double tr = _rng.NextDouble();
                            Prop(c, tr < 0.70 ? leafTrees : (tr < 0.85 ? needleTrees : cypressTrees), "grass_tree", true);
                            treeCells.Add(c);
                        }
                        else if (r < 0.055) Prop(c, forestProps, "grass_prop", false);
                        break;
                    case Zone.Snow:
                        if (r < 0.012 && TreeSpacingOk(c))
                        {
                            Prop(c, needleTrees, "snow_tree", true);
                            treeCells.Add(c);
                        }
                        else if (r < 0.042) Overlay(c, "snow_overlay");
                        else if (r < 0.057) Prop(c, snowProps, "snow_prop", false);
                        break;
                    case Zone.Sand:
                        if (r < 0.020) Overlay(c, "sand_overlay");
                        else if (r < 0.048) Prop(c, desertProps, "sand_prop", false);
                        break;
                    case Zone.Rock:
                        if (r < 0.025) Overlay(c, "rock_overlay");
                        else if (r < 0.045) Prop(c, ruinsProps, "rock_prop", false);
                        break;
                    case Zone.Lava:
                        if (r < 0.020) Prop(c, lavaProps, "lava_prop", false);
                        break;
                }
            }
        }

        groundTm.CompressBounds();
        decoTm.CompressBounds();
        EditorSceneManager.MarkSceneDirty(grid.scene);

        var b = groundTm.cellBounds;
        sb.AppendLine($"Ground bounds: min=({b.xMin},{b.yMin}) max=({b.xMax - 1},{b.yMax - 1}) size={b.size.x}x{b.size.y}");
        sb.AppendLine("=== zones ===");
        foreach (var kv in zoneCounts.OrderBy(k => k.Key.ToString()))
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        sb.AppendLine($"sunkRegions={walled.Count} sunkCells={sunkCells} wallCells={wallCells} (coastDeco={coastWall.Count}) props={props.childCount}");
        sb.AppendLine("=== region usable cells (rx=-4..4, ry top→bottom) ===");
        for (int ry = REGION_COUNT_HALF; ry >= -REGION_COUNT_HALF; ry--)
        {
            var row = new StringBuilder();
            for (int rx = -REGION_COUNT_HALF; rx <= REGION_COUNT_HALF; rx++)
            {
                regionUsable.TryGetValue((rx, ry), out int n);
                string mark = walled.Contains((rx, ry)) ? "W" : (n == 0 ? "-" : " ");
                row.Append($"{n,3}{mark} ");
            }
            sb.AppendLine(row.ToString());
        }
        sb.AppendLine("=== placement ===");
        foreach (var kv in counts.OrderBy(k => k.Key))
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        return sb.ToString();
    }

    static int RegionIndex(int v) => Mathf.FloorToInt((v + REGION / 2) / (float)REGION);

    // 월드 XY 기준 바다 사각형 (카메라 클램프 영역) 안인지
    static bool InSeaRect(int x, int y)
    {
        float wx = (x - y) * CELL_TO_WORLD_X;
        float wy = (x + y) * CELL_TO_WORLD_Y;
        return Mathf.Abs(wx) <= SEA_HALF_WIDTH_WU && Mathf.Abs(wy) <= SEA_HALF_HEIGHT_WU;
    }

    static Zone Classify(int x, int y)
    {
        int d2 = x * x + y * y;
        if (d2 > RADIUS_SQ) return InSeaRect(x, y) ? Zone.Sea : Zone.Void;
        int ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        if (ax <= WATER_HALF && ay <= WATER_HALF) return Zone.Water;
        if ((Mathf.Abs(ax - PORTAL_CENTER) <= PORTAL_PAD_HALF && ay <= PORTAL_PAD_HALF) ||
            (Mathf.Abs(ay - PORTAL_CENTER) <= PORTAL_PAD_HALF && ax <= PORTAL_PAD_HALF)) return Zone.PortalPad;
        if ((y == 0 && ax >= ROAD_MIN && ax <= ROAD_MAX) ||
            (x == 0 && ay >= ROAD_MIN && ay <= ROAD_MAX)) return Zone.Road;
        if (d2 <= GRASS_RADIUS_SQ) return Zone.Grass;
        return Wedge(x, y);
    }

    // 사분면 바이옴 (v6): 셀(+x,+y)=설원(화면 북) / (-x,-y)=용암·화산(남) / (+x,-y)=사막(동) / (-x,+y)=암석(서)
    static Zone Wedge(int x, int y)
    {
        if (x >= 0 && y >= 0) return Zone.Snow;
        if (x >= 0) return Zone.Sand;
        if (y >= 0) return Zone.Rock;
        return Zone.Lava;
    }

    static TileBase PickWall(Zone wedge)
    {
        switch (wedge)
        {
            case Zone.Snow: return T("ISO_Tile_Brick_Snow_01");
            case Zone.Sand: return T("ISO_Tile_Brick_SandStone_01");
            case Zone.Lava:
                return T(Pick(("ISO_Tile_Brick_Brick_01", 55f), ("ISO_Tile_Brick_Brick_02", 15f),
                    ("ISO_Tile_Brick_Brick_03", 15f), ("ISO_Tile_Brick_Brick_04", 15f)));
            default:
                return T(Pick(("ISO_Tile_Brick_Stone_01", 70f), ("ISO_Tile_Brick_Stone_01_02", 10f),
                    ("ISO_Tile_Brick_Stone_01_03", 10f), ("ISO_Tile_Brick_Stone_01_04", 5f),
                    ("ISO_Tile_Brick_Stone_01_05", 5f)));
        }
    }

    static TileBase PickGround(Zone z)
    {
        switch (z)
        {
            case Zone.Sea:
            case Zone.Water: return T("ISO_Autotile_Water_Shores_01_Simple");
            case Zone.PortalPad: return T(_rng.NextDouble() < 0.6 ? "ISO_Tile_Tar_01" : "ISO_Tile_Tar_02");
            case Zone.Road: return T("ISO_Tile_Dirt_02");
            case Zone.Grass:
                return T(Pick(("ISO_Tile_Dirt_01_Grass_01", 93f), ("ISO_Tile_Dirt_01_GrassPatch_01", 3f),
                    ("ISO_Tile_Dirt_01_GrassPatch_02", 2f), ("ISO_Tile_Dirt_01_GrassPatch_03", 2f)));
            case Zone.Snow:
                return T(Pick(("ISO_Tile_Snow_01", 88f), ("ISO_Tile_Snow_02", 12f)));
            case Zone.Sand:
                return T(Pick(("ISO_Tile_Sand_01", 82f), ("ISO_Tile_Sand_02", 9f),
                    ("ISO_Tile_Sand_03", 6f), ("ISO_Tile_Sand_04", 3f)));
            case Zone.Rock:
                return T(Pick(("ISO_Tile_Stone_01", 90f), ("ISO_Tile_Stone_02", 6f), ("ISO_Tile_Stone_03", 4f)));
            default:
                return T(Pick(("ISO_Tile_LavaCracks_01", 68f), ("ISO_Tile_LavaStone_01", 28f),
                    ("ISO_Tile_Lava_01", 2.5f), ("ISO_Tile_Lava_02", 1.5f)));
        }
    }

    static TileBase T(string name) => _tiles.TryGetValue(name, out var t) ? t : null;

    static string Pick(params (string name, float w)[] items)
    {
        float total = items.Sum(i => i.w);
        float r = (float)_rng.NextDouble() * total;
        foreach (var i in items) { if ((r -= i.w) <= 0f) return i.name; }
        return items[items.Length - 1].name;
    }

    static void LoadTiles(string root)
    {
        _tiles = new Dictionary<string, TileBase>();
        foreach (var g in AssetDatabase.FindAssets("t:TileBase", new[] { root }))
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g));
            if (t != null && !_tiles.ContainsKey(t.name)) _tiles[t.name] = t;
        }
    }

    static void LoadFoliage(string root)
    {
        _foliage = new List<(string, Sprite)>();
        foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { root }))
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
            if (s != null) _foliage.Add((s.name, s));
        }
    }
}
