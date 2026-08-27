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
    [Tooltip("노드 카드 슬롯 가운데에 뜨는 아이콘. 비워 두면 아이콘 자리를 통째로 끈다. " +
        "트리 생성기가 건드리지 않으므로 인스펙터에서 지정한 값이 재생성 후에도 남는다.")]
    [SerializeField] private Sprite _icon;

    [SerializeField] private ResearchBranch _branch;
    [Min(1)]
    [SerializeField] private int _tier = 1;

    [Tooltip("같은 이름으로 단계가 올라가는 연구의 몇 단계인지(I=1, II=2, III=3). " +
        "단계가 없는 연구는 0. 노드 카드의 별 개수에 쓴다. " +
        "주기 해금을 정하는 Tier와는 다른 값이다.")]
    [Min(0)]
    [SerializeField] private int _rank;
    [SerializeField] private ResearchNodeData[] _prerequisites;
    [Min(0)]
    [SerializeField] private int _researchPointCost;
    [SerializeField] private ResourceAmount[] _resourceCost;
    [SerializeField] private ResearchEffectSO[] _effects;

    [Tooltip("역설계 연구 - 이 랜드마크를 점령해야 연구할 수 있다. 비워두면 조건 없음.")]
    [SerializeField] private LandmarkDataSO _requiredLandmark;

    public string NodeId => _nodeId;
    public string NameLocKey => _nameLocKey;
    public string DescriptionLocKey => _descriptionLocKey;

    /// <summary>노드 카드 아이콘. 지정하지 않은 노드는 null이다.</summary>
    public Sprite Icon => _icon;
    public ResearchBranch Branch => _branch;
    public int Tier => _tier;

    /// <summary>단계(I=1, II=2, III=3). 단계가 없는 연구는 0이다.</summary>
    public int Rank => _rank;
    public IReadOnlyList<ResearchNodeData> Prerequisites =>
        _prerequisites ?? System.Array.Empty<ResearchNodeData>();
    public int ResearchPointCost => _researchPointCost;
    public IReadOnlyList<ResourceAmount> ResourceCost =>
        _resourceCost ?? System.Array.Empty<ResourceAmount>();
    public IReadOnlyList<ResearchEffectSO> Effects =>
        _effects ?? System.Array.Empty<ResearchEffectSO>();

    public LandmarkDataSO RequiredLandmark => _requiredLandmark;
}
