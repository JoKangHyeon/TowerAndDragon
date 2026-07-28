using UnityEngine;

/// <summary>
/// 속성(DragonType) → BabyDragonData 조회. 알 성장 설정값 조회와, 배치 시
/// "이 속성이면 어떤 데이터를 주입할지" 결정 양쪽에 쓰인다(BuildingCatalog와 같은 패턴).
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Baby Dragon Data Catalog", fileName = "BabyDragonDataCatalog")]
public class BabyDragonDataCatalog : ScriptableObject
{
    [SerializeField] private BabyDragonData[] _entries;

    public bool TryResolve(DragonType dragonType, out BabyDragonData data)
    {
        foreach (BabyDragonData entry in _entries)
        {
            if (entry != null && entry.DragonType == dragonType)
            {
                data = entry;
                return true;
            }
        }

        data = null;
        return false;
    }
}
