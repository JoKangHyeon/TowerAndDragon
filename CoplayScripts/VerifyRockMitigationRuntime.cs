using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

// 검증용: 암석 지형 위 시설의 돌 유지비가 암석 새끼용(버프모드) 반경 안에서 사라지는지
// 실제 계산 경로(TerrainPenaltySystem.ResolvePreview -> TerrainPenaltyScaleComposite ->
// BabyDragonBuffSystem.GetPenaltyScale)로 확인한다.
public static class VerifyRockMitigationRuntime
{
    private const string DRAGON_PREFAB = "Assets/Prefabs/Building/BabyDragonTower.prefab";
    private const string STONE_DATA = "Assets/Data/BabyDragon/Stone/BD_Stone.asset";
    private const int TEST_POPULATION = 10;

    public static string Execute()
    {
        if (!Application.isPlaying)
        {
            return "FAIL: 플레이 모드가 아닙니다.";
        }

        var sb = new StringBuilder();

        var grid = Object.FindFirstObjectByType<GridMap>();
        var penalty = Object.FindFirstObjectByType<TerrainPenaltySystem>();
        var buffSystem = Object.FindFirstObjectByType<BabyDragonBuffSystem>();

        if (grid == null || penalty == null || buffSystem == null)
        {
            return $"FAIL: grid={grid} penalty={penalty} buffSystem={buffSystem}";
        }

        // 1. 암석 셀 하나 찾기
        Vector3Int rockCoord = default;
        bool found = false;
        int rockCount = 0;
        foreach (Vector3Int coord in grid.EnumerateAllCoords())
        {
            if (grid.GetTerrainType(coord) != TerrainType.Rock) continue;
            rockCount++;
            if (!found) { rockCoord = coord; found = true; }
        }
        if (!found) return "FAIL: 암석 지형 셀이 없습니다.";

        Vector3 rockWorld = grid.ConvertGridToWorld(rockCoord);
        var footprint = new List<Vector3Int> { rockCoord };
        sb.AppendLine($"암석 셀 {rockCount}개, 테스트 셀 {rockCoord} world={rockWorld}");

        // 2. 완화 전 기준선
        Report(sb, "완화 전     ", penalty.ResolvePreview(footprint, rockWorld));

        // 3. 암석 새끼용을 암석 셀에 세운다
        var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DRAGON_PREFAB);
        var data = AssetDatabase.LoadAssetAtPath<BabyDragonData>(STONE_DATA);
        if (prefabObject == null || data == null) return $"FAIL: prefab={prefabObject} data={data}";

        var prefab = prefabObject.GetComponent<BabyDragonTower>();
        if (prefab == null) return "FAIL: 프리팹에 BabyDragonTower 없음";

        Vector3Int placedCoord = rockCoord;
        Building placedBuilding = grid.CanConstructBuilding(rockCoord)
            ? grid.ConstructBuilding(prefab, rockCoord, 0)
            : null;

        if (placedBuilding == null)
        {
            // 암석 청크가 미점령이면 못 짓는다 - 아무 곳에나 세우고 좌표만 옮긴다
            // (완화 판정은 transform.position 기준 타원이므로 경로가 같다).
            foreach (Vector3Int coord in grid.EnumerateAllCoords())
            {
                if (!grid.CanConstructBuilding(coord)) continue;
                placedBuilding = grid.ConstructBuilding(prefab, coord, 0);
                if (placedBuilding != null) { placedCoord = coord; break; }
            }
            if (placedBuilding == null) return "FAIL: 새끼용을 세울 자리가 없습니다.";
            placedBuilding.transform.position = rockWorld;
            sb.AppendLine("(암석 셀에 직접 건설 불가 -> 좌표 이동으로 대체)");
        }

        var dragon = (BabyDragonTower)placedBuilding;
        dragon.Setup(data);
        dragon.BindRecord(new BabyDragon { DragonType = DragonType.Stone, DragonName = "TEST" });
        dragon.SetMode(BabyDragonMode.Buff);

        sb.AppendLine($"새끼용: pos={dragon.transform.position} mode={dragon.Mode} canOperate={dragon.CanOperate} " +
            $"radius={buffSystem.GetEffectiveBuffRadius(dragon)} mitigate={string.Join(",", dragon.DragonData.PenaltyMitigationTerrains)}");

        // 4. 완화 후
        Report(sb, "완화 후     ", penalty.ResolvePreview(footprint, rockWorld));

        // 5. 대조군 A - 같은 암석 셀, 반경 밖 좌표
        Vector3 farAway = rockWorld + new Vector3(50f, 50f, 0f);
        Report(sb, "반경 밖     ", penalty.ResolvePreview(footprint, farAway));

        // 6. 대조군 B - 설원 셀(암석 새끼용은 완화하지 않아야 한다)
        foreach (Vector3Int coord in grid.EnumerateAllCoords())
        {
            if (grid.GetTerrainType(coord) != TerrainType.Snow) continue;
            var snowFootprint = new List<Vector3Int> { coord };
            Report(sb, "설원(대조군)", penalty.ResolvePreview(snowFootprint, rockWorld));
            break;
        }

        // 7. 철거 후 원복
        grid.RemoveBuilding(placedCoord);
        Report(sb, "철거 후     ", penalty.ResolvePreview(footprint, rockWorld));

        return sb.ToString();
    }

    private static void Report(StringBuilder sb, string label, TerrainPenaltyModifiers m)
    {
        TerrainUpkeepRules.FacilityUpkeep upkeep =
            TerrainUpkeepRules.ResolveFacilityUpkeep(TEST_POPULATION, m);
        sb.AppendLine($"{label} | yieldMul={m.YieldMultiplier} atkSpdMul={m.AttackSpeedMultiplier} " +
            $"wood/pop={m.WoodUpkeepPerPopulation} stone/pop={m.StoneUpkeepPerPopulation} " +
            $"=> 인구{TEST_POPULATION} 실지불 나무{upkeep.WoodAmount} 돌{upkeep.StoneAmount}");
    }
}
