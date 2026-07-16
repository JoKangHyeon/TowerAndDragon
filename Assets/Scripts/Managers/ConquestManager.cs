using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class ConquestManager : MonoBehaviour
{

    // terrainType이 디폴토인 경우 아예 점령이 안되게 -> 호버 하이라이트도 안되게 수정 필요
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private CycleManager _cycleManager;

    [SerializeField]
    private ConquestDurationTable _durationTable;

    [SerializeField]
    private ConquestChunkCostTable _chunkCostTable;

    [SerializeField]
    private Building _garrisonPrefab;
    public Building GarrisonPrefab => _garrisonPrefab;

    private readonly List<ConquestExpedition> _activeExpeditions = new();
    public IReadOnlyList<ConquestExpedition> ActiveExpeditions => _activeExpeditions;

    private EnemyScalingModifier _accumulatedEnemyScaling = EnemyScalingModifier.Neutral;
    public EnemyScalingModifier AccumulatedEnemyScaling => _accumulatedEnemyScaling;

    public UnityEvent<EnemyScalingModifier> OnEnemyScalingChanged;

    // 점령이 실제로 완료된 시점(며칠 뒤 밤 정산)에 발생 - 보상 지급 등은 이 이벤트를 구독해 처리한다.
    public UnityEvent<Vector2Int> OnConquestCompleted;

    // 원정이 추가되거나(SendExpedition) 진행/완료되었을 때(OnSettlement) 발생 - 진행률 표시 UI가 구독한다.
    public UnityEvent OnExpeditionsChanged;

    private void OnEnable()
    {
        // CycleManager는 Grid.prefab을 쓰는 씬(다른 팀원 테스트 씬 등)에 항상 있는 게 아니므로,
        // 없는 씬에서는 밤 정산 구독만 조용히 건너뛴다.
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.AddListener(OnSettlement);
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.RemoveListener(OnSettlement);
    }

    public bool TryGetExpeditionCost(Vector2Int targetChunkCoord, out ResourceCost cost) =>
        _chunkCostTable.TryResolve(targetChunkCoord, out cost);

    public int GetDaysRequired(Vector2Int chunkCoord)
    {
        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        return _durationTable.ResolveDaysRequired(chunk.DominantTerrain);
    }

    public EnemyScalingModifier PreviewEnemyScaling(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolveEnemyScaling(chunkCoord);

    public int GetPopulationReward(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolvePopulationReward(chunkCoord);

    public ResourceType GetUnlockedResources(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolveUnlockedResources(chunkCoord);

    // 아직 점령 전이라 다른 건물이 들어올 수 없는 청크이므로, 완료 전에도 안정적으로 미리 계산 가능하다.
    public bool TryGetGarrisonPreviewCell(Vector2Int chunkCoord, out Vector3Int cellCoord)
    {
        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        GridCell cell = chunk != null ? FindConstructableCellNearestCenter(chunk) : null;

        if (cell == null)
        {
            cellCoord = default;
            return false;
        }

        cellCoord = cell.Coord;
        return true;
    }

    public TerrainType GetDominantTerrain(Vector2Int chunkCoord) =>
        _gridMap.GetChunk(chunkCoord).DominantTerrain;

    public bool TryGetActiveExpedition(Vector2Int chunkCoord, out ConquestExpedition expedition)
    {
        foreach (ConquestExpedition candidate in _activeExpeditions)
        {
            if (candidate.TargetChunkCoord == chunkCoord)
            {
                expedition = candidate;
                return true;
            }
        }

        expedition = null;
        return false;
    }

    public bool CanSendExpedition(Vector2Int targetChunkCoord)
    {
        Chunk chunk = _gridMap.GetChunk(targetChunkCoord);
        if (chunk == null || chunk.CurrentState != ChunkState.Visible)
            return false;

        if (!HasConqueredOrthogonalNeighbor(targetChunkCoord))
            return false;

        foreach (ConquestExpedition expedition in _activeExpeditions)
        {
            if (expedition.TargetChunkCoord == targetChunkCoord)
                return false;
        }

        return true;
    }

    private bool HasConqueredOrthogonalNeighbor(Vector2Int chunkCoord)
    {
        foreach (Chunk neighbor in _gridMap.GetOrthogonalAdjacentChunks(chunkCoord))
        {
            if (neighbor.CurrentState == ChunkState.Conquered)
                return true;
        }

        return false;
    }

    // available: 원정을 보낼 시점의 보유 자원/인구
    // 실제 보유량 조회는 자원/인구 매니저가 생기면 그쪽에서 채워서 넘기고, 
    // 지금은 호출자가 직접 준비해서 넘김
    public bool CanAffordExpedition(Vector2Int targetChunkCoord, ResourceCost available)
    {
        if (!_chunkCostTable.TryResolve(targetChunkCoord, out ResourceCost cost))
            return false;

        return available.CanAfford(cost);
    }

    public bool SendExpedition(Vector2Int targetChunkCoord, ResourceCost available)
    {
        if (!CanSendExpedition(targetChunkCoord))
            return false;

        if (!_chunkCostTable.TryResolve(targetChunkCoord, out ResourceCost cost))
        {
            Debug.LogWarning($"[ConquestManager] 청크 {targetChunkCoord}의 점령 비용이 설정되지 않았습니다.");
            return false;
        }

        if (!available.CanAfford(cost))
        {
            Debug.LogWarning($"[ConquestManager] 청크 {targetChunkCoord} 원정 실패 - 자원이 부족합니다.");
            return false;
        }

        Chunk chunk = _gridMap.GetChunk(targetChunkCoord);
        int daysRequired = _durationTable.ResolveDaysRequired(chunk.DominantTerrain);

        _activeExpeditions.Add(new ConquestExpedition(targetChunkCoord, cost, daysRequired));
        OnExpeditionsChanged?.Invoke();
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

        OnExpeditionsChanged?.Invoke();
    }

    private void CompleteConquest(ConquestExpedition expedition)
    {
        _gridMap.SetChunkState(expedition.TargetChunkCoord, ChunkState.Conquered);
        ExpandVisibility(expedition.TargetChunkCoord);
        PlaceGarrison(expedition.TargetChunkCoord);
        ApplyEnemyScaling(expedition.TargetChunkCoord);
        OnConquestCompleted?.Invoke(expedition.TargetChunkCoord);
    }

    private void ExpandVisibility(Vector2Int chunkCoord)
    {
        foreach (Chunk neighbor in _gridMap.GetAdjacentChunks(chunkCoord))
        {
            if (neighbor.CurrentState == ChunkState.Hidden)
                _gridMap.SetChunkState(neighbor.ChunkCoord, ChunkState.Visible);
        }
    }

    private void ApplyEnemyScaling(Vector2Int chunkCoord)
    {
        EnemyScalingModifier modifier = _chunkCostTable.ResolveEnemyScaling(chunkCoord);

        _accumulatedEnemyScaling = _accumulatedEnemyScaling.Combine(modifier);
        OnEnemyScalingChanged?.Invoke(_accumulatedEnemyScaling);
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
