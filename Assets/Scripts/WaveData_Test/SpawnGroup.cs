using UnityEngine;

public class SpawnGroup : MonoBehaviour 
{
    [SerializeField] private BaseMonster _monsterPrefab;
    [SerializeField] private MonsterData _monsterData;
    private int _spawnCount;
    private float _spawnInterval;
    private float _startDelay;



    public BaseMonster MonsterPrefab => _monsterPrefab;
    public MonsterData MonsterData => _monsterData;
    public int SpawnCount => _spawnCount;
    public float SpawnInterval => _spawnInterval;
    public float StartDelay => _startDelay;
}