using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Population Capacity",
    fileName = "PopulationCapacityEffect")]
public sealed class PopulationCapacityEffectSO : ResearchEffectSO
{
    [SerializeField] private PopulationAssignmentType _targetAssignmentType;

    // 음수 = 요구 인구 감소, 양수 = 정원 확장.
    [SerializeField] private int _capacityDelta;

    public PopulationAssignmentType TargetAssignmentType => _targetAssignmentType;
    public int CapacityDelta => _capacityDelta;

    public override int GetPopulationCapacityDelta(PopulationAssignmentType assignmentType)
    {
        return assignmentType == _targetAssignmentType ? _capacityDelta : 0;
    }
}
