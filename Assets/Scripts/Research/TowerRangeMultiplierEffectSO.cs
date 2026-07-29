using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Range Multiplier",
    fileName = "TowerRangeMultiplierEffect")]
public sealed class TowerRangeMultiplierEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public float BonusRatio => _bonusRatio;

    public override float GetTowerRangeMultiplierBonus(TowerData towerData)
    {
        return _bonusRatio;
    }
}
