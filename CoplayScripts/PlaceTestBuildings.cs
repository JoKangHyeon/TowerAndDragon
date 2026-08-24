using UnityEditor;
using UnityEngine;

// 검증용: 낮 시작 상태의 씬에 생산시설 하나와 타워 하나를 직접 세운다.
// 실제 클릭 흐름(비용 검사·건설 모드)을 거치지 않고 GridMap.ConstructBuilding을 바로 부른다.
public static class PlaceTestBuildings
{
    private const string FARM_PREFAB = "Assets/Prefabs/Building/Factory/BasicFactory_FarmField.prefab";
    private const string CROSSBOW_PREFAB = "Assets/Data/TowerData/TowerPrefab/TP_CrossBow.prefab";
    private const string TIME_TOWER_PREFAB = "Assets/Data/TowerData/TowerPrefab/TP_TimeTower.prefab";

    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[P] 플레이 모드가 아닙니다.");
            return;
        }

        var grid = Object.FindFirstObjectByType<GridMap>();
        if (grid == null)
        {
            Debug.LogError("[P] GridMap 없음");
            return;
        }

        Place(grid, FARM_PREFAB);
        Place(grid, CROSSBOW_PREFAB);
        Place(grid, TIME_TOWER_PREFAB);
    }

    private static void Place(GridMap grid, string prefabPath)
    {
        var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabObject == null)
        {
            Debug.LogError($"[P] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        var prefab = prefabObject.GetComponent<Building>();
        if (prefab == null)
        {
            Debug.LogError($"[P] Building 컴포넌트 없음: {prefabPath}");
            return;
        }

        int tried = 0;
        foreach (Vector3Int coord in grid.EnumerateAllCoords())
        {
            tried++;
            if (!grid.CanConstructBuilding(coord))
            {
                continue;
            }

            Building placed = grid.ConstructBuilding(prefab, coord, 0);
            if (placed != null)
            {
                Debug.Log($"[P] 세움 {placed.name} at {coord} (후보 {tried}개 훑음)");
                return;
            }
        }

        Debug.LogError($"[P] 세울 자리를 못 찾음: {prefabPath} (후보 {tried}개)");
    }
}
