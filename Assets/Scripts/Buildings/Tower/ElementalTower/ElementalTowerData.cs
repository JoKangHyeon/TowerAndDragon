using UnityEngine;

[CreateAssetMenu (
    menuName = "TowerAndDragon/Elemental Tower Data",
    fileName = "ElementalTowerData"
)]
public sealed class ElementalTowerData : TowerData, ITowerAuraDataProvider, IElementalAttackData
{
    [Header("Element")]
    [SerializeField] private DragonType _dragonType;

    [Header("Tower Aura")]
    [SerializeField] private TowerAuraDataSO _towerAura;

    public override TowerCategory Category => TowerCategory.Elemental;

    public DragonType DragonType => _dragonType;

    public TowerAuraDataSO TowerAura => _towerAura;

    public bool HasTowerAura =>
        _towerAura != null && _towerAura.HasArea;
}
