using UnityEngine;

// 인구가 배치되어 가동 중인 랜드마크의 지속 효과를 청크 생산 배율로 합성한다.
// ChunkYieldMultiplierComposite에 소스로 등록되며(연구·용 스킬트리와 같은 자리),
// 소스 간에는 곱연산이므로 여기서는 랜드마크끼리의 보너스만 더해서 돌려준다.
//
// 랜드마크 효과는 자기가 놓인 청크에만 적용된다 - 조회 좌표와 랜드마크 좌표가 같을 때만 센다.
public sealed class LandmarkOperationCoordinator : MonoBehaviour, IChunkYieldMultiplierQuery
{
    private const float BASE_YIELD_MULTIPLIER = 1f;

    [SerializeField] private LandmarkManager _landmarkManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ChunkYieldMultiplierComposite _yieldComposite;

    private void OnEnable()
    {
        if (_landmarkManager == null || _yieldComposite == null)
        {
            Debug.LogError(
                "[LandmarkOperationCoordinator] 참조 누락 - 랜드마크 가동 효과가 전혀 적용되지 않습니다.",
                this);
            return;
        }

        _yieldComposite.Register(this);
    }

    private void OnDisable()
    {
        if (_yieldComposite != null)
        {
            _yieldComposite.Unregister(this);
        }
    }

    public float GetYieldMultiplier(Vector2Int chunkCoord, ResourceType resourceType)
    {
        if (_landmarkManager == null)
        {
            return BASE_YIELD_MULTIPLIER;
        }

        Chunk chunk = _gridMap != null ? _gridMap.GetChunk(chunkCoord) : null;
        TerrainType terrainType = chunk != null
            ? chunk.DominantTerrain
            : TerrainType.Default;

        float bonusRatio = 0f;

        foreach (Landmark landmark in _landmarkManager.Landmarks)
        {
            if (landmark.ChunkCoord != chunkCoord || !landmark.IsOperating || landmark.Data == null)
            {
                continue;
            }

            foreach (LandmarkEffectSO effect in landmark.Data.OperationEffects)
            {
                if (effect != null)
                {
                    bonusRatio += effect.GetYieldMultiplierBonus(
                        chunkCoord,
                        terrainType,
                        resourceType);
                }
            }
        }

        return BASE_YIELD_MULTIPLIER + bonusRatio;
    }
}
