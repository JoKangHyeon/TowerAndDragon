using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Skill Tree",
    fileName = "DragonSkillTree")]
public sealed class DragonSkillTreeData : ProgressionTreeData
{
    [SerializeField] private DragonSkillNodeData[] _nodes;

    // DragonSkillNodeData[] -> IReadOnlyList<ProgressionNodeData>는 배열 공변으로 그대로 변환된다.
    public override IReadOnlyList<ProgressionNodeData> Nodes =>
        _nodes ?? System.Array.Empty<DragonSkillNodeData>();

    public IReadOnlyList<DragonSkillNodeData> DragonNodes =>
        _nodes ?? System.Array.Empty<DragonSkillNodeData>();
}
