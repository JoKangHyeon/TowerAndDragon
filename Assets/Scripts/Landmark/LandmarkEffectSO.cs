using UnityEngine;

// 인구가 배치되어 있는 동안에만 적용되는 랜드마크 지속 효과.
// ResearchEffectSO와 같은 "virtual 질의 묶음" 형태를 의도적으로 따른다 - 시그니처를 맞춰야
// LandmarkOperationCoordinator가 기존 ChunkYieldMultiplierComposite에 그대로 합류할 수 있다.
// 파생 클래스는 자기가 답할 수 있는 질의만 override하고 나머지는 기본값(무효과)으로 둔다.
public abstract class LandmarkEffectSO : ScriptableObject
{
    public virtual float GetYieldMultiplierBonus(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        return 0f;
    }
}
