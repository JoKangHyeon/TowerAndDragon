using UnityEngine;

/// <summary>셀 좌표계의 이웃 방향 공용 상수.
/// 청크 인접 그래프 파생(GridMap.BuildChunkAdjacency)과 지형 접촉 폭 판정
/// (ConquestManager.CountLandBorderContact)이 같은 배열을 공유하도록 한 곳에 모았다.
/// 이 프로젝트의 셀 좌표는 항상 z=0으로 정규화되므로(GridMap.ConvertWorldToGrid 참고) z는 전부 0이다.</summary>
public static class CellDirections
{
    /// <summary>변을 공유하는 이웃 - 상하좌우 4방향.</summary>
    public static readonly Vector3Int[] ORTHOGONAL =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
    };

    /// <summary>꼭짓점만 공유하는 이웃 - 대각선 4방향.</summary>
    public static readonly Vector3Int[] DIAGONAL =
    {
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0),
    };

    /// <summary>변 또는 꼭짓점을 공유하는 이웃 - 8방향.</summary>
    public static readonly Vector3Int[] ALL_EIGHT =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0),
    };
}
