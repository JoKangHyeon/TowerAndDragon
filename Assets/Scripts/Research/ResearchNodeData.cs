using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Node",
    fileName = "ResearchNode")]
public sealed class ResearchNodeData : ScriptableObject
{
    [SerializeField] private string _nodeId;
    [SerializeField] private string _nameLocKey;
    [SerializeField] private string _descriptionLocKey;
    [SerializeField] private ResearchBranch _branch;
    [Min(1)]
    [SerializeField] private int _tier = 1;
    [SerializeField] private ResearchNodeData[] _prerequisites;
    [Min(0)]
    [SerializeField] private int _researchPointCost;
    [SerializeField] private ResourceAmount[] _resourceCost;
    [SerializeField] private ResearchEffectSO[] _effects;

    public string NodeId => _nodeId;
    public string NameLocKey => _nameLocKey;
    public string DescriptionLocKey => _descriptionLocKey;
    public ResearchBranch Branch => _branch;
    public int Tier => _tier;
    public IReadOnlyList<ResearchNodeData> Prerequisites =>
        _prerequisites ?? System.Array.Empty<ResearchNodeData>();
    public int ResearchPointCost => _researchPointCost;
    public IReadOnlyList<ResourceAmount> ResourceCost =>
        _resourceCost ?? System.Array.Empty<ResourceAmount>();
    public IReadOnlyList<ResearchEffectSO> Effects =>
        _effects ?? System.Array.Empty<ResearchEffectSO>();
}
