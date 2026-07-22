using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(TerrainTileMap))]
public class TerrainTileMapEditor : Editor
{
    private const string IMPORTED_FOLDER_PATH = "Assets/Imported";

    private static readonly Dictionary<string, TerrainType> FOLDER_TERRAIN_MAP = new()
    {
        { "Grass", TerrainType.Grass },
        { "Stone", TerrainType.Rock },
        { "Lava", TerrainType.Volcano },
        { "Sand", TerrainType.Desert },
        { "Dirt", TerrainType.Default },
        { "Snow", TerrainType.Snow },
    };

    // 지형별 기본(정적) 자원 매핑 확정본 - 초원은 기본 자원 3종 전체, 외곽 4바이옴은 각자 특화 자원 1종만 정적 해금.
    // 슬라임은 지형마다 고유 하위 타입이 있어 전부 추가로 해금된다(초원 포함).
    private static readonly Dictionary<TerrainType, ResourceType> TERRAIN_RESOURCE_MAP = new()
    {
        { TerrainType.Grass, ResourceType.Food | ResourceType.Wood | ResourceType.Stone | ResourceType.GrassSlime },
        { TerrainType.Rock, ResourceType.PhilosopherStone | ResourceType.RockSlime },
        { TerrainType.Volcano, ResourceType.FlameHeart | ResourceType.VolcanoSlime },
        { TerrainType.Desert, ResourceType.TimeSand | ResourceType.DesertSlime },
        { TerrainType.Snow, ResourceType.SnowCrystal | ResourceType.SnowSlime },
        { TerrainType.Default, ResourceType.None },
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Imported 폴더에서 자동 채우기"))
            AutoFillFromImportedFolder();

        if (GUILayout.Button("Terrain Type 기준으로 Default Resource Nodes 일괄 적용"))
            ApplyDefaultResourceNodesByTerrainType();
    }

    // 각 엔트리의 기존 TerrainType 값을 읽어, 확정된 지형-자원 매핑대로 DefaultResourceNodes를 일괄 채운다.
    private void ApplyDefaultResourceNodesByTerrainType()
    {
        serializedObject.Update();

        SerializedProperty entriesProperty = serializedObject.FindProperty("_entries");
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty element = entriesProperty.GetArrayElementAtIndex(i);
            var terrainType = (TerrainType)element.FindPropertyRelative("TerrainType").intValue;

            if (TERRAIN_RESOURCE_MAP.TryGetValue(terrainType, out ResourceType resourceType))
                element.FindPropertyRelative("DefaultResourceNodes").intValue = (int)resourceType;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void AutoFillFromImportedFolder()
    {
        serializedObject.Update();

        SerializedProperty entriesProperty = serializedObject.FindProperty("_entries");
        var existingTiles = new HashSet<Object>();
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            Object existingTile = entriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("Tile").objectReferenceValue;
            if (existingTile != null)
                existingTiles.Add(existingTile);
        }

        string[] guids = AssetDatabase.FindAssets("t:TileBase", new[] { IMPORTED_FOLDER_PATH });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
            if (tile == null || existingTiles.Contains(tile))
                continue;

            if (!TryResolveTerrainType(assetPath, out TerrainType terrainType))
                continue;

            int index = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty element = entriesProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Tile").objectReferenceValue = tile;
            element.FindPropertyRelative("TerrainType").enumValueIndex = (int)terrainType;
            existingTiles.Add(tile);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool TryResolveTerrainType(string assetPath, out TerrainType terrainType)
    {
        foreach (string segment in assetPath.Split('/'))
        {
            if (FOLDER_TERRAIN_MAP.TryGetValue(segment, out terrainType))
                return true;
        }

        terrainType = default;
        return false;
    }
}
