using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Tower Data/Thorn Tower Data",
    fileName = "ThornTowerData"
)]
public sealed class ThornTowerData : TowerData
{
    [Header("Thorn")]
    [SerializeField] private float _thornDamage;

    public override TowerCategory Category => TowerCategory.Special;

    // 가시 피해는 고정 데미지
    public float ThornDamage => _thornDamage;
}
