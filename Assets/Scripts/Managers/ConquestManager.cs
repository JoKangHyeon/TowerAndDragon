using UnityEngine;
using System.Collections.Generic;

public class ConquestManager : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private CycleManager _cycleManager;

    [SerializeField]
    private ConquestDurationTable _durationTable;

    // 주둔지 프리팹 필요함
    [SerializeField]
    private Building _garrisonPrefab;

    private readonly List<ConquestExpedition> _activeExpeditions = new();

    private void Awake()
    {
        //_cycleManager.OnNightEnd.AddListener(OnSettlement);
    }

    private void OnDestroy()
    {
        //_cycleManager.OnNightEnd.RemoveListener(OnSettlement);
    }

    
    // 현재 점령지 기준 4칸만 점령을 보낼 수 있음
    public bool CanSendExpedition(Vector2Int targetChunkCoord)
    {
        Chunk chunk = _gridMap.GetChunk(targetChunkCoord);
        if (chunk == null || chunk.CurrentState != State.Visible)
            return false;
        
        foreach (ConquestExpedition expedition in _activeExpeditions)
        {
            if (expedition.TargetChunkCoord == targetChunkCoord)
                return false;
        }

        return true;
    }

    public bool SendExpedition(Vector2Int targetChunkCoord)
    {
        if (!CanSendExpedition(targetChunkCoord))
            return false;
        
        int distance = _gridMap.GetChunkDistanceFromHome(targetChunkCoord);
        int daysRequired = _durationTable.Resolve(distance);

        _activeExpeditions.Add(new ConquestExpedition(targetChunkCoord, daysRequired));
        return true;
    }

    private void OnSettlement(int currentCycle)
    {
        for (int i = _activeExpeditions.Count - 1; i >= 0; i--)
        {
            ConquestExpedition expedition = _activeExpeditions[i];
            expedition.AdvanceDay();

            if (!expedition.IsComplete)
                continue;
            
            CompleteConquest(expedition);
            _activeExpeditions.RemoveAt(i);
        }
    }

    private void CompleteConquest(ConquestExpedition expedition)
    {
        _gridMap.SetChunkState(expedition.TargetChunkCoord, State.Conquered);
        ExpandVisibility(expedition.TargetChunkCoord);
        PlaceGarrison(expedition.TargetChunkCoord);
    }

    private void ExpandVisibility(Vector2Int chunkCoord)
    {
        foreach (Chunk neighbor in _gridMap.GetAdjacentChunks(chunkCoord))
        {
            if (neighbor.CurrentState == State.Hidden)
                _gridMap.SetChunkState(neighbor.ChunkCoord, State.Visible);
        }
    }

    private void PlaceGarrison(Vector2Int chunkCoord)
    {
        if (_garrisonPrefab == null)
        {
            Debug.LogWarning("[ConquestManger] 주둔지 프리팹 없음");
            return;
        }

        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        GridCell anchorCell = FindConstructableCellNearestCenter(chunk);
        if (anchorCell == null)
        {
            Debug.LogWarning($"[ConquestManager] 주둔지 배치 가능한 셀이 없습니다 - {chunkCoord}");
            return;
        }

        _gridMap.ConstructBuilding(_garrisonPrefab, anchorCell.Coord);
    }

    private GridCell FindConstructableCellNearestCenter(Chunk chunk)
    {
        var sum = Vector3Int.zero;
        foreach (GridCell cell in chunk.Cells)
            sum += cell.Coord;
        
        Vector3 center = (Vector3) sum / chunk.Cells.Count;

        GridCell nearestCell = null;
        float nearestSqrDistance = float.MaxValue;

        foreach(GridCell cell in chunk.Cells)
        {
            if (!cell.CanConstruct || cell.ExistTypeOnCell != ExistTypeOnCell.None)
                continue;
            
            float sqrDistance = Vector3.SqrMagnitude((Vector3)cell.Coord - center);
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestCell = cell;
            }
        }
        return nearestCell;
    }
}
