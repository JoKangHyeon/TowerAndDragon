using System.Collections.Generic;
using UnityEngine;

public class Chunk 
{
    public const int CHUNK_SIZE = 9;
    
    private readonly List<GridCell> _cells;
    public IReadOnlyList<GridCell> Cells => _cells;

    public Vector2Int ChunkCoord { get; }
    public State CurrentState { get; private set; }
    public TerrainType DominantTerrain { get; }

    public Chunk(Vector2Int chunkCoord, List<GridCell> cells)
    {
        ChunkCoord = chunkCoord;
        _cells = cells;
        CurrentState = State.Hidden;
    }

    public void SetState(State newState)
    {
        CurrentState = newState;

        foreach(GridCell cell in _cells)
        {
            cell.SetState(newState);
        }
    }
}
