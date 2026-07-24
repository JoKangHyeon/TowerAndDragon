using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Damage Multiplier",
    fileName = "TowerDamageMultiplierEffect")]
public sealed class TowerDamageMultiplierEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public float BonusRatio => _bonusRatio;

    public override float GetTowerDamageMultiplierBonus(TowerData towerData)
    {
        return _bonusRatio;
    }
}
