using UnityEngine;

/// <summary>
/// 타워, 생산 시설 또는 점령 원정 한 곳의 인구 수용량과 현재 배치 상태를 보관한다.
/// 값 변경은 전체 인구를 검증하는 PopulationManager를 통해서만 수행한다.
/// </summary>
public sealed class PopulationAllocation
{
    private readonly PopulationManager _manager;

    public PopulationAssignmentType AssignmentType {get;}

    // 연구 등 배율이 반영되기 전, 건물 데이터가 정한 원래 정원.
    public int BaseCapacity {get;}
    public int AssignedPopulation {get; private set;}

    // 생산 시설이 만드는 자원. 자원별 인력 효율 연구가 "식량 시설만" 같은 조건을 걸 때 쓴다.
    // 타워·원정·연구소는 자원과 무관하므로 None이다.
    public ResourceType ProducedResources {get;}

    // 매번 매니저를 통해 조회한다 - 쿼리 대입 순서에 의존하지 않고,
    // 연구 완료 시 값을 다시 계산해 주는 별도 패스도 필요 없다.
    public int Capacity =>
        _manager.CapacityModifierQuery?.ResolveCapacity(
            AssignmentType, BaseCapacity, ProducedResources) ?? BaseCapacity;

    public int AvailableCapacity =>
        Capacity - AssignedPopulation;

    // 정원이 줄어드는 순간 배치 인구가 정원을 넘을 수 있다 - TowerAttack.GetAttackInterval이
    // 이 값으로 제산하므로 1을 넘기면 타워가 비정상적으로 빨라진다.
    //
    // Capacity는 매번 연구 보정을 다시 계산하므로 한 번만 읽는다.
    public float StaffingRatio
    {
        get
        {
            int capacity = Capacity;
            return capacity == 0 ? 0f : Mathf.Clamp01((float)AssignedPopulation / capacity);
        }
    }

    internal PopulationAllocation(
        PopulationAssignmentType assignmentType,
        int baseCapacity,
        PopulationManager manager,
        ResourceType producedResources = ResourceType.None)

    {
        AssignmentType = assignmentType;
        BaseCapacity = baseCapacity;
        _manager = manager;
        ProducedResources = producedResources;
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
