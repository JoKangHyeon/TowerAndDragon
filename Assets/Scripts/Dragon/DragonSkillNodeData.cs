using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Skill Node",
    fileName = "DragonSkillNode")]
public sealed class DragonSkillNodeData : ProgressionNodeData
{
    [SerializeField] private DragonType _attribute;
    [SerializeField] private DragonNodeKind _kind;
    [SerializeField] private DragonSkillEffectSO[] _effects;

    public DragonType Attribute => _attribute;
    public DragonNodeKind Kind => _kind;

    public IReadOnlyList<DragonSkillEffectSO> Effects =>
        _effects ?? System.Array.Empty<DragonSkillEffectSO>();
}
