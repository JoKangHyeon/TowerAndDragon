using System.Collections.Generic;
using UnityEngine;

// 청크는 "ID 하나가 붙은 임의의 셀 집합"이다 - 크기나 모양에 대한 가정을 두지 않는다.
// 어느 셀이 어느 청크에 속하는지는 ChunkLayoutTable이 정하고 GridMap이 조립한다.
public class Chunk
{
    private readonly List<GridCell> _cells;
    public IReadOnlyList<GridCell> Cells => _cells;

    // 육지(비 Default) 셀 좌표 - 인접 청크와의 실제 지형 접촉 판정(점령지 편입 등)에 재사용된다.
    // 지형은 생성 이후 절대 바뀌지 않으므로(GridCell.TerrainType은 get-only) 생성자에서 한 번만 계산해 캐싱한다.
    private readonly HashSet<Vector3Int> _landCellCoords;
    public IReadOnlyCollection<Vector3Int> LandCellCoords => _landCellCoords;

    public Vector2Int ChunkCoord { get; }
    public ChunkState CurrentState { get; private set; }
    public TerrainType DominantTerrain { get; }

    public Chunk(Vector2Int chunkCoord, List<GridCell> cells)
    {
        ChunkCoord = chunkCoord;
        _cells = cells;
        CurrentState = ChunkState.Hidden;
        DominantTerrain = ResolveDominantTerrain(cells);
        _landCellCoords = ResolveLandCellCoords(cells);
    }

    public bool ContainsLandCell(Vector3Int coord) => _landCellCoords.Contains(coord);

    public void SetState(ChunkState newState)
    {
        CurrentState = newState;

        foreach(GridCell cell in _cells)
        {
            cell.SetState(newState);
        }
    }

    // 물(Default)과 통행로(Road)는 실제 지형이 아니므로 대표 지형 계산에서 제외한다.
    // Road가 섞이면 길이 많이 지나는 청크의 대표 지형이 Road가 되어 원정 기간·적 강화 프로필·
    // 지형 아이콘이 모두 엉뚱한 값을 받는다.
    private static TerrainType ResolveDominantTerrain(List<GridCell> cells)
    {
        var counts = new Dictionary<TerrainType, int>();

        foreach (GridCell cell in cells)
        {
            if (cell.TerrainType == TerrainType.Default || cell.TerrainType == TerrainType.Road)
                continue;

            counts.TryGetValue(cell.TerrainType, out int count);
            counts[cell.TerrainType] = count + 1;
        }

        TerrainType dominant = TerrainType.Default;
        int maxCount = 0;

        foreach (var pair in counts)
        {
            if (pair.Value > maxCount)
            {
                maxCount = pair.Value;
                dominant = pair.Key;
            }
        }

        return dominant;
    }

    private static HashSet<Vector3Int> ResolveLandCellCoords(List<GridCell> cells)
    {
        var landCellCoords = new HashSet<Vector3Int>();

        foreach (GridCell cell in cells)
        {
            if (cell.TerrainType != TerrainType.Default)
                landCellCoords.Add(cell.Coord);
        }

        return landCellCoords;
    }
}
