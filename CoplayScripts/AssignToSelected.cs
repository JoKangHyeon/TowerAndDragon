using UnityEngine;

// 검증용: 선택된 건물에 인구를 넣거나 뺀다. 실제 버튼과 같은 규칙(PopulationAssignmentRules)을 거친다.
public static class AssignToSelected
{
    public static void AssignOne() => Change(1);

    public static void AssignAll() => Change(int.MaxValue);

    public static void UnassignAll() => Change(-int.MaxValue);

    private static void Change(int amount)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[A] 플레이 모드가 아닙니다.");
            return;
        }

        var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
        var populationManager = Object.FindFirstObjectByType<PopulationManager>();
        if (controller == null || populationManager == null)
        {
            Debug.LogError("[A] 컨트롤러/매니저 없음");
            return;
        }

        Building selected = controller.SelectedBuilding;
        if (selected == null)
        {
            Debug.LogError("[A] 선택된 건물 없음");
            return;
        }

        var target = selected.GetComponent<IPopulationAllocationTarget>();
        if (target == null)
        {
            Debug.LogError("[A] 배치 대상 아님");
            return;
        }

        bool ok = amount > 0
            ? PopulationAssignmentRules.TryAssignClamped(target, populationManager, amount)
            : PopulationAssignmentRules.TryUnassignClamped(target, -amount);

        Debug.Log($"[A] {selected.name} 요청={amount} 성공={ok} " +
                  $"배치={target.AssignedPopulation}/{target.Capacity} " +
                  $"가용={populationManager.AvailablePopulation}/{populationManager.MaxPopulation}");
    }
}
