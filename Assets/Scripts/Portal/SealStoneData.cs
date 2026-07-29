using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Portal/Seal Stone Data",
    fileName = "SealStoneData")]
public sealed class SealStoneData : ScriptableObject
{
    [SerializeField] private string _nameLocKey;

    [SerializeField] private ResourceAmount[] _buildCost;

    public string NameLocKey => _nameLocKey;
    public IReadOnlyList<ResourceAmount> BuildCost =>
        _buildCost ?? System.Array.Empty<ResourceAmount>();
}
