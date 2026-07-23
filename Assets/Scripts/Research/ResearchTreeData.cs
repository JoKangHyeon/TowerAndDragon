using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Tree",
    fileName = "ResearchTree")]
public sealed class ResearchTreeData : ScriptableObject
{
    [SerializeField] private ResearchNodeData[] _nodes;

    public IReadOnlyList<ResearchNodeData> Nodes =>
        _nodes ?? System.Array.Empty<ResearchNodeData>();
}
