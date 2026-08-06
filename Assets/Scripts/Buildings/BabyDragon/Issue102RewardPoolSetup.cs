#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class Issue102RewardPoolSetup
{
    private const string ASSET_PATH = "Assets/Data/BabyDragon/DragonEggRewardPool.asset";
    private const string ENTRIES_PROPERTY = "_entries";
    private const string DRAGON_TYPE_PROPERTY = "DragonType";
    private const string WEIGHT_PROPERTY = "Weight";
    private const int DEFAULT_WEIGHT = 1;

    public static void Execute()
    {
        DragonEggRewardPoolSO pool = AssetDatabase.LoadAssetAtPath<DragonEggRewardPoolSO>(ASSET_PATH);

        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<DragonEggRewardPoolSO>();
            AssetDatabase.CreateAsset(pool, ASSET_PATH);
        }

        SerializedObject serializedPool = new SerializedObject(pool);
        SerializedProperty entries = serializedPool.FindProperty(ENTRIES_PROPERTY);
        Array dragonTypes = Enum.GetValues(typeof(DragonType));
        entries.arraySize = dragonTypes.Length;

        for (int index = 0; index < dragonTypes.Length; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(DRAGON_TYPE_PROPERTY).enumValueIndex = index;
            entry.FindPropertyRelative(WEIGHT_PROPERTY).intValue = DEFAULT_WEIGHT;
        }

        serializedPool.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
