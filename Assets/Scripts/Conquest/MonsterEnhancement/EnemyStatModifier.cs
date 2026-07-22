using UnityEngine;
using System;

[Serializable]
public struct EnemyStatModifier
{
    private const float NEUTRAL_BONUS = 0f;

    [SerializeField] private float _additiveBonus;
    [SerializeField] private float _multiplierBonus;

    public float AdditiveBonus => _additiveBonus;
    public float MultiplierBonus => _multiplierBonus;
    public float Multiplier => 1f + _multiplierBonus;
    public bool HasAdditiveBonus => !Mathf.Approximately(_additiveBonus, NEUTRAL_BONUS);
    public bool HasMultiplierBonus => !Mathf.Approximately(_multiplierBonus, NEUTRAL_BONUS);
}
