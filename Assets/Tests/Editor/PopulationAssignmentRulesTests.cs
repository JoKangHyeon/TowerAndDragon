using NUnit.Framework;

public class PopulationAssignmentRulesTests
{
    // requested, availableCapacity, availablePopulation, expected
    [TestCase(1, 4, 10, 1)]         // 한 명 배치 - 여유 충분
    [TestCase(5, 10, 10, 5)]        // 묶음 배치 - 여유 충분
    [TestCase(5, 3, 10, 3)]         // 남은 정원이 더 작으면 정원까지만
    [TestCase(5, 10, 2, 2)]         // 가용 인구가 더 작으면 가용 인구까지만
    [TestCase(10, 4, 3, 3)]         // 최대치 요청 - 정원/가용 중 작은 쪽
    [TestCase(1, 0, 10, 0)]         // 정원이 꽉 찬 건물
    [TestCase(1, 4, 0, 0)]          // 남은 인구가 없음
    [TestCase(0, 4, 10, 0)]         // 요청량 0
    [TestCase(-1, 4, 10, 0)]        // 음수 요청은 0으로 취급
    public void ResolveAssignAmount_ClampsToSmallestLimit(
        int requested,
        int availableCapacity,
        int availablePopulation,
        int expected)
    {
        int amount = PopulationAssignmentRules.ResolveAssignAmount(
            requested,
            availableCapacity,
            availablePopulation);

        Assert.That(amount, Is.EqualTo(expected));
    }

    // requested, assignedPopulation, expected
    [TestCase(1, 4, 1)]             // 한 명 회수
    [TestCase(5, 10, 5)]            // 묶음 회수
    [TestCase(5, 3, 3)]             // 배치 인원이 더 적으면 그만큼만
    [TestCase(4, 4, 4)]             // 전원 회수
    [TestCase(1, 0, 0)]             // 배치된 인원이 없음
    [TestCase(0, 4, 0)]             // 요청량 0
    [TestCase(-1, 4, 0)]            // 음수 요청은 0으로 취급
    public void ResolveUnassignAmount_ClampsToAssignedPopulation(
        int requested,
        int assignedPopulation,
        int expected)
    {
        int amount = PopulationAssignmentRules.ResolveUnassignAmount(
            requested,
            assignedPopulation);

        Assert.That(amount, Is.EqualTo(expected));
    }

    [Test]
    public void TryAssignClamped_NullTarget_ReturnsFalse()
    {
        bool isAssigned = PopulationAssignmentRules.TryAssignClamped(null, null, 1);

        Assert.That(isAssigned, Is.False);
    }

    [Test]
    public void TryUnassignClamped_NullTarget_ReturnsFalse()
    {
        bool isUnassigned = PopulationAssignmentRules.TryUnassignClamped(null, 1);

        Assert.That(isUnassigned, Is.False);
    }

    [Test]
    public void TryUnassignClamped_NothingToUnassign_DoesNotCallTarget()
    {
        var target = new StubAllocationTarget { AssignedPopulation = 0, Capacity = 4 };

        bool isUnassigned = PopulationAssignmentRules.TryUnassignClamped(target, 5);

        Assert.That(isUnassigned, Is.False);
        Assert.That(target.LastUnassignRequest, Is.Zero);
    }

    private sealed class StubAllocationTarget : IPopulationAllocationTarget
    {
        public int AssignedPopulation { get; set; }
        public int Capacity { get; set; }
        public int AvailableCapacity => Capacity - AssignedPopulation;
        public bool IsInitialized => true;

        public int LastAssignRequest { get; private set; }
        public int LastUnassignRequest { get; private set; }

        public bool TryAssign(int amount)
        {
            LastAssignRequest = amount;
            AssignedPopulation += amount;
            return true;
        }

        public bool TryUnassign(int amount)
        {
            LastUnassignRequest = amount;
            AssignedPopulation -= amount;
            return true;
        }
    }
}
