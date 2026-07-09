using UnityEngine;

[CreateAssetMenu(fileName = "New Building Catalog", menuName = "Grid/Building Catalog")]
public class BuildingCatalog : ScriptableObject
{
    [SerializeField] private Building[] _buildingPrefabs;

    public T GetPrefab<T>() where T : Building
    {
        foreach (Building prefab in _buildingPrefabs)
        {
            if (prefab is T typedPrefab)
                return typedPrefab;
        }

        return null;
    }
}
