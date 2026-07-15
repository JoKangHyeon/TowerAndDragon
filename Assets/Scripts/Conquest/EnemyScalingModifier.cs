using System;
using UnityEngine;

/// <summary>
///  한 청크 점령 시 적의 공격력이나 스폰 수 아니면 둘다 높일지 저장하는 구조체
/// </summary>
[Serializable]
public struct EnemyScalingModifier
{
    private const float NEUTRAL_MULTIPLIER = 1f;

    [SerializeField]
    private float _spawnCountMultiplier;

    [SerializeField]
    private float _attackPowerMultiplier;

    public float SpawnCountMultiplier => _spawnCountMultiplier;
    public float AttackPowermultiplier => _attackPowerMultiplier;

    // 이 청크가 스폰 수/공격력 중 실제로 강화하는 항목이 무엇인지 - UI에서 해당 항목만 표시할 때 사용
    public bool AffectsSpawnCount => !Mathf.Approximately(_spawnCountMultiplier, NEUTRAL_MULTIPLIER);
    public bool AffectsAttackPower => !Mathf.Approximately(_attackPowerMultiplier, NEUTRAL_MULTIPLIER);

    public static EnemyScalingModifier Neutral =>
        new EnemyScalingModifier(NEUTRAL_MULTIPLIER, NEUTRAL_MULTIPLIER);

    public EnemyScalingModifier(float spawnCountMultiplier, float attackPowerMultiplier)
    {
        _spawnCountMultiplier = spawnCountMultiplier;
        _attackPowerMultiplier = attackPowerMultiplier;
    }

    public EnemyScalingModifier Combine(EnemyScalingModifier other) =>
        new EnemyScalingModifier(
            SpawnCountMultiplier * other.SpawnCountMultiplier,
            AttackPowermultiplier * other.AttackPowermultiplier
        );
}
