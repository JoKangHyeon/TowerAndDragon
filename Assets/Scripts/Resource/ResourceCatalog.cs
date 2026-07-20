using System.Collections.Generic;
using UnityEngine;

/// <summary>게임에 존재하는 전체 자원 목록. ResourceManager가 보유량 시드에, UI가 표시 목록에 사용한다.</summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Catalog", fileName = "ResourceCatalog")]
public class ResourceCatalog : ScriptableObject
{
    [SerializeField] private ResourceData[] _resources;

    public IReadOnlyList<ResourceData> All => _resources;

    // 종류로 자원 데이터를 찾는다. 7종 수준이라 선형 탐색로 충분.
    public bool TryGet(ResourceType type, out ResourceData data)
    {
        foreach (ResourceData resource in _resources)
        {
            if (resource != null && resource.Type == type)
            {
                data = resource;
                return true;
            }
        }

        data = null;
        return false;
    }
}
