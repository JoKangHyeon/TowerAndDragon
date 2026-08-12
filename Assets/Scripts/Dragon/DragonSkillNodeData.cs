using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Skill Node",
    fileName = "DragonSkillNode")]
public sealed class DragonSkillNodeData : ProgressionNodeData
{
    [SerializeField] private DragonType _attribute;
    [SerializeField] private DragonNodeKind _kind;

    [Tooltip("같은 슬롯 안에서의 랭크(1부터). 랭크는 별도 노드로 체인되며, 직전 랭크가 선행이다 - " +
        "해금 집합이 문자열 id의 HashSet이라 코어(ProgressionRules·세이브)를 건드리지 않고 표현된다.")]
    [Min(1)]
    [SerializeField] private int _rank = 1;

    [Tooltip("이 슬롯의 최대 랭크. UI가 'Lv 1/2' 뱃지를 그리는 데 쓴다.")]
    [Min(1)]
    [SerializeField] private int _maxRank = 1;

    [SerializeField] private DragonSkillEffectSO[] _effects;

    public DragonType Attribute => _attribute;
    public DragonNodeKind Kind => _kind;
    public int Rank => _rank;
    public int MaxRank => _maxRank;

    public IReadOnlyList<DragonSkillEffectSO> Effects =>
        _effects ?? System.Array.Empty<DragonSkillEffectSO>();
}
