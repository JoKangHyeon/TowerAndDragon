using UnityEngine;

public static class SurveyPlacedBuildings
{
    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[S] 플레이 모드가 아닙니다.");
            return;
        }

        var towers = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
        var factories = Object.FindObjectsByType<Factory>(FindObjectsSortMode.None);
        var labs = Object.FindObjectsByType<ResearchLab>(FindObjectsSortMode.None);
        var grid = Object.FindFirstObjectByType<GridMap>();
        var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
        var window = Object.FindFirstObjectByType<UI_PopulationAllocationWindow>();

        Debug.Log($"[S] tower={towers.Length} factory={factories.Length} lab={labs.Length} " +
                  $"grid={(grid != null)} controller={(controller != null)} window={(window != null)}");

        foreach (Tower t in towers)
        {
            bool hasCoord = grid != null && grid.TryGetOccupiedCoord(t, out Vector3Int c);
            Vector3Int coord = default;
            if (grid != null)
            {
                grid.TryGetOccupiedCoord(t, out coord);
            }

            var pop = t.GetComponent<TowerPopulation>();
            Debug.Log($"[S] TOWER {t.name} data={(t.Data == null ? "null" : t.Data.name)} " +
                      $"coord={(hasCoord ? coord.ToString() : "?")} " +
                      $"pop={(pop == null ? "none" : pop.AssignedPopulation + "/" + pop.Capacity)} " +
                      $"init={(pop != null && pop.IsInitialized)}");
        }

        foreach (Factory f in factories)
        {
            Vector3Int coord = default;
            bool hasCoord = grid != null && grid.TryGetOccupiedCoord(f, out coord);
            Debug.Log($"[S] FACTORY {f.name} coord={(hasCoord ? coord.ToString() : "?")}");
        }
    }
}
