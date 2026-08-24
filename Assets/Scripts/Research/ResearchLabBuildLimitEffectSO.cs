using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Research Lab Build Limit",
    fileName = "ResearchLabBuildLimitEffect")]
public sealed class ResearchLabBuildLimitEffectSO : ResearchEffectSO
{
    [SerializeField] private int _buildLimitBonus;

    public int BuildLimitBonus => _buildLimitBonus;

    public override int GetResearchLabBuildLimitBonus() => _buildLimitBonus;
}
