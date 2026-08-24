using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Population Capacity",
    fileName = "PopulationCapacityEffect")]
public sealed class PopulationCapacityEffectSO : ResearchEffectSO
{
    [SerializeField] private PopulationAssignmentType _targetAssignmentType;

    // 음수 = 요구 인구 감소, 양수 = 정원 확장.
    [SerializeField] private int _capacityDelta;

    [Tooltip("이 자원을 생산하는 시설에만 적용한다. None이면 배치 종류 전체에 적용한다 " +
        "- 기존 에셋(타워 인력 효율·생산 최적화·연구소 증축)이 그대로 동작하도록 None이 기본이다.")]
    [SerializeField] private ResourceType _targetResources;

    public PopulationAssignmentType TargetAssignmentType => _targetAssignmentType;
    public int CapacityDelta => _capacityDelta;
    public ResourceType TargetResources => _targetResources;

    public override int GetPopulationCapacityDelta(
        PopulationAssignmentType assignmentType,
        ResourceType producedResources)
    {
        if (assignmentType != _targetAssignmentType)
        {
            return 0;
        }

        return MatchesResources(producedResources) ? _capacityDelta : 0;
    }

    // 기본 자원 시설은 한 종류만 생산하지만(농장 Food / 벌목장 Wood / 채석장 Stone),
    // 슬라임 농장처럼 여러 비트를 동시에 가지는 시설이 있어 겹침으로 판정한다.
    private bool MatchesResources(ResourceType producedResources)
    {
        if (_targetResources == ResourceType.None)
        {
            return true;
        }

        return (_targetResources & producedResources) != ResourceType.None;
    }
}
