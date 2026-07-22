using System;
using UnityEngine;

[Serializable]
public class EnemyEnhancementRule
{
    [SerializeField] private MonsterData _targetMonster;

    [SerializeField] private int _spawnCountBonus;
    [SerializeField] private EnemyStatModifier _maxHealth;
    [SerializeField] private EnemyStatModifier _shieldAmount;
    [SerializeField] private EnemyStatModifier _attackPower;
    [SerializeField] private EnemyStatModifier _moveSpeed;
    [SerializeField] private EnemyStatModifier _spawnInterval;

    public MonsterData TargetMonster => _targetMonster;
    public int SpawnCountBonus => _spawnCountBonus;
    public EnemyStatModifier MaxHealth => _maxHealth;
    public EnemyStatModifier ShieldAmount => _shieldAmount;
    public EnemyStatModifier AttackPower => _attackPower;
    public EnemyStatModifier MoveSpeed => _moveSpeed;
    public EnemyStatModifier SpawnInterval => _spawnInterval;
}
