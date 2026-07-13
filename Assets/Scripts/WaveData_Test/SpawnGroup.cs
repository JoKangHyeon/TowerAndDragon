using UnityEngine;

public class SpawnGroup : MonoBehaviour 
{
    [SerializeField] private BaseMonster _monsterPrefab;
    [SerializeField] private MonsterData _monsterData;
    private int _spawnCount;
    private float _spawnInterval;
    private float _startDelay;
}