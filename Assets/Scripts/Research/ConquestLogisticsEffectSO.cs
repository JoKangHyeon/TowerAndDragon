using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Conquest Logistics",
    fileName = "ConquestLogisticsEffect")]
public sealed class ConquestLogisticsEffectSO : ResearchEffectSO
{
    [Range(0f, 1f)]
    [SerializeField] private float _costReductionRatio;
    [Min(0)]
    [SerializeField] private int _daysReduction;

    public float CostReductionRatio => _costReductionRatio;
    public int DaysReduction => _daysReduction;

    public override float GetConquestCostReductionRatio()
    {
        return _costReductionRatio;
    }

    public override int GetConquestDaysReduction()
    {
        return _daysReduction;
    }
}
