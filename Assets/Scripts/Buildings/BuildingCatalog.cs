using System.Collections.Generic;
using UnityEngine;

// 세이브가 "이 id의 건물을 다시 지어라"를 해석하는 유일한 레지스트리.
// 배치 가능한 건물 목록 자체는 아직 UI(UI_BuildModeWindow._filterTabs)가 들고 있으므로,
// 둘이 어긋나면 저장은 되는데 복원이 안 되는 상태가 된다 - BuildingCatalogTools가 그 불일치를 잡는다.
[CreateAssetMenu(fileName = "New Building Catalog", menuName = "Grid/Building Catalog")]
public class BuildingCatalog : ScriptableObject
{
    [Tooltip("세이브 복원 대상이 되는 모든 건물 프리팹. 각 프리팹의 Building.PrefabId가 키가 된다.")]
    [SerializeField] private Building[] _buildingPrefabs;

    // 첫 조회 때 한 번만 만든다. ScriptableObject는 씬 로드 사이에도 살아남으므로
    // 에디터에서 배열을 고쳤을 때 갱신되지 않는데, 그때는 재컴파일/재임포트로 다시 로드된다.
    private Dictionary<string, Building> _prefabById;

    public IReadOnlyList<Building> All => _buildingPrefabs;

    public bool TryGetPrefab(string prefabId, out Building prefab)
    {
        if (string.IsNullOrWhiteSpace(prefabId))
        {
            prefab = null;
            return false;
        }

        EnsureIndexBuilt();
        return _prefabById.TryGetValue(prefabId, out prefab);
    }

    private void EnsureIndexBuilt()
    {
        if (_prefabById != null)
        {
            return;
        }

        _prefabById = new Dictionary<string, Building>();

        if (_buildingPrefabs == null)
        {
            return;
        }

        foreach (Building prefab in _buildingPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogError("[BuildingCatalog] 끊긴 프리팹 참조가 있습니다 - 인스펙터에서 다시 지정해야 합니다.", this);
                continue;
            }

            if (!prefab.IsSaveable)
            {
                Debug.LogError(
                    $"[BuildingCatalog] '{prefab.name}'에 PrefabId가 비어 있어 세이브로 되살릴 수 없습니다.", this);
                continue;
            }

            // 먼저 등록된 쪽을 남긴다 - 나중 것으로 덮으면 어느 프리팹이 이겼는지 로그만으로 알 수 없다.
            if (_prefabById.ContainsKey(prefab.PrefabId))
            {
                Debug.LogError(
                    $"[BuildingCatalog] PrefabId '{prefab.PrefabId}'가 중복입니다 " +
                    $"('{prefab.name}'을 무시하고 먼저 등록된 프리팹을 씁니다).", this);
                continue;
            }

            _prefabById.Add(prefab.PrefabId, prefab);
        }
    }
}
