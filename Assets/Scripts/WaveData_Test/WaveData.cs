using UnityEngine;


public class WaveData : MonoBehaviour 
{
    [SerializeField] private SpawnGroup _spawnGroup;

    public SpawnGroup SpawnGroup => _spawnGroup;
}