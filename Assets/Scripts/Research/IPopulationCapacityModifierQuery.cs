public interface IPopulationCapacityModifierQuery
{
    // producedResources: 생산 시설이 만드는 자원. 타워·원정·연구소처럼 자원과 무관한
    // 배치는 ResourceType.None을 넘긴다(자원 조건이 없는 효과는 그래도 전부 적용된다).
    int ResolveCapacity(
        PopulationAssignmentType assignmentType,
        int baseCapacity,
        ResourceType producedResources);
}
