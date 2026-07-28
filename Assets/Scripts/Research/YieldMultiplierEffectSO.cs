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

    // 특화 자원·슬라임은 자원 종류 자체가 바이옴을 특정하므로 지형 게이트가 중복이다.
    // 기본값 false이므로 기존 RE_GrassYieldMultiplier의 동작은 바뀌지 않는다.
    [SerializeField] private bool _ignoresTerrain;

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
        bool isTargetTerrain = _ignoresTerrain || terrainType == _targetTerrain;

        return isTargetTerrain && isTargetResource
            ? _bonusRatio
            : 0f;
    }
}
