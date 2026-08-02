using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Tower Aura Data",
    fileName = "TowerAuraData"
)]
public sealed class TowerAuraDataSO : ScriptableObject
{
    [Header("Range")]
    [Min(0f)]
    [SerializeField] private float _radius;

    [Header("Offense")]
    [Min(1f)]
    [SerializeField] private float _damageMultiplier = 1f;

    [Min(1f)]
    [SerializeField] private float _attackSpeedMultiplier = 1f;

    [Header("Recovery")]
    [Min(1f)]
    [SerializeField] private float _reviveSpeedMultiplier = 1f;

    [Header("Defense")]
    [Min(1f)]
    [SerializeField] private float _maxHealthMultiplier = 1f;

    [Min(0f)]
    [SerializeField] private float _shieldAmount;

    public float Radius => _radius;
    public float DamageMultiplier => _damageMultiplier;
    public float AttackSpeedMultiplier => _attackSpeedMultiplier;
    public float ReviveSpeedMultiplier => _reviveSpeedMultiplier;
    public float MaxHealthMultiplier => _maxHealthMultiplier;
    public float ShieldAmount => _shieldAmount;


    public bool HasArea => _radius > 0f;
}