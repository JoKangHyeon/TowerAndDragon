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

    [SerializeField] private bool _scaleRadiusWithStaffing;

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

    [Header("Utility")]
    [SerializeField] private bool _isStealth;
    [SerializeField] private int _soulPopulation;

    public float Radius => _radius;
    public bool ScaleRadiusWithStaffing => _scaleRadiusWithStaffing;
    public float DamageMultiplier => _damageMultiplier;
    public float AttackSpeedMultiplier => _attackSpeedMultiplier;
    public float ReviveSpeedMultiplier => _reviveSpeedMultiplier;
    public float MaxHealthMultiplier => _maxHealthMultiplier;
    public float ShieldAmount => _shieldAmount;
    public bool IsStealth => _isStealth;
    public int SoulPopulation => _soulPopulation;

    public bool HasArea => _radius > 0f;
}
