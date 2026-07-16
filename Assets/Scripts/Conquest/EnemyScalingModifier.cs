using System;
using UnityEngine;

/// <summary>
///  한 청크 점령 시 적의 공격력이나 스폰 수 아니면 둘다 높일지 저장하는 구조체
/// </summary>
[Serializable]
public struct EnemyScalingModifier
{
    private const int NEUTRAL_SPAWN_COUNT_BONUS = 0;
    private const float NEUTRAL_ATTACK_POWER_BONUS = 0f;

    [SerializeField]
    private int _spawnCountBonus;

    [SerializeField]
    private float _attackPowerBonus;

    public int SpawnCountBonus => _spawnCountBonus;
    public float AttackPowerBonus => _attackPowerBonus;

    // 이 청크가 스폰 수/공격력 중 실제로 강화하는 항목이 무엇인지 - UI에서 해당 항목만 표시할 때 사용
    public bool AffectsSpawnCount => _spawnCountBonus != NEUTRAL_SPAWN_COUNT_BONUS;
    public bool AffectsAttackPower => !Mathf.Approximately(_attackPowerBonus, NEUTRAL_ATTACK_POWER_BONUS);

    public static EnemyScalingModifier Neutral =>
        new EnemyScalingModifier(NEUTRAL_SPAWN_COUNT_BONUS, NEUTRAL_ATTACK_POWER_BONUS);

    public EnemyScalingModifier(int spawnCountBonus, float attackPowerBonus)
    {
        _spawnCountBonus = spawnCountBonus;
        _attackPowerBonus = attackPowerBonus;
    }

    public EnemyScalingModifier Combine(EnemyScalingModifier other) =>
        new EnemyScalingModifier(
            SpawnCountBonus + other.SpawnCountBonus,
            AttackPowerBonus + other.AttackPowerBonus
        );
}
