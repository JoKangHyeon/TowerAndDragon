using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Lab Data",
    fileName = "ResearchLabData")]
public sealed class ResearchLabData : ScriptableObject
{
    [SerializeField] private string _nameLocKey;
    [Min(1)]
    [SerializeField] private int _populationCapacity = 1;
    [SerializeField] private ResourceAmount[] _buildCost;

    public string NameLocKey => _nameLocKey;
    public int PopulationCapacity => _populationCapacity;
    public IReadOnlyList<ResourceAmount> BuildCost =>
        _buildCost ?? System.Array.Empty<ResourceAmount>();
}
