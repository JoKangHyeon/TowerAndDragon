using System;
using UnityEngine;

//데이터 클래스
[Serializable]
public class SpawnGroup
{
    [SerializeField] private BaseMonster _monsterPrefab;
    [SerializeField] private MonsterData _monsterData;
    [SerializeField] private int _spawnCount;
    [SerializeField] private float _spawnInterval;
    [SerializeField] private float _startDelay;

    public SpawnGroup(
        BaseMonster monsterPrefab,
        MonsterData monsterData,
        int spawnCount,
        float spawnInterval,
        float startDelay)
    {
        _monsterPrefab = monsterPrefab;
        //_monsterData = monsterData;
        _spawnCount = spawnCount;
        _spawnInterval = spawnInterval;
        _startDelay = startDelay;
    }

    public BaseMonster MonsterPrefab => _monsterPrefab;
    public MonsterData MonsterData => _monsterData;
    public int SpawnCount => _spawnCount;
    public float SpawnInterval => _spawnInterval;
    public float StartDelay => _startDelay;
}
