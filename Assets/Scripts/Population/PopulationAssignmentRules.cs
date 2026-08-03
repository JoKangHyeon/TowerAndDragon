using UnityEngine;

// 인구 배치/회수 요청량을 실제로 처리 가능한 수로 깎는 규칙.
// PopulationManager.TryAssign/TryUnassign은 all-or-nothing이라(요청량이 정원이나 가용 인구를
// 넘으면 한 명도 들어가지 않고 false) 호출 전에 반드시 클램프해야 한다.
// UI_PopulationAllocationWindow(버튼)와 WorkerModeController(클릭)가 같은 규칙을 쓰도록
// 계산을 여기로 모았다. Resolve* 는 부수효과가 없는 순수 계산이라 EditMode 테스트 대상이다.
public static class PopulationAssignmentRules
{
    // 요청량, 남은 정원, 남은 가용 인구 중 가장 작은 값. 음수 요청은 0으로 취급한다.
    public static int ResolveAssignAmount(int requested, int availableCapacity, int availablePopulation)
    {
        int limit = Mathf.Min(availableCapacity, availablePopulation);
        return Mathf.Max(0, Mathf.Min(requested, limit));
    }

    // 요청량과 현재 배치 인원 중 작은 값. 음수 요청은 0으로 취급한다.
    public static int ResolveUnassignAmount(int requested, int assignedPopulation)
    {
        return Mathf.Max(0, Mathf.Min(requested, assignedPopulation));
    }

    // 처리 가능한 만큼만 배치한다. 배치할 인원이 한 명도 없으면 아무것도 하지 않고 false.
    public static bool TryAssignClamped(
        IPopulationAllocationTarget target,
        PopulationManager populationManager,
        int requested)
    {
        if (target == null || populationManager == null)
        {
            return false;
        }

        int amount = ResolveAssignAmount(
            requested,
            target.AvailableCapacity,
            populationManager.AvailablePopulation);

        return amount > 0 && target.TryAssign(amount);
    }

    // 처리 가능한 만큼만 회수한다. 회수할 인원이 없으면 아무것도 하지 않고 false.
    public static bool TryUnassignClamped(IPopulationAllocationTarget target, int requested)
    {
        if (target == null)
        {
            return false;
        }

        int amount = ResolveUnassignAmount(requested, target.AssignedPopulation);

        return amount > 0 && target.TryUnassign(amount);
    }
}
