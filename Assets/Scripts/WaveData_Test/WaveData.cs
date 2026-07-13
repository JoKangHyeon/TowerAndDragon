using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/Wave Data", fileName = "WaveData")]
public class WaveData : ScriptableObject 
{
    [SerializeField] private SpawnGroup[] _spawnGroups;

    public IReadOnlyList<SpawnGroup> SpawnGroups => _spawnGroups;

    public void Initialize(SpawnGroup[] spawnGroups)
    {
        _spawnGroups = spawnGroups;
    }
}
