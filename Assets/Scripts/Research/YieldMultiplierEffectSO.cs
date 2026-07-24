using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Yield Multiplier",
    fileName = "YieldMultiplierEffect")]
public sealed class YieldMultiplierEffectSO : ResearchEffectSO
{
    [SerializeField] private TerrainType _targetTerrain;
    [SerializeField] private ResourceType _targetResources;
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public TerrainType TargetTerrain => _targetTerrain;
    public ResourceType TargetResources => _targetResources;
    public float BonusRatio => _bonusRatio;

    public override float GetYieldMultiplierBonus(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        bool isTargetResource =
            resourceType != ResourceType.None &&
            (_targetResources & resourceType) == resourceType;

        return terrainType == _targetTerrain && isTargetResource
            ? _bonusRatio
            : 0f;
    }
}
