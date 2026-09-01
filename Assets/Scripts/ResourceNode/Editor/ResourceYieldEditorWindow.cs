using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ChunkYieldTable(청크 기본 생산량)과 CellYieldOverrideTable(셀 오버라이드)을
// 좌표로 찾아 헤매지 않고 정렬·필터링해서 편집할 수 있는 도구 창.
public class ResourceYieldEditorWindow : EditorWindow
{
    private enum Tab
    {
        ChunkYieldTable,
        CellYieldOverrideTable,
    }

    private enum ChunkSortKey
    {
        ChunkX,
        ChunkY,
        TerrainType,
    }

    private const int DEFAULT_COORD_FILTER_RANGE = 999;

    private Tab _tab;

    private ChunkYieldTable _chunkYieldTable;
    private SerializedObject _chunkYieldSerialized;
    private ChunkSortKey _chunkSortKey;
    private bool _chunkSortAscending = true;
    private int _chunkFilterMinX = -DEFAULT_COORD_FILTER_RANGE;
    private int _chunkFilterMaxX = DEFAULT_COORD_FILTER_RANGE;
    private int _chunkFilterMinY = -DEFAULT_COORD_FILTER_RANGE;
    private int _chunkFilterMaxY = DEFAULT_COORD_FILTER_RANGE;
    private Vector2 _chunkScrollPosition;

    // 청크의 지형 타입은 테이블 에셋이 아니라 씬의 GridMap이 런타임에 계산해 갖고 있는 정보라
    // 정렬/표시용으로 별도 캐시해 둔다("지형 정보 새로고침" 버튼으로 채움 - Play 모드 필요).
    private readonly Dictionary<Vector2Int, TerrainType> _terrainByChunk = new();

    private CellYieldOverrideTable _cellOverrideTable;
    private SerializedObject _cellOverrideSerialized;
    private Vector2 _overrideScrollPosition;

    [MenuItem("TowerAndDragon/자원 생산량 편집기")]
    public static void Open() => GetWindow<ResourceYieldEditorWindow>("자원 생산량 편집기");

    private void OnEnable()
    {
        if (_chunkYieldTable == null)
            _chunkYieldTable = FindFirstAsset<ChunkYieldTable>();

        if (_cellOverrideTable == null)
            _cellOverrideTable = FindFirstAsset<CellYieldOverrideTable>();
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private void OnGUI()
    {
        _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "청크 기본값", "셀 오버라이드" });
        EditorGUILayout.Space();

        if (_tab == Tab.ChunkYieldTable)
            DrawChunkYieldTab();
        else
            DrawCellOverrideTab();

        EditorGUILayout.Space();
        if (GUILayout.Button("변경사항 저장"))
            AssetDatabase.SaveAssets();
    }

    private void DrawChunkYieldTab()
    {
        EditorGUI.BeginChangeCheck();
        _chunkYieldTable = (ChunkYieldTable)EditorGUILayout.ObjectField("테이블", _chunkYieldTable, typeof(ChunkYieldTable), false);
        if (EditorGUI.EndChangeCheck())
            _chunkYieldSerialized = null;

        if (_chunkYieldTable == null)
        {
            EditorGUILayout.HelpBox("편집할 ChunkYieldTable 에셋을 선택하세요.", MessageType.Info);
            return;
        }

        _chunkYieldSerialized ??= new SerializedObject(_chunkYieldTable);
        _chunkYieldSerialized.Update();

        EditorGUILayout.LabelField("필터 - 청크 좌표 범위", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("X", GUILayout.Width(12));
            _chunkFilterMinX = EditorGUILayout.IntField(_chunkFilterMinX, GUILayout.Width(50));
            EditorGUILayout.LabelField("~", GUILayout.Width(12));
            _chunkFilterMaxX = EditorGUILayout.IntField(_chunkFilterMaxX, GUILayout.Width(50));

            EditorGUILayout.LabelField("Y", GUILayout.Width(12));
            _chunkFilterMinY = EditorGUILayout.IntField(_chunkFilterMinY, GUILayout.Width(50));
            EditorGUILayout.LabelField("~", GUILayout.Width(12));
            _chunkFilterMaxY = EditorGUILayout.IntField(_chunkFilterMaxY, GUILayout.Width(50));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("정렬", GUILayout.Width(30));
            _chunkSortKey = (ChunkSortKey)EditorGUILayout.EnumPopup(_chunkSortKey, GUILayout.Width(100));
            _chunkSortAscending = GUILayout.Toggle(_chunkSortAscending, _chunkSortAscending ? "오름차순" : "내림차순", "Button", GUILayout.Width(80));

            if (GUILayout.Button("지형 정보 새로고침", GUILayout.Width(120)))
                RefreshTerrainCache();
        }

        if (_chunkSortKey == ChunkSortKey.TerrainType && _terrainByChunk.Count == 0)
            EditorGUILayout.HelpBox("지형 타입은 씬의 GridMap이 Play 모드에서 계산한 값이라, 정렬하려면 Play 모드에서 '지형 정보 새로고침'을 먼저 눌러야 합니다.", MessageType.Info);

        EditorGUILayout.Space();

        SerializedProperty entriesProp = _chunkYieldSerialized.FindProperty("_entries");
        List<int> order = BuildChunkSortOrder(entriesProp);

        _chunkScrollPosition = EditorGUILayout.BeginScrollView(_chunkScrollPosition);
        foreach (int index in order)
            DrawChunkYieldEntry(entriesProp.GetArrayElementAtIndex(index));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("항목 추가"))
        {
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
        }

        _chunkYieldSerialized.ApplyModifiedProperties();
    }

    private void RefreshTerrainCache()
    {
        _terrainByChunk.Clear();

        GridMap gridMap = FindFirstObjectByType<GridMap>();
        if (gridMap == null)
        {
            Debug.LogWarning("[ResourceYieldEditorWindow] 씬에서 GridMap을 찾지 못했습니다.");
            return;
        }

        foreach (Chunk chunk in gridMap.GetAllChunks())
            _terrainByChunk[chunk.ChunkCoord] = chunk.DominantTerrain;

        if (_terrainByChunk.Count == 0)
            Debug.LogWarning("[ResourceYieldEditorWindow] GridMap에 청크가 없습니다 - Play 모드에서 다시 시도하세요.");
    }

    private List<int> BuildChunkSortOrder(SerializedProperty entriesProp)
    {
        var indices = new List<int>();

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            Vector2Int chunkCoord = entry.FindPropertyRelative("ChunkCoord").vector2IntValue;

            if (chunkCoord.x < _chunkFilterMinX || chunkCoord.x > _chunkFilterMaxX)
                continue;
            if (chunkCoord.y < _chunkFilterMinY || chunkCoord.y > _chunkFilterMaxY)
                continue;

            indices.Add(i);
        }

        int Key(int index)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(index);
            Vector2Int chunkCoord = entry.FindPropertyRelative("ChunkCoord").vector2IntValue;

            return _chunkSortKey switch
            {
                ChunkSortKey.ChunkX => chunkCoord.x,
                ChunkSortKey.ChunkY => chunkCoord.y,
                ChunkSortKey.TerrainType => _terrainByChunk.TryGetValue(chunkCoord, out TerrainType terrain) ? (int)terrain : -1,
                _ => 0,
            };
        }

        indices.Sort((a, b) => Key(a).CompareTo(Key(b)));
        if (!_chunkSortAscending)
            indices.Reverse();

        return indices;
    }

    private void DrawChunkYieldEntry(SerializedProperty entry)
    {
        SerializedProperty chunkCoordProp = entry.FindPropertyRelative("ChunkCoord");
        SerializedProperty multipliersProp = entry.FindPropertyRelative("ResourceMultipliers");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(chunkCoordProp, new GUIContent("청크 좌표"), GUILayout.Width(200));

                Vector2Int chunkCoord = chunkCoordProp.vector2IntValue;
                string terrainLabel = _terrainByChunk.TryGetValue(chunkCoord, out TerrainType terrain) ? terrain.ToString() : "-";
                EditorGUILayout.LabelField($"지형: {terrainLabel}", GUILayout.Width(120));
            }

            EditorGUILayout.PropertyField(multipliersProp, new GUIContent("자원별 배율"), true);
        }
    }

    private void DrawCellOverrideTab()
    {
        EditorGUI.BeginChangeCheck();
        _cellOverrideTable = (CellYieldOverrideTable)EditorGUILayout.ObjectField("테이블", _cellOverrideTable, typeof(CellYieldOverrideTable), false);
        if (EditorGUI.EndChangeCheck())
            _cellOverrideSerialized = null;

        if (_cellOverrideTable == null)
        {
            EditorGUILayout.HelpBox("편집할 CellYieldOverrideTable 에셋을 선택하세요.", MessageType.Info);
            return;
        }

        _cellOverrideSerialized ??= new SerializedObject(_cellOverrideTable);
        _cellOverrideSerialized.Update();

        SerializedProperty entriesProp = _cellOverrideSerialized.FindProperty("_entries");

        _overrideScrollPosition = EditorGUILayout.BeginScrollView(_overrideScrollPosition);
        for (int i = 0; i < entriesProp.arraySize; i++)
            DrawCellOverrideEntry(entriesProp, i);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("항목 추가"))
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);

        _cellOverrideSerialized.ApplyModifiedProperties();
    }

    private void DrawCellOverrideEntry(SerializedProperty entriesProp, int index)
    {
        SerializedProperty entry = entriesProp.GetArrayElementAtIndex(index);
        SerializedProperty anchorProp = entry.FindPropertyRelative("AnchorCoord");
        SerializedProperty shapeProp = entry.FindPropertyRelative("Shape");
        SerializedProperty overridesProp = entry.FindPropertyRelative("Overrides");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(anchorProp, new GUIContent("앵커 좌표"));
                if (GUILayout.Button("삭제", GUILayout.Width(50)))
                {
                    entriesProp.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            EditorGUILayout.PropertyField(shapeProp, new GUIContent("영역 모양"), true);
            EditorGUILayout.PropertyField(overridesProp, new GUIContent("자원별 생산량"), true);
        }
    }
}
