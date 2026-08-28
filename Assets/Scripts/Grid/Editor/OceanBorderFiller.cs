using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Grid.prefab의 Ground 타일맵 바다 띠를 넓히는 일회성 저작 도구.
// 카메라가 좌측 건설 창 등 UI 폭만큼 더 바깥으로 이동할 수 있게 되면(CameraController._uiInset*),
// 그만큼 바다를 더 칠해 두지 않으면 화면에 빈 공간(void)이 보인다.
// 에디터 전용이므로 문자열·숫자 리터럴 제한(CLAUDE.md 3·4항)의 예외 대상이다.
public static class OceanBorderFiller
{
    private const string GRID_PREFAB_PATH = "Assets/Prefabs/Grid.prefab";
    private const string GROUND_CHILD_NAME = "Ground";
    private const string SEA_TILE_PATH = "Assets/Data/Tiles/ISO_Autotile_Water_Shores_01_Animated_Safe.asset";

    // u = x-y, v = x+y (Isometric 셀 -> 월드 변환의 두 대각축).
    // 기존 맵은 |u|<=72, |v|<=72 (정사각형)로 칠해져 있다 - 여기서 u·v 반경을 축별로 따로 늘리면
    // 월드 X·Y 반경을 독립적으로 조절할 수 있다(worldX = u*0.5, worldY = v*0.25).
    // 원점(u=0,v=0)을 중심으로 대칭 확장해야 Tilemap.cellBounds 중심이 셀 (0,0)에서 벗어나지
    // 않는다 - GridMap.GetCenterCell()을 Castle이 그대로 쓰므로 어긋나면 성 배치가 밀린다.
    private const int NEW_HALF_U = 94; // worldX 반경 47.0 (기존 36.0)
    private const int NEW_HALF_V = 80; // worldY 반경 20.0 (기존 18.0)

    [MenuItem("TowerAndDragon/Grid/바다 경계 넓히기 (Ocean Border Fill)")]
    public static void FillOceanBorder()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GRID_PREFAB_PATH);
        try
        {
            Transform groundTransform = root.transform.Find(GROUND_CHILD_NAME);
            if (groundTransform == null)
            {
                Debug.LogError($"[OceanBorderFiller] {GROUND_CHILD_NAME} 자식을 찾을 수 없습니다.");
                return;
            }

            Tilemap tilemap = groundTransform.GetComponent<Tilemap>();
            TileBase seaTile = AssetDatabase.LoadAssetAtPath<TileBase>(SEA_TILE_PATH);
            if (tilemap == null || seaTile == null)
            {
                Debug.LogError("[OceanBorderFiller] Ground Tilemap 또는 바다 타일 에셋을 찾을 수 없습니다.");
                return;
            }

            int filledCount = FillMissingCells(tilemap, seaTile);

            tilemap.RefreshAllTiles();
            tilemap.CompressBounds();

            BoundsInt bounds = tilemap.cellBounds;
            bool isCentered = bounds.xMin + bounds.size.x / 2 == 0 && bounds.yMin + bounds.size.y / 2 == 0;

            Debug.Log($"[OceanBorderFiller] 신규 바다 타일 {filledCount}개 도색. " +
                      $"cellBounds origin={bounds.position} size={bounds.size} 중심 대칭={isCentered}");

            if (!isCentered)
            {
                Debug.LogError("[OceanBorderFiller] cellBounds 중심이 (0,0)이 아닙니다 - " +
                                "GridMap.GetCenterCell() 기반 성 배치가 어긋나므로 저장을 중단합니다.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, GRID_PREFAB_PATH);
            Debug.Log("[OceanBorderFiller] Grid.prefab 저장 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 대상 다이아몬드(|u|<=NEW_HALF_U && |v|<=NEW_HALF_V) 안에서 아직 타일이 없는 셀만 채운다.
    // 기존 타일(육지·기존 바다)은 절대 덮어쓰지 않는다.
    private static int FillMissingCells(Tilemap tilemap, TileBase seaTile)
    {
        int radius = (NEW_HALF_U + NEW_HALF_V) / 2;
        int filledCount = 0;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int u = x - y;
                int v = x + y;
                if (Mathf.Abs(u) > NEW_HALF_U || Mathf.Abs(v) > NEW_HALF_V)
                    continue;

                Vector3Int cell = new Vector3Int(x, y, 0);
                if (tilemap.HasTile(cell))
                    continue;

                tilemap.SetTile(cell, seaTile);
                filledCount++;
            }
        }

        return filledCount;
    }
}
