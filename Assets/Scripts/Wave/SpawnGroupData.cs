using UnityEngine;

public class SpawnGroupData : MonoBehaviour
{
    [SerializeField] private BaseMonster _monsterPrefab;
    [SerializeField] private MonsterData _monsterData;
    [SerializeField] private int _spawnCount;
    [SerializeField] private float _spawnInterval;
    [SerializeField] private float _delayAfterGroup;

    public BaseMonster MonsterPrefab => _monsterPrefab;
    public MonsterData MonsterData => _monsterData;
    public int SpawnCount => _spawnCount;
    public float SpawnInterval => _spawnInterval;
    public float DelayAfterGroup => _delayAfterGroup; 

}
