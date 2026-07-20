using UnityEngine;

// 청크 단위 연구 해금 조회 접점. 본체(연구 비용/기간/UI)는 추후 별도 설계 - 지금은 GridMap이 참조할 형태만 확정한다.
public interface IChunkResearchUnlockQuery
{
    bool IsUnlocked(Vector2Int chunkCoord, ResourceType resourceType);
}
