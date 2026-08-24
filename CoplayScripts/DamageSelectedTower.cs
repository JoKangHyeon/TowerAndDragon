using UnityEngine;

// 검증용: 선택된 타워에 피해를 준다. 선택도 인구도 바뀌지 않으므로,
// 창의 체력 행이 따라 내려가면 주기 갱신(REFRESH_INTERVAL_SECONDS)이 동작하는 것이다.
public static class DamageSelectedTower
{
    private const float DAMAGE = 30f;

    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[H] 플레이 모드가 아닙니다.");
            return;
        }

        var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
        Building selected = controller != null ? controller.SelectedBuilding : null;
        if (selected == null)
        {
            Debug.LogError("[H] 선택된 건물 없음");
            return;
        }

        var health = selected.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogError("[H] Health 없음");
            return;
        }

        health.TakeDamage(DAMAGE);
        Debug.Log($"[H] {selected.name} 피해 {DAMAGE} → {health.CurrentHealth}/{health.MaxHealth}");
    }
}
