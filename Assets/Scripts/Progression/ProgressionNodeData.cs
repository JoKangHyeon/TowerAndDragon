using System.Collections.Generic;
using UnityEngine;

// 진행도 트리 노드 1개의 공용 필드. 연구·용 스킬트리가 공유하며, 파생 클래스가
// 자기 시스템 전용 필드(용: 속성·종류·효과 / 연구: 갈래·티어·RP비용·효과)를 추가한다.
// ResearchNodeData와 동일한 null-가드 게터 관례(?? Array.Empty<T>())를 따른다.
public abstract class ProgressionNodeData : ScriptableObject
{
    [SerializeField] private string _nodeId;
    [SerializeField] private string _nameLocKey;
    [SerializeField] private string _descriptionLocKey;
    [SerializeField] private ProgressionNodeData[] _prerequisites;
    [SerializeField] private ResourceAmount[] _resourceCost;
    [SerializeField] private ProgressionGateSO[] _gates;

    public string NodeId => _nodeId;
    public string NameLocKey => _nameLocKey;
    public string DescriptionLocKey => _descriptionLocKey;

    public IReadOnlyList<ProgressionNodeData> Prerequisites =>
        _prerequisites ?? System.Array.Empty<ProgressionNodeData>();

    public IReadOnlyList<ResourceAmount> ResourceCost =>
        _resourceCost ?? System.Array.Empty<ResourceAmount>();

    public IReadOnlyList<ProgressionGateSO> Gates =>
        _gates ?? System.Array.Empty<ProgressionGateSO>();
}
