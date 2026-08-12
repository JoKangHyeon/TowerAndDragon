using UnityEngine;

// 자원 랜드마크의 가동 효과 - 인구가 배치되어 있는 동안 특정 자원의 생산 배율을 올린다.
// YieldMultiplierEffectSO(연구)와 판정 방식을 맞췄다. 다만 연구 효과가 지형으로 대상을
// 고르는 것과 달리, 랜드마크는 자기가 놓인 청크에만 적용되므로 지형 조건이 없다.
// 적용 청크 판정은 LandmarkOperationCoordinator가 좌표를 비교해 처리한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Effects/Yield Multiplier",
    fileName = "LandmarkYieldEffect")]
public sealed class LandmarkYieldEffectSO : LandmarkEffectSO
{
    [SerializeField] private ResourceType _targetResources;

    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public override float GetYieldMultiplierBonus(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        bool isTargetResource =
            resourceType != ResourceType.None &&
            (_targetResources & resourceType) == resourceType;

        return isTargetResource ? _bonusRatio : 0f;
    }
}
