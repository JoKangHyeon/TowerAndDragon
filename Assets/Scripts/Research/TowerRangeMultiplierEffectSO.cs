using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Range Multiplier",
    fileName = "TowerRangeMultiplierEffect")]
public sealed class TowerRangeMultiplierEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    [Tooltip("대상 타워. 비워 두면(All) 전 타워에 걸린다.")]
    [SerializeField] private TowerTargetFilter _target;

    public float BonusRatio => _bonusRatio;

    public override float GetTowerRangeMultiplierBonus(TowerData towerData)
    {
        return _target.Matches(towerData) ? _bonusRatio : 0f;
    }
}
