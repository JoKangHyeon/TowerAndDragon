using UnityEngine;
using UnityEditor;
using System.Reflection;

public class FixWavePrefabs
{
    public static void Execute()
    {
        string folderPath = "Assets/Data/WaveData/BalancingWave_AGY";
        string[] guids = AssetDatabase.FindAssets("t:WaveDefinitionSO", new[] { folderPath });
        
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WaveDefinitionSO wave = AssetDatabase.LoadAssetAtPath<WaveDefinitionSO>(path);
            if (wave == null) continue;
            
            bool changed = false;
            
            FieldInfo portalWavesField = typeof(WaveDefinitionSO).GetField("_portalWaves", BindingFlags.NonPublic | BindingFlags.Instance);
            var portalWaves = portalWavesField?.GetValue(wave) as System.Collections.IList;
            
            if (portalWaves != null)
            {
                foreach (var portalWave in portalWaves)
                {
                    var portalWaveType = portalWave.GetType();
                    var routeWavesField = portalWaveType.GetField("_routeWaves", BindingFlags.NonPublic | BindingFlags.Instance);
                    var routeWaves = routeWavesField?.GetValue(portalWave) as System.Collections.IList;
                    
                    if (routeWaves != null)
                    {
                        foreach (var routeWave in routeWaves)
                        {
                            var routeWaveType = routeWave.GetType();
                            var spawnGroupsField = routeWaveType.GetField("_spawnGroups", BindingFlags.NonPublic | BindingFlags.Instance);
                            var spawnGroups = spawnGroupsField?.GetValue(routeWave) as System.Collections.IList;
                            
                            if (spawnGroups != null)
                            {
                                foreach (var group in spawnGroups)
                                {
                                    var groupType = group.GetType();
                                    var dataField = groupType.GetField("_monsterData", BindingFlags.NonPublic | BindingFlags.Instance);
                                    var prefabField = groupType.GetField("_monsterPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                                    
                                    var data = dataField?.GetValue(group) as ScriptableObject;
                                    if (data != null)
                                    {
                                        string dataName = data.name;
                                        string prefabName = dataName.Replace("MD_", "MP_");
                                        string prefabPath = $"Assets/Data/MonsterData/MonsterPrefab/{prefabName}.prefab";
                                        
                                        GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                                        if (prefabObj != null)
                                        {
                                            Component baseMonster = prefabObj.GetComponent("BaseMonster");
                                            if (baseMonster != null)
                                            {
                                                var currentPrefab = prefabField.GetValue(group) as Component;
                                                if (currentPrefab != baseMonster)
                                                {
                                                    prefabField.SetValue(group, baseMonster);
                                                    changed = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            if (changed)
            {
                EditorUtility.SetDirty(wave);
                fixedCount++;
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {fixedCount} WaveDefinitionSO files.");
    }
}
