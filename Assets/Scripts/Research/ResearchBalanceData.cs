using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Balance",
    fileName = "ResearchBalance")]
public sealed class ResearchBalanceData : ScriptableObject
{
    [Min(1)]
    [SerializeField] private int _researchPointsPerPopulation = 1;

    public int ResearchPointsPerPopulation => _researchPointsPerPopulation;
}
