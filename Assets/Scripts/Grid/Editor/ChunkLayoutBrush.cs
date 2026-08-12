using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 씬뷰에서 청크 경계를 직접 칠하는 브러시. ChunkLayoutEditorWindow가 켜고 끈다.
// 에디터 전용이므로 문자열·숫자 리터럴 제한(CLAUDE.md 3·4항)의 예외 대상이다.
public class ChunkLayoutBrush
{
    // 청크 ID를 색으로 바꾸는 파라미터 - 황금비 걸음은 슬롯 번호를 색상환에 고르게 흩뿌린다.
    private const float GOLDEN_RATIO_CONJUGATE = 0.618034f;
    private const float CHUNK_FILL_SATURATION = 0.65f;
    private const float CHUNK_FILL_VALUE = 0.95f;
    private const float CHUNK_FILL_ALPHA = 0.34f;
    private const float ACTIVE_CHUNK_FILL_ALPHA = 0.55f;
    private const float HOVER_OUTLINE_WIDTH = 3f;

    // 황금비 걸음을 돌리기 전에 해시를 접어 넣는 주기. float 가수가 24비트라
    // 1e7을 넘는 수에 0.618을 곱하면 소수부가 통째로 사라져 모든 청크가 같은 색이 된다.
    private const int HUE_SLOT_COUNT = 1024;

    // 좌표 해시(FNV-1a) 상수와 눈사태(avalanche) 마무리 혼합 상수.
    private const uint HASH_OFFSET_BASIS = 2166136261u;
    private const uint HASH_PRIME = 16777619u;
    private const uint HASH_MIX_A = 0x7feb352du;
    private const uint HASH_MIX_B = 0x846ca68bu;

    // 색상이 우연히 비슷하게 잡힌 청크끼리도 밝기로 갈리게 하는 단계.
    private const int VALUE_STEP_COUNT = 3;
    private const float VALUE_STEP_SIZE = 0.06f;

    // 활성 청크 전용 초록. 랜덤 팔레트는 아래 대역을 비워 두므로 이 색과 겹치지 않는다.
    private const float ACTIVE_CHUNK_HUE = 0.333f;
    private const float ACTIVE_CHUNK_SATURATION = 0.85f;
    private const float GREEN_BAND_START = 0.22f;
    private const float GREEN_BAND_WIDTH = 0.23f;

    // 화면 밖 셀까지 매 리페인트마다 그리면 큰 맵에서 씬뷰가 버벅인다 - 보이는 범위만 그린다.
    private const float VISIBLE_MARGIN = 2f;

    private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> _layout = new();
    private readonly Dictionary<Vector3Int, Vector2Int> _cellToChunk = new();
    private readonly Vector3[] _cellCornerBuffer = new Vector3[4];

    private ChunkLayoutTable _table;
    private GridMap _gridMap;
    private MouseSelectController _mouseSelectController;
    private bool _isPainting;
    private bool _hasStrokeChanges;

    // 아이소 타일은 블록 그림이라 셀 좌표 그대로 그리면 블록 밑동에 깔린다 - 런타임 오버레이들과
    // 똑같이 표시용 Y 오프셋만큼 올려 그리고, 집을 때는 같은 양을 빼서 "그려진 자리" 기준으로 셀을 찾는다.
    // (규칙 출처: MouseSelectController.GetHoveredCell)
    private float DisplayYOffset => MouseSelectController.GetYOffsetOrZero(_mouseSelectController);

    public Vector2Int ActiveChunkCoord { get; set; }
    public IEnumerable<Vector2Int> ChunkCoords => _layout.Keys;
    public int ChunkCount => _layout.Count;

    public int ActiveChunkCellCount =>
        _layout.TryGetValue(ActiveChunkCoord, out HashSet<Vector2Int> cells) ? cells.Count : 0;

    public void Begin(ChunkLayoutTable table, GridMap gridMap)
    {
        _table = table;
        _gridMap = gridMap;
        _mouseSelectController = Object.FindFirstObjectByType<MouseSelectController>(FindObjectsInactive.Include);
        LoadLayout();
        EnsureActiveChunkExists();

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoEvent -= OnUndoRedo;
        Undo.undoRedoEvent += OnUndoRedo;
        SceneView.RepaintAll();
    }

    public void End()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoEvent -= OnUndoRedo;
        SceneView.RepaintAll();
    }

    // 실행 취소는 에셋만 되돌린다 - 오버레이와 다음 획이 함께 쓰는 메모리 사본을 다시 읽지 않으면
    // 씬뷰가 취소 이전 상태를 계속 그리고, 다음 획이 낡은 사본을 에셋에 덮어써 취소를 지워버린다.
    private void OnUndoRedo(in UndoRedoInfo info)
    {
        if (_table == null)
            return;

        LoadLayout();
        EnsureActiveChunkExists();
        SceneView.RepaintAll();
    }

    // 실행 취소로 활성 청크가 통째로 사라질 수 있으므로 남아 있는 청크 하나로 옮겨 둔다.
    private void EnsureActiveChunkExists()
    {
        if (_layout.ContainsKey(ActiveChunkCoord))
            return;

        foreach (Vector2Int chunkCoord in _layout.Keys)
        {
            ActiveChunkCoord = chunkCoord;
            break;
        }
    }

    // 새 청크 ID를 만든다 - 기존에 쓰지 않는 좌표 중 원점에서 가까운 것을 고른다.
    public Vector2Int CreateChunkCoord()
    {
        for (int ring = 0; ; ring++)
        {
            for (int x = -ring; x <= ring; x++)
            {
                for (int y = -ring; y <= ring; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (_layout.ContainsKey(candidate))
                        continue;

                    _layout[candidate] = new HashSet<Vector2Int>();
                    ActiveChunkCoord = candidate;
                    return candidate;
                }
            }
        }
    }

    // 셀이 하나도 없는 청크를 지운다 - 브러시로 전부 옮긴 뒤 남은 빈 껍데기 정리용.
    public int RemoveEmptyChunks()
    {
        var emptyCoords = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, HashSet<Vector2Int>> pair in _layout)
        {
            if (pair.Value.Count == 0)
                emptyCoords.Add(pair.Key);
        }

        foreach (Vector2Int chunkCoord in emptyCoords)
        {
            _layout.Remove(chunkCoord);
        }

        if (emptyCoords.Count > 0)
            WriteBack();

        return emptyCoords.Count;
    }

    private void LoadLayout()
    {
        _layout.Clear();
        _cellToChunk.Clear();

        var serialized = new SerializedObject(_table);
        SerializedProperty entries = serialized.FindProperty("_entries");

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            Vector2Int chunkCoord = element.FindPropertyRelative("ChunkCoord").vector2IntValue;
            SerializedProperty cellsProperty = element.FindPropertyRelative("CellCoords");

            if (!_layout.TryGetValue(chunkCoord, out HashSet<Vector2Int> cells))
            {
                cells = new HashSet<Vector2Int>();
                _layout[chunkCoord] = cells;
            }

            for (int j = 0; j < cellsProperty.arraySize; j++)
            {
                Vector2Int cellCoord = cellsProperty.GetArrayElementAtIndex(j).vector2IntValue;
                cells.Add(cellCoord);
                _cellToChunk[new Vector3Int(cellCoord.x, cellCoord.y, 0)] = chunkCoord;
            }
        }
    }

    // 한 획(드래그)이 끝날 때 한 번만 에셋에 쓴다 - 실행 취소도 획 단위가 되어 다루기 쉽다.
    private void WriteBack()
    {
        // RecordObject는 diff 기반이라 배열 크기 변화를 놓친다 - 이 편집은 매번 배열을 리사이즈하므로
        // 통째로 스냅샷을 뜨는 RegisterCompleteObjectUndo를 쓴다. 그룹을 올려 획끼리 뭉치지 않게 한다.
        Undo.IncrementCurrentGroup();
        Undo.RegisterCompleteObjectUndo(_table, "청크 레이아웃 편집");

        var serialized = new SerializedObject(_table);
        SerializedProperty entries = serialized.FindProperty("_entries");

        var orderedChunkCoords = new List<Vector2Int>(_layout.Keys);
        orderedChunkCoords.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        entries.ClearArray();
        entries.arraySize = orderedChunkCoords.Count;

        for (int i = 0; i < orderedChunkCoords.Count; i++)
        {
            Vector2Int chunkCoord = orderedChunkCoords[i];
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("ChunkCoord").vector2IntValue = chunkCoord;

            var cells = new List<Vector2Int>(_layout[chunkCoord]);
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            SerializedProperty cellsProperty = element.FindPropertyRelative("CellCoords");
            cellsProperty.ClearArray();
            cellsProperty.arraySize = cells.Count;

            for (int j = 0; j < cells.Count; j++)
                cellsProperty.GetArrayElementAtIndex(j).vector2IntValue = cells[j];
        }

        // 위에서 이미 스냅샷을 떴다 - ApplyModifiedProperties를 쓰면 항목이 두 번 쌓여 Ctrl+Z를 두 번 눌러야 한다.
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_table);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_table == null || _gridMap == null)
            return;

        // 기본 컨트롤을 가로채지 않으면 드래그가 오브젝트 선택 박스로 넘어간다.
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        DrawChunkOverlay(sceneView);
        HandleInput(sceneView);
    }

    private void HandleInput(SceneView sceneView)
    {
        Event current = Event.current;
        if (current.alt)
            return;

        bool isPaintButton = current.button == 0;

        switch (current.type)
        {
            // 씬뷰는 마우스가 움직이는 것만으로는 다시 그리지 않는다 - 직접 요청하지 않으면
            // 커서 아래 셀 표시가 그 자리에 멈춰 있다가 다른 이벤트(모디파이어 키 등)가 와야 따라온다.
            case EventType.MouseMove:
                sceneView.Repaint();
                break;

            case EventType.MouseDown when isPaintButton:
                _isPainting = true;
                _hasStrokeChanges = false;
                ApplyBrushAtMouse(current);
                current.Use();
                break;

            case EventType.MouseDrag when isPaintButton && _isPainting:
                ApplyBrushAtMouse(current);
                current.Use();
                break;

            case EventType.MouseUp when isPaintButton && _isPainting:
                _isPainting = false;
                if (_hasStrokeChanges)
                    WriteBack();
                current.Use();
                break;
        }
    }

    private void ApplyBrushAtMouse(Event current)
    {
        if (!TryPickCell(current.mousePosition, out Vector3Int cellCoord))
            return;

        var cellKey = new Vector2Int(cellCoord.x, cellCoord.y);

        // Ctrl = 스포이드. 칠하기 전에 대상 청크를 집는다.
        if (current.control)
        {
            if (_cellToChunk.TryGetValue(cellCoord, out Vector2Int pickedChunkCoord))
                ActiveChunkCoord = pickedChunkCoord;
            return;
        }

        // Shift = 지우개. 어느 청크에도 속하지 않게 만든다(런타임에서 근접 청크로 흡수된다).
        if (current.shift)
        {
            if (!_cellToChunk.TryGetValue(cellCoord, out Vector2Int ownerChunkCoord))
                return;

            _layout[ownerChunkCoord].Remove(cellKey);
            _cellToChunk.Remove(cellCoord);
            _hasStrokeChanges = true;
            SceneView.RepaintAll();
            return;
        }

        if (_cellToChunk.TryGetValue(cellCoord, out Vector2Int previousChunkCoord))
        {
            if (previousChunkCoord == ActiveChunkCoord)
                return;

            _layout[previousChunkCoord].Remove(cellKey);
        }

        if (!_layout.TryGetValue(ActiveChunkCoord, out HashSet<Vector2Int> activeCells))
        {
            activeCells = new HashSet<Vector2Int>();
            _layout[ActiveChunkCoord] = activeCells;
        }

        activeCells.Add(cellKey);
        _cellToChunk[cellCoord] = ActiveChunkCoord;
        _hasStrokeChanges = true;
        SceneView.RepaintAll();
    }

    // 마우스 위치를 그리드 평면(z=0)에 쏘아 셀을 고른다.
    // PickCellAtWorldPoint가 고저차를 되돌려주므로 단차가 있는 지형에서도 눈에 보이는 타일이 잡힌다.
    private bool TryPickCell(Vector2 mousePosition, out Vector3Int cellCoord)
    {
        cellCoord = default;

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var gridPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!gridPlane.Raycast(ray, out float distance))
            return false;

        // 오버레이를 DisplayYOffset만큼 올려 그렸으므로, 커서 아래 셀을 찾을 때는 같은 양을 빼야
        // 눈에 보이는 칸과 칠해지는 칸이 일치한다.
        Vector3 worldPoint = ray.GetPoint(distance);
        worldPoint.y -= DisplayYOffset;

        cellCoord = _gridMap.PickCellAtWorldPoint(worldPoint);
        return true;
    }

    private void DrawChunkOverlay(SceneView sceneView)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Camera camera = sceneView.camera;
        if (camera == null)
            return;

        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, camera.nearClipPlane));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, camera.nearClipPlane));

        float minX = Mathf.Min(bottomLeft.x, topRight.x) - VISIBLE_MARGIN;
        float maxX = Mathf.Max(bottomLeft.x, topRight.x) + VISIBLE_MARGIN;
        float minY = Mathf.Min(bottomLeft.y, topRight.y) - VISIBLE_MARGIN;
        float maxY = Mathf.Max(bottomLeft.y, topRight.y) + VISIBLE_MARGIN;

        foreach (KeyValuePair<Vector3Int, Vector2Int> entry in _cellToChunk)
        {
            Vector3 center = _gridMap.ConvertGridToWorld(entry.Key);
            if (center.x < minX || center.x > maxX || center.y < minY || center.y > maxY)
                continue;

            bool isActive = entry.Value == ActiveChunkCoord;
            Color fill = ResolveChunkColor(entry.Value, isActive);
            fill.a = isActive ? ACTIVE_CHUNK_FILL_ALPHA : CHUNK_FILL_ALPHA;

            FillCell(entry.Key, fill);
        }

        DrawHoveredCell();
    }

    private void DrawHoveredCell()
    {
        if (!TryPickCell(Event.current.mousePosition, out Vector3Int hoveredCoord))
            return;

        WriteCellCorners(hoveredCoord);
        Handles.color = Color.white;
        Handles.DrawAAPolyLine(HOVER_OUTLINE_WIDTH,
            _cellCornerBuffer[0], _cellCornerBuffer[1], _cellCornerBuffer[2], _cellCornerBuffer[3], _cellCornerBuffer[0]);
    }

    private void FillCell(Vector3Int cellCoord, Color fill)
    {
        WriteCellCorners(cellCoord);
        Handles.color = fill;
        Handles.DrawAAConvexPolygon(_cellCornerBuffer);
    }

    // 셀 (x,y)의 네 꼭짓점은 격자 꼭짓점 (x,y) / (x+1,y) / (x+1,y+1) / (x,y+1)이다.
    // 네 꼭짓점 모두에 "이 셀의" 높이만 똑같이 더해야 다이아몬드 모양이 유지된 채 타일 위에 얹힌다.
    private void WriteCellCorners(Vector3Int cellCoord)
    {
        float heightOffset = _gridMap.GetHeightOffset(cellCoord) + DisplayYOffset;
        var origin = new Vector2Int(cellCoord.x, cellCoord.y);

        _cellCornerBuffer[0] = GetFlatCellCorner(origin);
        _cellCornerBuffer[1] = GetFlatCellCorner(origin + Vector2Int.right);
        _cellCornerBuffer[2] = GetFlatCellCorner(origin + Vector2Int.one);
        _cellCornerBuffer[3] = GetFlatCellCorner(origin + Vector2Int.up);

        for (int i = 0; i < _cellCornerBuffer.Length; i++)
        {
            _cellCornerBuffer[i].y += heightOffset;
        }
    }

    // 격자 꼭짓점 (x,y)는 셀 (x-1,y-1)과 셀 (x,y) 중심의 중점이다(아이소 격자가 평행사변형 격자이므로).
    // GridMap.GetCellCornerWorld를 쓰지 않는다 - 그쪽은 ConvertGridToWorld를 거치며 이웃 두 셀의
    // 높이 오프셋을 평균해 섞어 넣는다. 꼭짓점마다 섞이는 이웃이 달라서 사각형이 기울고 타일에서
    // 떠버린다(이 맵에서 타일의 38%가 해당, 최대 1.2유닛). 여기서는 높이를 섞지 않은 지면 좌표를 쓴다.
    private Vector3 GetFlatCellCorner(Vector2Int corner)
    {
        Tilemap tilemap = _gridMap.TerrainTilemap;
        Vector3 diagonalCenter = tilemap.GetCellCenterWorld(new Vector3Int(corner.x - 1, corner.y - 1, 0));
        Vector3 cellCenter = tilemap.GetCellCenterWorld(new Vector3Int(corner.x, corner.y, 0));
        return (diagonalCenter + cellCenter) * 0.5f;
    }

    // 청크 ID를 색으로 바꾼다 - 좌표를 섞어 만든 정수를 황금비 간격으로 색상환에 흩뿌리므로
    // 이웃한 청크끼리도 색이 뚜렷이 갈리고, 같은 청크는 세션이 바뀌어도 같은 색을 유지한다.
    // 지금 칠하고 있는 청크만 고정 초록으로 빼내고, 나머지는 초록 대역을 비켜 간다.
    public static Color ResolveChunkColor(Vector2Int chunkCoord, bool isActive)
    {
        if (isActive)
            return Color.HSVToRGB(ACTIVE_CHUNK_HUE, ACTIVE_CHUNK_SATURATION, CHUNK_FILL_VALUE);

        // x·y를 각각 곱해 XOR하는 방식은 쓰지 않는다 - 두 곱의 끝자리 0 개수가 같으면
        // (x,y)와 (-x,-y)의 해시가 비트까지 똑같아져(2의 보수 성질) 맞은편 청크가 같은 색이 된다.
        // x를 먼저 흡수·교반한 뒤 y를 흡수하면 순서 의존성이 생겨 그 대칭이 깨진다.
        uint hash;
        unchecked
        {
            hash = HASH_OFFSET_BASIS;
            hash = (hash ^ (uint)chunkCoord.x) * HASH_PRIME;
            hash = (hash ^ (uint)chunkCoord.y) * HASH_PRIME;
            hash ^= hash >> 16;
            hash *= HASH_MIX_A;
            hash ^= hash >> 15;
            hash *= HASH_MIX_B;
            hash ^= hash >> 16;
        }

        int slot = (int)(hash % HUE_SLOT_COUNT);
        float hue = Mathf.Repeat(slot * GOLDEN_RATIO_CONJUGATE, 1f);

        // 색상환에서 초록 구간을 도려내고 남은 범위로 압축해 활성 청크와 절대 헷갈리지 않게 한다.
        hue *= 1f - GREEN_BAND_WIDTH;
        if (hue >= GREEN_BAND_START)
            hue += GREEN_BAND_WIDTH;

        // 색상만으로는 우연히 이웃과 비슷해질 수 있으므로 해시의 다른 자리로 밝기도 한 단계씩 어긋낸다.
        int valueStep = (int)(hash / HUE_SLOT_COUNT) % VALUE_STEP_COUNT;
        float value = CHUNK_FILL_VALUE - valueStep * VALUE_STEP_SIZE;

        return Color.HSVToRGB(hue, CHUNK_FILL_SATURATION, value);
    }
}
