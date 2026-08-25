using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Special Tower Data",
    fileName = "SpecialTowerData")]
public sealed class SpecialTowerData : TowerData, ITowerAuraDataProvider
{
    [Header("Tower Aura")]
    [SerializeField] private TowerAuraDataSO _towerAura;

    public override TowerCategory Category => TowerCategory.Special;

    public TowerAuraDataSO TowerAura => _towerAura;

    public bool HasTowerAura =>
        _towerAura != null && _towerAura.HasArea;
}
