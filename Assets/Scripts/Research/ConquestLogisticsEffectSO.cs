using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Conquest Logistics",
    fileName = "ConquestLogisticsEffect")]
public sealed class ConquestLogisticsEffectSO : ResearchEffectSO
{
    [Range(0f, 1f)]
    [SerializeField] private float _costReductionRatio;
    [Min(0)]
    [SerializeField] private int _populationReduction;
    [Min(0)]
    [SerializeField] private int _daysReduction;

    public float CostReductionRatio => _costReductionRatio;
    public int PopulationReduction => _populationReduction;
    public int DaysReduction => _daysReduction;

    public override float GetConquestCostReductionRatio()
    {
        return _costReductionRatio;
    }

    public override int GetConquestPopulationReduction()
    {
        return _populationReduction;
    }

    public override int GetConquestDaysReduction()
    {
        return _daysReduction;
    }
}
