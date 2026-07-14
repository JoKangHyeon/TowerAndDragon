using System;
using UnityEngine;

/// <summary>
/// 한 종류의 적을 생성하기 위한 프리팹, 데이터, 수량과 시간 간격을 정의한다.
/// </summary>
[Serializable]
public class SpawnGroupData
{
    [Tooltip("생성할 적 프리팹입니다.")]
    [SerializeField] private BaseMonster _monsterPrefab;

    [Tooltip("생성한 적에 주입할 능력치 데이터입니다.")]
    [SerializeField] private MonsterData _monsterData;

    [Tooltip("이 그룹에서 생성할 적의 수입니다.")]
    [SerializeField] private int _spawnCount;

    [Tooltip("같은 그룹에 속한 적 사이의 생성 간격(초)입니다.")]
    [SerializeField] private float _spawnInterval;

    [Tooltip("이 그룹의 생성을 시작하기 전까지 기다릴 시간(초)입니다.")]
    [SerializeField] private float _delayBeforeGroup;

    public BaseMonster MonsterPrefab => _monsterPrefab;
    public MonsterData MonsterData => _monsterData;
    public int SpawnCount => _spawnCount;
    public float SpawnInterval => _spawnInterval;
    public float DelayBeforeGroup => _delayBeforeGroup;
}
