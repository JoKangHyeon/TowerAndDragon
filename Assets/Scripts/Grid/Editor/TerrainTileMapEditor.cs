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

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Imported 폴더에서 자동 채우기"))
            AutoFillFromImportedFolder();
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
