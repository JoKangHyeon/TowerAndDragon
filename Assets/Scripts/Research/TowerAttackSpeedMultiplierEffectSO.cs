using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Attack Speed Multiplier",
    fileName = "TowerAttackSpeedMultiplierEffect")]
public sealed class TowerAttackSpeedMultiplierEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public float BonusRatio => _bonusRatio;

    public override float GetTowerAttackSpeedMultiplierBonus(TowerData towerData)
    {
        return _bonusRatio;
    }
}
