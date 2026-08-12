using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 청크 레이아웃(셀→청크 소속) 저작 도구.
// 에디터 전용이므로 문자열·숫자 리터럴 제한(CLAUDE.md 3·4항)의 예외 대상이다.
public class ChunkLayoutEditorWindow : EditorWindow
{
    // 자유 형태 전환 이전의 9×9 규칙 사본 - 기존 맵을 그대로 에셋으로 굽는 부트스트랩과
    // 신규 맵의 초안 생성에 쓴다. 런타임(GridMap)에는 더 이상 이 규칙이 존재하지 않는다.
    private const int LEGACY_CHUNK_SIZE = 9;
    private const int LEGACY_CHUNK_ORIGIN_OFFSET = (LEGACY_CHUNK_SIZE - 1) / 2;

    private const string ENTRIES_PROPERTY = "_entries";
    private const string CHUNK_COORD_PROPERTY = "ChunkCoord";
    private const string CELL_COORDS_PROPERTY = "CellCoords";
    private const string TERRAIN_TILE_MAP_PROPERTY = "_terrainTileMap";

    // 물은 맵 경계 장식이라 어느 청크에도 속하지 않는다 - TerrainType.Default가 곧 물이다
    // (TerrainTileMap에 Default로 등록된 타일은 물 오토타일 둘뿐이고, 흙은 Road로 간다).
    private const TerrainType WATER_TERRAIN = TerrainType.Default;

    // 길은 바이옴이 아니다 - 청크를 가르는 기준에서 빼고, 다 만든 뒤 인접 청크에 흡수시킨다.
    private const TerrainType ROAD_TERRAIN = TerrainType.Road;

    // 가장 넓은 청크 대비 이 비율 "미만"으로 좁은 청크는 독립 점령 대상으로 두지 않고 이웃에 합친다.
    // 9×9 정원(81칸)이 아니라 실제 최대 넓이를 기준으로 삼는다 - 물 제외와 바이옴 분할을 거치고 나면
    // 청크 크기가 들쭉날쭉해져서, 고정 기준으로는 "옆 청크에 비해 자투리"인 것들이 걸러지지 않는다.
    private const int SMALL_CHUNK_PERCENT = 40;

    private ChunkLayoutTable _table;
    private GridMap _gridMap;
    private Vector2 _scrollPosition;
    private string _report = string.Empty;

    private readonly ChunkLayoutBrush _brush = new();
    private bool _isPaintModeActive;

    [MenuItem("TowerAndDragon/청크 레이아웃 편집기")]
    private static void Open() => GetWindow<ChunkLayoutEditorWindow>("청크 레이아웃");

    private void OnEnable()
    {
        if (_gridMap == null)
            _gridMap = FindFirstObjectByType<GridMap>();
    }

    private void OnDisable() => SetPaintModeActive(false);

    private void OnGUI()
    {
        _table = (ChunkLayoutTable)EditorGUILayout.ObjectField("레이아웃 에셋", _table, typeof(ChunkLayoutTable), false);
        _gridMap = (GridMap)EditorGUILayout.ObjectField("GridMap (씬)", _gridMap, typeof(GridMap), true);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(_table == null || _gridMap == null))
        {
            if (GUILayout.Button("현재 9×9 규칙으로 굽기"))
                BakeFromLegacyRule();

            if (GUILayout.Button("9×9 + 바이옴 단일화로 굽기 (길 제외, 조각은 인근에 흡수)"))
                BakeByBiome();

            if (GUILayout.Button("검증"))
                Validate();

            if (GUILayout.Button("인접 그래프 diff (구 격자 산술 ↔ 신 셀 접촉)"))
                DiffAdjacency();
        }

        if (_table == null || _gridMap == null)
            EditorGUILayout.HelpBox("레이아웃 에셋과 씬의 GridMap을 모두 지정하세요.", MessageType.Info);

        EditorGUILayout.Space();
        DrawPaintModeSection();
        EditorGUILayout.Space();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ---------- 씬뷰 페인팅 ----------

    private void DrawPaintModeSection()
    {
        using (new EditorGUI.DisabledScope(_table == null || _gridMap == null))
        {
            bool wantsPaintMode = EditorGUILayout.ToggleLeft("씬뷰 페인팅 모드", _isPaintModeActive, EditorStyles.boldLabel);
            if (wantsPaintMode != _isPaintModeActive)
                SetPaintModeActive(wantsPaintMode);
        }

        if (!_isPaintModeActive)
            return;

        EditorGUILayout.HelpBox(
            "활성 청크는 초록, 나머지 청크는 각자 고유색으로 칠해집니다.\n" +
            "좌클릭 드래그: 활성 청크로 칠하기\n" +
            "Ctrl + 클릭: 커서 아래 청크를 활성으로 집기(스포이드)\n" +
            "Shift + 드래그: 소속 지우기(런타임에서 근접 청크로 흡수됨)\n" +
            "획을 놓을 때마다 에셋에 기록되며 Ctrl+Z로 되돌릴 수 있습니다.",
            MessageType.None);

        var chunkCoords = new List<Vector2Int>(_brush.ChunkCoords);
        chunkCoords.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        string[] labels = chunkCoords.Select(coord => $"({coord.x}, {coord.y})").ToArray();
        int currentIndex = chunkCoords.IndexOf(_brush.ActiveChunkCoord);

        int nextIndex = EditorGUILayout.Popup("활성 청크", currentIndex, labels);
        if (nextIndex >= 0 && nextIndex < chunkCoords.Count && nextIndex != currentIndex)
        {
            _brush.ActiveChunkCoord = chunkCoords[nextIndex];
            SceneView.RepaintAll();
        }

        // 활성 청크 색을 그대로 보여줘 씬뷰에서 어느 덩어리를 칠하고 있는지 눈으로 잇는다.
        Rect swatchRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        EditorGUI.DrawRect(swatchRect, ChunkLayoutBrush.ResolveChunkColor(_brush.ActiveChunkCoord, true));
        EditorGUI.LabelField(swatchRect, $"  셀 {_brush.ActiveChunkCellCount}개 / 청크 {_brush.ChunkCount}개");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("새 청크 추가"))
            {
                Vector2Int created = _brush.CreateChunkCoord();
                _report = $"새 청크 {created} 를 만들었습니다. 씬뷰에서 셀을 칠해 채우세요.";
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("빈 청크 정리"))
            {
                int removed = _brush.RemoveEmptyChunks();
                _report = removed > 0 ? $"빈 청크 {removed}개를 지웠습니다." : "빈 청크가 없습니다.";
                SceneView.RepaintAll();
            }
        }
    }

    private void SetPaintModeActive(bool isActive)
    {
        if (isActive == _isPaintModeActive)
            return;

        // 켤 때만 유효성을 따진다 - 끄는 쪽은 언제나 안전해야 한다(창이 닫힐 때도 불린다).
        if (isActive && (_table == null || _gridMap == null))
            return;

        _isPaintModeActive = isActive;

        if (isActive)
            _brush.Begin(_table, _gridMap);
        else
            _brush.End();
    }

    // ---------- 굽기 ----------

    private static Vector2Int LegacyToChunkCoord(Vector3Int cellCoord) =>
        new Vector2Int(
            LegacyFloorDiv(cellCoord.x + LEGACY_CHUNK_ORIGIN_OFFSET, LEGACY_CHUNK_SIZE),
            LegacyFloorDiv(cellCoord.y + LEGACY_CHUNK_ORIGIN_OFFSET, LEGACY_CHUNK_SIZE));

    private static int LegacyFloorDiv(int value, int divisor) =>
        (value >= 0) ? value / divisor : (value - divisor + 1) / divisor;

    private void BakeFromLegacyRule()
    {
        List<Vector3Int> cellCoords = CollectTilemapCells();
        HashSet<Vector3Int> waterCells = CollectWaterCells();
        var grouped = new Dictionary<Vector2Int, List<Vector2Int>>();

        foreach (Vector3Int cellCoord in cellCoords)
        {
            // 물은 청크에 넣지 않는다 - 런타임에서도 점령·생산 대상이 아니다.
            if (waterCells.Contains(cellCoord))
                continue;

            Vector2Int chunkCoord = LegacyToChunkCoord(cellCoord);
            if (!grouped.TryGetValue(chunkCoord, out List<Vector2Int> cells))
            {
                cells = new List<Vector2Int>();
                grouped[chunkCoord] = cells;
            }

            cells.Add(new Vector2Int(cellCoord.x, cellCoord.y));
        }

        int chunkCountBeforeDissolve = grouped.Count;
        int dissolvedCount = DissolveUntilStable(
            grouped, out int movedCellCount, out List<Vector2Int> strandedChunks,
            out int cellThreshold, out int passCount);

        WriteLayout(grouped);

        int bakedCellCount = cellCoords.Count - waterCells.Count;

        var builder = new StringBuilder();
        builder.AppendLine("굽기 완료");
        builder.AppendLine($"청크 {grouped.Count}개 / 셀 {bakedCellCount}개");
        builder.AppendLine($"물 타일 {waterCells.Count}개는 제외했습니다(전체 {cellCoords.Count}개 중).");
        builder.AppendLine(
            $"최대 넓이의 {SMALL_CHUNK_PERCENT}%({cellThreshold}칸) 미만인 조각 청크 {dissolvedCount}개를 해체해 " +
            $"셀 {movedCellCount}개를 인접 청크로 나눴습니다 ({chunkCountBeforeDissolve} → {grouped.Count}개, {passCount}회차).");

        if (strandedChunks.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"※ 받아줄 이웃이 없어 그대로 남긴 조각 청크 {strandedChunks.Count}개:");
            foreach (Vector2Int coord in strandedChunks.Take(20))
                builder.AppendLine($"    {coord} ({grouped[coord].Count}칸)");
        }

        builder.AppendLine();
        builder.AppendLine("다음 순서로 진행하세요: [검증] → [인접 그래프 diff]");
        builder.AppendLine("diff의 '없어진 변'이 0이 아니면 런타임 전환 전에 레이아웃에서 해결해야 합니다.");

        _report = builder.ToString();
        Debug.Log($"[ChunkLayoutEditorWindow] 굽기 완료 - 청크 {grouped.Count}, 셀 {bakedCellCount}, " +
                  $"물 제외 {waterCells.Count}, 조각 청크 해체 {dissolvedCount}, 이동한 셀 {movedCellCount}");
    }

    // ---------- 굽기: 9×9 + 바이옴 단일화 ----------

    // 9×9로 나누되, 한 청크에 바이옴이 섞이지 않게 블록 안에서 지형별로 한 번 더 쪼갠다.
    // 길(Road)은 바이옴이 아니므로 쪼개는 기준이 되지 않고, 다 만든 뒤 맞닿은 청크에 흡수시킨다.
    private void BakeByBiome()
    {
        Dictionary<Vector3Int, TerrainType> terrainByCell = CollectTerrainByCell();

        var biomeCells = new List<Vector3Int>();
        var roadCells = new List<Vector3Int>();
        int waterCount = 0;

        foreach (KeyValuePair<Vector3Int, TerrainType> pair in terrainByCell)
        {
            if (pair.Value == WATER_TERRAIN)
            {
                waterCount++;
                continue;
            }

            if (pair.Value == ROAD_TERRAIN)
                roadCells.Add(pair.Key);
            else
                biomeCells.Add(pair.Key);
        }

        // ① 블록 × 바이옴으로 묶고, 그 안에서 다시 4방향 연결 덩어리로 쪼갠다.
        //    (같은 블록·같은 바이옴이어도 떨어져 있으면 별개 청크여야 테두리가 두 갈래로 갈라지지 않는다)
        var blockBiomeGroups = new Dictionary<(Vector2Int Block, TerrainType Biome), List<Vector3Int>>();
        foreach (Vector3Int cell in biomeCells)
        {
            var key = (LegacyToChunkCoord(cell), terrainByCell[cell]);
            if (!blockBiomeGroups.TryGetValue(key, out List<Vector3Int> cells))
            {
                cells = new List<Vector3Int>();
                blockBiomeGroups[key] = cells;
            }

            cells.Add(cell);
        }

        // 길은 같은 블록 안에서만 이음길 노릇을 한다 - 블록 밖으로 돌아 이어지면 청크가 끊겨 보인다.
        var roadCellsByBlock = new Dictionary<Vector2Int, HashSet<Vector3Int>>();
        foreach (Vector3Int cell in roadCells)
        {
            Vector2Int block = LegacyToChunkCoord(cell);
            if (!roadCellsByBlock.TryGetValue(block, out HashSet<Vector3Int> cells))
            {
                cells = new HashSet<Vector3Int>();
                roadCellsByBlock[block] = cells;
            }

            cells.Add(cell);
        }

        var components = new List<(Vector2Int Block, TerrainType Biome, List<Vector3Int> Cells)>();
        foreach (KeyValuePair<(Vector2Int Block, TerrainType Biome), List<Vector3Int>> pair in blockBiomeGroups)
        {
            if (!roadCellsByBlock.TryGetValue(pair.Key.Block, out HashSet<Vector3Int> connectors))
                connectors = new HashSet<Vector3Int>();

            foreach (List<Vector3Int> component in SplitIntoConnectedComponents(pair.Value, connectors))
                components.Add((pair.Key.Block, pair.Key.Biome, component));
        }

        // ② 청크 좌표 배정 - 블록 안에서 가장 큰 덩어리가 블록 좌표를 그대로 쓰고,
        //    나머지는 비어 있는 좌표를 원점에서 가까운 순으로 받아간다(결과가 매번 같도록 정렬 후 배정).
        var grouped = new Dictionary<Vector2Int, List<Vector2Int>>();
        var chunkBiome = new Dictionary<Vector2Int, TerrainType>();
        AssignChunkCoords(components, grouped, chunkBiome);

        // ③ 길 흡수 - 청크에 맞닿은 길부터 파도처럼 번지며 배분한다.
        int assignedRoadCount = AbsorbRoadCells(grouped, roadCells);

        // ④ 조각 청크 해체 - 같은 바이옴 이웃을 우선해 합친다.
        int chunkCountBeforeDissolve = grouped.Count;
        int dissolvedCount = DissolveUntilStable(
            grouped, out int movedCellCount, out List<Vector2Int> strandedChunks,
            out int cellThreshold, out int passCount, chunkBiome);

        WriteLayout(grouped);

        int mixedChunkCount = CountMixedBiomeChunks(grouped, terrainByCell);

        var builder = new StringBuilder();
        builder.AppendLine("굽기 완료 (9×9 + 바이옴 단일화)");
        builder.AppendLine($"청크 {grouped.Count}개 / 셀 {biomeCells.Count + assignedRoadCount}개");
        builder.AppendLine($"물 {waterCount}개 제외, 길 {roadCells.Count}개 중 {assignedRoadCount}개를 인접 청크에 흡수시켰습니다.");
        builder.AppendLine($"바이옴으로 쪼갠 결과 {components.Count}개 → 조각 청크 {dissolvedCount}개 해체(셀 {movedCellCount}개 이동) → {grouped.Count}개");
        builder.AppendLine($"해체 기준: 가장 넓은 청크의 {SMALL_CHUNK_PERCENT}% = {cellThreshold}칸 미만 ({passCount}회차에 안정)");
        builder.AppendLine();
        builder.AppendLine(mixedChunkCount == 0
            ? "바이옴이 섞인 청크: 없음"
            : $"바이옴이 섞인 청크: {mixedChunkCount}개 (같은 바이옴 이웃이 없어 부득이하게 합쳐진 조각)");

        if (strandedChunks.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"※ 받아줄 이웃이 없어 그대로 남긴 조각 청크 {strandedChunks.Count}개:");
            foreach (Vector2Int coord in strandedChunks.Take(20))
                builder.AppendLine($"    {coord} ({grouped[coord].Count}칸)");
        }

        builder.AppendLine();
        builder.AppendLine("다음 순서로 진행하세요: [검증] → [인접 그래프 diff]");

        _report = builder.ToString();
        Debug.Log($"[ChunkLayoutEditorWindow] 바이옴 굽기 완료 - 청크 {grouped.Count}, " +
                  $"해체 {dissolvedCount}, 길 흡수 {assignedRoadCount}, 혼합 청크 {mixedChunkCount}");
    }

    // 4방향으로 이어진 덩어리별로 쪼갠다. connectors(같은 블록의 길)는 덩어리에 포함되지 않지만
    // 통과는 할 수 있다 - 길은 바이옴이 아니므로 바이옴을 갈라놓는 벽도 아니어야 한다.
    // (길을 벽으로 두면 성이 있는 블록 (0,0)의 초원 48칸이 4조각으로 잘려 전부 해체돼 버린다)
    private static List<List<Vector3Int>> SplitIntoConnectedComponents(
        List<Vector3Int> biomeCells, HashSet<Vector3Int> connectors)
    {
        var remainingBiome = new HashSet<Vector3Int>(biomeCells);
        var result = new List<List<Vector3Int>>();

        while (remainingBiome.Count > 0)
        {
            Vector3Int seed = remainingBiome.First();
            var component = new List<Vector3Int>();
            var visited = new HashSet<Vector3Int> { seed };
            var frontier = new Stack<Vector3Int>();
            frontier.Push(seed);

            while (frontier.Count > 0)
            {
                Vector3Int current = frontier.Pop();

                if (remainingBiome.Remove(current))
                    component.Add(current);

                foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
                {
                    Vector3Int neighbor = current + direction;

                    if (!visited.Add(neighbor))
                        continue;

                    if (remainingBiome.Contains(neighbor) || connectors.Contains(neighbor))
                        frontier.Push(neighbor);
                }
            }

            result.Add(component);
        }

        return result;
    }

    // 블록 좌표는 그 블록에서 가장 큰 덩어리가 물려받고, 나머지는 빈 좌표를 나눠 받는다.
    private static void AssignChunkCoords(
        List<(Vector2Int Block, TerrainType Biome, List<Vector3Int> Cells)> components,
        Dictionary<Vector2Int, List<Vector2Int>> grouped,
        Dictionary<Vector2Int, TerrainType> chunkBiome)
    {
        // 큰 덩어리 우선, 동점이면 좌표순 - 같은 입력이면 항상 같은 결과가 나오게 한다.
        List<(Vector2Int Block, TerrainType Biome, List<Vector3Int> Cells)> ordered = components
            .OrderByDescending(entry => entry.Cells.Count)
            .ThenBy(entry => entry.Block.x).ThenBy(entry => entry.Block.y)
            .ThenBy(entry => entry.Biome)
            .ToList();

        var takenBlockCoords = new HashSet<Vector2Int>();
        var overflow = new List<(Vector2Int Block, TerrainType Biome, List<Vector3Int> Cells)>();

        foreach ((Vector2Int block, TerrainType biome, List<Vector3Int> cells) in ordered)
        {
            if (!takenBlockCoords.Add(block))
            {
                overflow.Add((block, biome, cells));
                continue;
            }

            grouped[block] = cells.Select(cell => new Vector2Int(cell.x, cell.y)).ToList();
            chunkBiome[block] = biome;
        }

        // 남은 덩어리에 빈 좌표를 원점에서 가까운 순으로 배정한다.
        int ring = 0;
        var freeCoords = new Queue<Vector2Int>();

        foreach ((Vector2Int _, TerrainType biome, List<Vector3Int> cells) in overflow)
        {
            while (freeCoords.Count == 0)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                            continue;

                        var candidate = new Vector2Int(x, y);
                        if (!grouped.ContainsKey(candidate) && !takenBlockCoords.Contains(candidate))
                            freeCoords.Enqueue(candidate);
                    }
                }

                ring++;
            }

            Vector2Int coord = freeCoords.Dequeue();
            takenBlockCoords.Add(coord);
            grouped[coord] = cells.Select(cell => new Vector2Int(cell.x, cell.y)).ToList();
            chunkBiome[coord] = biome;
        }
    }

    // 길은 바이옴이 아니므로 청크를 가르지 않는다 - 맞닿은 청크에서 번져 나가며 나눠 갖는다.
    private static int AbsorbRoadCells(Dictionary<Vector2Int, List<Vector2Int>> grouped, List<Vector3Int> roadCells)
    {
        var cellToChunk = new Dictionary<Vector2Int, Vector2Int>();
        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in grouped)
        {
            foreach (Vector2Int cell in pair.Value)
                cellToChunk[cell] = pair.Key;
        }

        var remaining = new HashSet<Vector2Int>(roadCells.Select(cell => new Vector2Int(cell.x, cell.y)));
        var receivedCount = new Dictionary<Vector2Int, int>();
        var frontier = new List<(Vector2Int Cell, Vector2Int Receiver)>();

        foreach (Vector2Int cell in remaining)
        {
            foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
            {
                var neighbor = new Vector2Int(cell.x + direction.x, cell.y + direction.y);
                if (cellToChunk.TryGetValue(neighbor, out Vector2Int owner))
                    frontier.Add((cell, owner));
            }
        }

        int assignedCount = 0;

        while (frontier.Count > 0)
        {
            frontier.Sort((a, b) =>
                GetReceivedCount(receivedCount, a.Receiver).CompareTo(GetReceivedCount(receivedCount, b.Receiver)));

            var nextFrontier = new List<(Vector2Int Cell, Vector2Int Receiver)>();

            foreach ((Vector2Int cell, Vector2Int receiver) in frontier)
            {
                if (!remaining.Remove(cell))
                    continue;

                grouped[receiver].Add(cell);
                receivedCount[receiver] = GetReceivedCount(receivedCount, receiver) + 1;
                assignedCount++;

                foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
                {
                    var next = new Vector2Int(cell.x + direction.x, cell.y + direction.y);
                    if (remaining.Contains(next))
                        nextFrontier.Add((next, receiver));
                }
            }

            frontier = nextFrontier;
        }

        return assignedCount;
    }

    // 길을 뺀 실제 바이옴이 둘 이상 섞인 청크 수 - 단일 바이옴 규칙이 지켜졌는지 확인용.
    private static int CountMixedBiomeChunks(
        Dictionary<Vector2Int, List<Vector2Int>> grouped, Dictionary<Vector3Int, TerrainType> terrainByCell)
    {
        int mixedCount = 0;

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in grouped)
        {
            var biomes = new HashSet<TerrainType>();
            foreach (Vector2Int cell in pair.Value)
            {
                if (terrainByCell.TryGetValue(new Vector3Int(cell.x, cell.y, 0), out TerrainType terrain) &&
                    terrain != ROAD_TERRAIN && terrain != WATER_TERRAIN)
                {
                    biomes.Add(terrain);
                }
            }

            if (biomes.Count > 1)
                mixedCount++;
        }

        return mixedCount;
    }

    private List<Vector3Int> CollectTilemapCells()
    {
        var result = new List<Vector3Int>();
        Tilemap tilemap = _gridMap.TerrainTilemap;

        if (tilemap == null)
        {
            _report = "GridMap의 지형 Tilemap이 비어 있습니다.";
            return result;
        }

        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
                result.Add(new Vector3Int(pos.x, pos.y, 0));
        }

        return result;
    }

    // 너무 작은 청크를 해체해 맞닿은 정상 청크들에 나눠 붙인다.
    // 배분은 이웃들에서 동시에 번지는 너비 우선 탐색이라, 두 청크 사이에 낀 띠 모양이면
    // 가운데서 만나 자연히 반반으로 갈린다. 같은 깊이에서 여러 이웃이 닿으면 지금까지 적게 받은
    // 쪽을 먼저 집어 치우친 배분을 막는다.
    // chunkBiome을 넘기면 같은 바이옴 이웃에게 먼저 보낸다(단일 바이옴 규칙 유지). 같은 바이옴 이웃이
    // 하나도 없을 때만 다른 바이옴에 합쳐지고, 그 결과는 CountMixedBiomeChunks가 리포트로 드러낸다.
    private static int DissolveSmallChunks(
        Dictionary<Vector2Int, List<Vector2Int>> grouped,
        int cellThreshold,
        out int movedCellCount,
        out List<Vector2Int> strandedChunks,
        Dictionary<Vector2Int, TerrainType> chunkBiome = null)
    {
        movedCellCount = 0;
        strandedChunks = new List<Vector2Int>();

        var cellToChunk = new Dictionary<Vector2Int, Vector2Int>();
        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in grouped)
        {
            foreach (Vector2Int cell in pair.Value)
                cellToChunk[cell] = pair.Key;
        }

        // 해체 대상은 시작 시점 크기로 한 번에 정한다 - 받아서 커진 청크가 도중에 대상에서
        // 빠지거나 들어오는 순서 의존을 없애기 위함. 작은 것부터 처리한다.
        var smallChunkCoords = new HashSet<Vector2Int>(grouped
            .Where(pair => pair.Value.Count < cellThreshold)
            .Select(pair => pair.Key));

        List<Vector2Int> processOrder = smallChunkCoords
            .OrderBy(coord => grouped[coord].Count)
            .ThenBy(coord => coord.x).ThenBy(coord => coord.y)
            .ToList();

        int dissolvedCount = 0;

        foreach (Vector2Int smallChunk in processOrder)
        {
            List<Vector2Int> cells = grouped[smallChunk];
            var remaining = new HashSet<Vector2Int>(cells);
            var assignment = new Dictionary<Vector2Int, Vector2Int>();
            var receivedCount = new Dictionary<Vector2Int, int>();

            // 첫 파도 - 해체되지 않고 살아남는 이웃 청크와 맞닿은 칸들
            List<(Vector2Int Cell, Vector2Int Receiver)> frontier =
                BuildReceiverFrontier(cells, cellToChunk, smallChunk, smallChunkCoords);

            // 맞닿은 이웃이 전부 해체 대상이면 그중 아무에게나 붙인다 - 서로 미루다 1칸짜리로 남는 것보다는
            // 합쳐 놓고 다음 회차에서 다시 판정하는 편이 낫다.
            if (frontier.Count == 0)
                frontier = BuildReceiverFrontier(cells, cellToChunk, smallChunk, null);

            if (frontier.Count == 0)
            {
                strandedChunks.Add(smallChunk);
                continue;
            }

            // 같은 바이옴 이웃이 하나라도 있으면 그쪽만 후보로 남긴다.
            if (chunkBiome != null && chunkBiome.TryGetValue(smallChunk, out TerrainType ownBiome))
            {
                List<(Vector2Int Cell, Vector2Int Receiver)> sameBiome = frontier
                    .Where(entry => chunkBiome.TryGetValue(entry.Receiver, out TerrainType biome) && biome == ownBiome)
                    .ToList();

                if (sameBiome.Count > 0)
                    frontier = sameBiome;
            }

            while (frontier.Count > 0)
            {
                frontier.Sort((a, b) =>
                    GetReceivedCount(receivedCount, a.Receiver).CompareTo(GetReceivedCount(receivedCount, b.Receiver)));

                var nextFrontier = new List<(Vector2Int Cell, Vector2Int Receiver)>();

                foreach ((Vector2Int cell, Vector2Int receiver) in frontier)
                {
                    if (!remaining.Remove(cell))
                        continue;

                    assignment[cell] = receiver;
                    receivedCount[receiver] = GetReceivedCount(receivedCount, receiver) + 1;

                    foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
                    {
                        var next = new Vector2Int(cell.x + direction.x, cell.y + direction.y);
                        if (remaining.Contains(next))
                            nextFrontier.Add((next, receiver));
                    }
                }

                frontier = nextFrontier;
            }

            // 청크 안에서 끊겨 파도가 닿지 못한 조각 - 가장 가까운 수령 청크에 붙인다.
            foreach (Vector2Int cell in remaining)
            {
                Vector2Int nearest = FindNearestReceiver(cell, receivedCount.Keys, grouped);
                assignment[cell] = nearest;
                receivedCount[nearest] = GetReceivedCount(receivedCount, nearest) + 1;
            }

            foreach (KeyValuePair<Vector2Int, Vector2Int> pair in assignment)
            {
                grouped[pair.Value].Add(pair.Key);
                cellToChunk[pair.Key] = pair.Value;
                movedCellCount++;
            }

            grouped.Remove(smallChunk);
            dissolvedCount++;
        }

        return dissolvedCount;
    }

    private static List<(Vector2Int Cell, Vector2Int Receiver)> BuildReceiverFrontier(
        List<Vector2Int> cells,
        Dictionary<Vector2Int, Vector2Int> cellToChunk,
        Vector2Int smallChunk,
        HashSet<Vector2Int> excludedReceivers)
    {
        var frontier = new List<(Vector2Int Cell, Vector2Int Receiver)>();

        foreach (Vector2Int cell in cells)
        {
            foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
            {
                var neighbor = new Vector2Int(cell.x + direction.x, cell.y + direction.y);

                if (!cellToChunk.TryGetValue(neighbor, out Vector2Int owner) || owner == smallChunk)
                    continue;

                if (excludedReceivers != null && excludedReceivers.Contains(owner))
                    continue;

                frontier.Add((cell, owner));
            }
        }

        return frontier;
    }

    // 자투리가 하나도 안 남을 때까지 반복해 합친다. 합칠수록 받은 청크가 넓어져 최대 넓이가 올라가고,
    // 그러면 새 기준에서 자투리가 되는 청크가 또 생기기 때문에 한 번만 돌려서는 기준을 만족하지 못한다.
    // 매 회차마다 청크 수가 최소 1개씩 줄어드므로 반드시 끝난다.
    private static int DissolveUntilStable(
        Dictionary<Vector2Int, List<Vector2Int>> grouped,
        out int movedCellCount,
        out List<Vector2Int> strandedChunks,
        out int finalThreshold,
        out int passCount,
        Dictionary<Vector2Int, TerrainType> chunkBiome = null)
    {
        int dissolvedTotal = 0;
        movedCellCount = 0;
        passCount = 0;

        while (true)
        {
            finalThreshold = CalculateSmallChunkThreshold(grouped);
            int dissolved = DissolveSmallChunks(
                grouped, finalThreshold, out int moved, out strandedChunks, chunkBiome);

            dissolvedTotal += dissolved;
            movedCellCount += moved;
            passCount++;

            if (dissolved == 0)
                return dissolvedTotal;
        }
    }

    // 해체 기준선 - 가장 넓은 청크의 SMALL_CHUNK_PERCENT%.
    private static int CalculateSmallChunkThreshold(Dictionary<Vector2Int, List<Vector2Int>> grouped)
    {
        int maxCellCount = 0;

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in grouped)
            maxCellCount = Mathf.Max(maxCellCount, pair.Value.Count);

        return maxCellCount * SMALL_CHUNK_PERCENT / 100;
    }

    private static int GetReceivedCount(Dictionary<Vector2Int, int> counts, Vector2Int chunkCoord) =>
        counts.TryGetValue(chunkCoord, out int count) ? count : 0;

    private static Vector2Int FindNearestReceiver(
        Vector2Int cell, IEnumerable<Vector2Int> receivers, Dictionary<Vector2Int, List<Vector2Int>> grouped)
    {
        Vector2Int best = default;
        float bestDistance = float.MaxValue;

        foreach (Vector2Int receiver in receivers)
        {
            foreach (Vector2Int receiverCell in grouped[receiver])
            {
                float distance = (receiverCell - cell).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = receiver;
            }
        }

        return best;
    }

    // 셀별 지형. GridMap._terrainTileMap은 private [SerializeField]이고, 런타임 GetTerrainType은
    // 플레이 중에만 채워지는 _cells에 의존하므로 에디터에서는 타일에서 직접 푼다.
    private Dictionary<Vector3Int, TerrainType> CollectTerrainByCell()
    {
        var result = new Dictionary<Vector3Int, TerrainType>();
        Tilemap tilemap = _gridMap.TerrainTilemap;

        var serializedGridMap = new SerializedObject(_gridMap);
        var terrainTileMap =
            serializedGridMap.FindProperty(TERRAIN_TILE_MAP_PROPERTY).objectReferenceValue as TerrainTileMap;

        if (tilemap == null || terrainTileMap == null)
            return result;

        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos))
                continue;

            result[new Vector3Int(pos.x, pos.y, 0)] = terrainTileMap.Resolve(tilemap.GetTile(pos));
        }

        return result;
    }

    private HashSet<Vector3Int> CollectWaterCells()
    {
        var result = new HashSet<Vector3Int>();

        foreach (KeyValuePair<Vector3Int, TerrainType> pair in CollectTerrainByCell())
        {
            if (pair.Value == WATER_TERRAIN)
                result.Add(pair.Key);
        }

        return result;
    }

    // 청크 좌표와 셀 좌표를 모두 정렬해 저장한다 - 다시 구웠을 때 git diff가 실제 변경만 보여주게 하기 위함.
    private void WriteLayout(Dictionary<Vector2Int, List<Vector2Int>> grouped)
    {
        var serializedTable = new SerializedObject(_table);
        SerializedProperty entriesProperty = serializedTable.FindProperty(ENTRIES_PROPERTY);

        List<Vector2Int> orderedChunkCoords = grouped.Keys
            .OrderBy(coord => coord.x).ThenBy(coord => coord.y).ToList();

        entriesProperty.ClearArray();
        entriesProperty.arraySize = orderedChunkCoords.Count;

        for (int i = 0; i < orderedChunkCoords.Count; i++)
        {
            Vector2Int chunkCoord = orderedChunkCoords[i];
            SerializedProperty element = entriesProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(CHUNK_COORD_PROPERTY).vector2IntValue = chunkCoord;

            List<Vector2Int> cells = grouped[chunkCoord];
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            SerializedProperty cellsProperty = element.FindPropertyRelative(CELL_COORDS_PROPERTY);
            cellsProperty.ClearArray();
            cellsProperty.arraySize = cells.Count;

            for (int j = 0; j < cells.Count; j++)
                cellsProperty.GetArrayElementAtIndex(j).vector2IntValue = cells[j];
        }

        serializedTable.ApplyModifiedProperties();
        EditorUtility.SetDirty(_table);
        AssetDatabase.SaveAssets();
    }

    // ---------- 읽기 ----------

    // SerializedObject로 읽는다 - Entry가 private이라 리플렉션 없이 접근할 수 있는 유일한 경로다.
    private Dictionary<Vector2Int, List<Vector2Int>> ReadLayout()
    {
        var result = new Dictionary<Vector2Int, List<Vector2Int>>();
        var serializedTable = new SerializedObject(_table);
        SerializedProperty entriesProperty = serializedTable.FindProperty(ENTRIES_PROPERTY);

        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty element = entriesProperty.GetArrayElementAtIndex(i);
            Vector2Int chunkCoord = element.FindPropertyRelative(CHUNK_COORD_PROPERTY).vector2IntValue;
            SerializedProperty cellsProperty = element.FindPropertyRelative(CELL_COORDS_PROPERTY);

            if (!result.TryGetValue(chunkCoord, out List<Vector2Int> cells))
            {
                cells = new List<Vector2Int>();
                result[chunkCoord] = cells;
            }

            for (int j = 0; j < cellsProperty.arraySize; j++)
                cells.Add(cellsProperty.GetArrayElementAtIndex(j).vector2IntValue);
        }

        return result;
    }

    // 셀→청크 역인덱스. 중복은 먼저 나온 쪽이 이긴다(ChunkLayoutTable.BuildCellToChunkIndex와 같은 규칙).
    private static Dictionary<Vector3Int, Vector2Int> BuildCellToChunk(
        Dictionary<Vector2Int, List<Vector2Int>> layout, List<(Vector3Int Cell, Vector2Int First, Vector2Int Second)> duplicates)
    {
        var cellToChunk = new Dictionary<Vector3Int, Vector2Int>();

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> pair in layout)
        {
            foreach (Vector2Int cellCoord in pair.Value)
            {
                var coord = new Vector3Int(cellCoord.x, cellCoord.y, 0);
                if (cellToChunk.TryGetValue(coord, out Vector2Int owner))
                {
                    duplicates?.Add((coord, owner, pair.Key));
                    continue;
                }

                cellToChunk[coord] = pair.Key;
            }
        }

        return cellToChunk;
    }

    // ---------- 검증 ----------

    private void Validate()
    {
        Dictionary<Vector2Int, List<Vector2Int>> layout = ReadLayout();
        var duplicates = new List<(Vector3Int Cell, Vector2Int First, Vector2Int Second)>();
        Dictionary<Vector3Int, Vector2Int> cellToChunk = BuildCellToChunk(layout, duplicates);
        List<Vector3Int> tilemapCells = CollectTilemapCells();
        HashSet<Vector3Int> waterCells = CollectWaterCells();

        var builder = new StringBuilder();
        builder.AppendLine($"청크 {layout.Count}개 / 지정된 셀 {cellToChunk.Count}개 / 타일맵 셀 {tilemapCells.Count}개");
        builder.AppendLine($"그중 물 타일 {waterCells.Count}개는 청크 대상이 아니라 검사에서 제외합니다.");
        builder.AppendLine();

        // ① 어느 청크에도 속하지 않은 셀 - 런타임 폴백이 흡수하지만 의도한 것인지 확인해야 한다.
        // 물은 애초에 굽기에서 빠지므로 미지정이 정상이다.
        List<Vector3Int> unmapped = tilemapCells
            .Where(coord => !cellToChunk.ContainsKey(coord) && !waterCells.Contains(coord)).ToList();
        AppendCheck(builder, "① 미지정 셀 (물 제외)", unmapped.Count,
            unmapped.Take(20).Select(coord => $"({coord.x}, {coord.y})"));

        // ①-2 물인데 청크에 들어가 있는 셀 - 물 제외 규칙 이전에 구운 레이아웃의 잔재다.
        List<Vector3Int> assignedWater = waterCells.Where(cellToChunk.ContainsKey).ToList();
        AppendCheck(builder, "①-2 청크에 지정된 물 타일 (다시 구우면 사라짐)", assignedWater.Count,
            assignedWater.Take(20).Select(coord => $"({coord.x}, {coord.y})"));

        // ② 중복 지정
        AppendCheck(builder, "② 중복 지정 셀", duplicates.Count,
            duplicates.Take(20).Select(d => $"({d.Cell.x}, {d.Cell.y}): {d.First} vs {d.Second}"));

        // ③ 레이아웃에는 있으나 타일맵에 타일이 없는 셀 - 유령 지정이라 아무 효과가 없다.
        var tilemapCellSet = new HashSet<Vector3Int>(tilemapCells);
        List<Vector3Int> phantom = cellToChunk.Keys.Where(coord => !tilemapCellSet.Contains(coord)).ToList();
        AppendCheck(builder, "③ 타일 없는 좌표 지정", phantom.Count,
            phantom.Take(20).Select(coord => $"({coord.x}, {coord.y})"));

        // ④ 4방향으로 연결이 끊긴 분리 덩어리 청크 - 테두리가 두 개로 그려지고 원정 UI가 어색해진다.
        List<string> disconnected = layout
            .Where(pair => CountConnectedComponents(pair.Value) > 1)
            .Select(pair => $"{pair.Key} (덩어리 {CountConnectedComponents(pair.Value)}개)")
            .ToList();
        AppendCheck(builder, "④ 분리된 덩어리 청크", disconnected.Count, disconnected.Take(20));

        // ⑤ 빈 청크
        List<string> empty = layout.Where(pair => pair.Value.Count == 0)
            .Select(pair => pair.Key.ToString()).ToList();
        AppendCheck(builder, "⑤ 빈 청크", empty.Count, empty.Take(20));

        // ⑥ 밸런싱 테이블에만 있고 레이아웃에 없는 청크 좌표 - 조회 미스로 조용히 기본값이 적용된다.
        AppendBalancingTableCheck(builder, layout.Keys.ToHashSet());

        _report = builder.ToString();
        Debug.Log(_report);
    }

    private static void AppendCheck(StringBuilder builder, string label, int count, IEnumerable<string> samples)
    {
        builder.AppendLine(count == 0 ? $"{label}: 없음" : $"{label}: {count}개");

        if (count == 0)
            return;

        foreach (string sample in samples)
            builder.AppendLine($"    {sample}");

        builder.AppendLine();
    }

    // 청크 안 셀들이 4방향으로 몇 덩어리인지 센다.
    private static int CountConnectedComponents(List<Vector2Int> cells)
    {
        var remaining = new HashSet<Vector3Int>(cells.Select(c => new Vector3Int(c.x, c.y, 0)));
        int componentCount = 0;
        var frontier = new Stack<Vector3Int>();

        while (remaining.Count > 0)
        {
            componentCount++;
            Vector3Int seed = remaining.First();
            remaining.Remove(seed);
            frontier.Push(seed);

            while (frontier.Count > 0)
            {
                Vector3Int current = frontier.Pop();
                foreach (Vector3Int direction in CellDirections.ORTHOGONAL)
                {
                    Vector3Int neighbor = current + direction;
                    if (remaining.Remove(neighbor))
                        frontier.Push(neighbor);
                }
            }
        }

        return componentCount;
    }

    private void AppendBalancingTableCheck(StringBuilder builder, HashSet<Vector2Int> layoutChunkCoords)
    {
        var orphans = new List<string>();

        foreach (string typeName in new[] { nameof(ConquestChunkCostTable), nameof(ChunkYieldTable), nameof(ChunkLandmarkTable) })
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeName}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;

                var serializedAsset = new SerializedObject(asset);
                SerializedProperty entriesProperty = serializedAsset.FindProperty(ENTRIES_PROPERTY);
                if (entriesProperty == null || !entriesProperty.isArray)
                    continue;

                for (int i = 0; i < entriesProperty.arraySize; i++)
                {
                    SerializedProperty coordProperty = entriesProperty.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(CHUNK_COORD_PROPERTY);

                    if (coordProperty == null || layoutChunkCoords.Contains(coordProperty.vector2IntValue))
                        continue;

                    orphans.Add($"{asset.name}: {coordProperty.vector2IntValue}");
                }
            }
        }

        AppendCheck(builder, "⑥ 레이아웃에 없는 청크를 참조하는 밸런싱 엔트리", orphans.Count, orphans.Take(20));
    }

    // ---------- 인접 그래프 diff ----------

    private void DiffAdjacency()
    {
        Dictionary<Vector2Int, List<Vector2Int>> layout = ReadLayout();
        Dictionary<Vector3Int, Vector2Int> cellToChunk = BuildCellToChunk(layout, null);
        var chunkCoords = new HashSet<Vector2Int>(layout.Keys);

        var builder = new StringBuilder();
        builder.AppendLine($"인접 그래프 diff - 청크 {chunkCoords.Count}개");
        builder.AppendLine();

        AppendEdgeDiff(builder, "변 공유 (구 4방향)",
            BuildLegacyEdges(chunkCoords, useDiagonal: false),
            BuildDerivedEdges(cellToChunk, CellDirections.ORTHOGONAL));

        AppendEdgeDiff(builder, "변+꼭짓점 (구 8방향)",
            BuildLegacyEdges(chunkCoords, useDiagonal: true),
            BuildDerivedEdges(cellToChunk, CellDirections.ALL_EIGHT));

        _report = builder.ToString();
        Debug.Log(_report);
    }

    private static HashSet<(Vector2Int, Vector2Int)> BuildLegacyEdges(HashSet<Vector2Int> chunkCoords, bool useDiagonal)
    {
        var edges = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (Vector2Int chunkCoord in chunkCoords)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    if (!useDiagonal && dx != 0 && dy != 0)
                        continue;

                    var neighborCoord = chunkCoord + new Vector2Int(dx, dy);
                    if (chunkCoords.Contains(neighborCoord))
                        edges.Add(MakeEdge(chunkCoord, neighborCoord));
                }
            }
        }

        return edges;
    }

    private static HashSet<(Vector2Int, Vector2Int)> BuildDerivedEdges(
        Dictionary<Vector3Int, Vector2Int> cellToChunk, Vector3Int[] directions)
    {
        var edges = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (KeyValuePair<Vector3Int, Vector2Int> pair in cellToChunk)
        {
            foreach (Vector3Int direction in directions)
            {
                if (cellToChunk.TryGetValue(pair.Key + direction, out Vector2Int neighborChunk) &&
                    neighborChunk != pair.Value)
                {
                    edges.Add(MakeEdge(pair.Value, neighborChunk));
                }
            }
        }

        return edges;
    }

    // 무향 변을 정규화해 (a,b)와 (b,a)를 같은 항목으로 취급한다.
    private static (Vector2Int, Vector2Int) MakeEdge(Vector2Int a, Vector2Int b) =>
        (a.x != b.x ? a.x < b.x : a.y < b.y) ? (a, b) : (b, a);

    private static void AppendEdgeDiff(StringBuilder builder, string label,
        HashSet<(Vector2Int, Vector2Int)> legacyEdges, HashSet<(Vector2Int, Vector2Int)> derivedEdges)
    {
        var removed = legacyEdges.Except(derivedEdges).ToList();
        var added = derivedEdges.Except(legacyEdges).ToList();

        builder.AppendLine($"[{label}] 구 {legacyEdges.Count}변 → 신 {derivedEdges.Count}변");

        AppendCheck(builder, "  없어진 변 (점령 경로가 끊길 수 있음)", removed.Count,
            removed.Take(30).Select(edge => $"{edge.Item1} ↔ {edge.Item2}"));

        AppendCheck(builder, "  새로 생긴 변", added.Count,
            added.Take(30).Select(edge => $"{edge.Item1} ↔ {edge.Item2}"));

        builder.AppendLine();
    }
}
