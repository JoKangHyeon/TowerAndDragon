/// <summary>
/// 타워, 생산 시설 또는 점령 원정 한 곳의 인구 수용량과 현재 배치 상태를 보관한다.
/// 값 변경은 전체 인구를 검증하는 PopulationManager를 통해서만 수행한다.
/// </summary>
public sealed class PopulationAllocation
{
    public PopulationAssignmentType AssignmentType {get;}
    public int Capacity {get;}
    public int AssignedPopulation {get; private set;}

    public int AvailableCapacity => 
        Capacity - AssignedPopulation;
    public float StaffingRatio => 
        Capacity == 0 ? 0f : (float)AssignedPopulation / Capacity; 

    internal PopulationAllocation(
        PopulationAssignmentType assignmentType,
        int capacity)

    {
        AssignmentType = assignmentType;
        Capacity = capacity;
    }

    internal void Assign (int amount)
    {
        AssignedPopulation += amount;
    }

    internal void Unassign(int amount)
    {
        AssignedPopulation -= amount;
    }

}
