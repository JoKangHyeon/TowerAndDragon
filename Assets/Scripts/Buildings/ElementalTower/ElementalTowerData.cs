using UnityEngine;

[CreateAssetMenu (
    menuName = "TowerAndDragon/Elemental Tower Data",
    fileName = "ElementalTowerData"
)]
public sealed class ElementalTowerData : TowerData
{
    [Header("Element")]
    [SerializeField] private DragonType _dragonType;

    public DragonType DragonType => _dragonType;
}