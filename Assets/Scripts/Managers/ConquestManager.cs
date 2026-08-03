using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class ConquestManager : MonoBehaviour
{
    private const int MINIMUM_DAYS_REQUIRED = 1;

    // 코디네이터(ConquestResearchCoordinator)가 배선한다 - 배선되지 않은 씬에서는 null로 남아
    // 할인·기간감소가 적용되지 않는다(기존 동작 유지).
    public IConquestModifierQuery ResearchModifierQuery { get; set; }

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private CycleManager _cycleManager;

    [SerializeField]
    private EnemyEnhancementManager _enemyEnhancementManager;

    [SerializeField]
    private ConquestDurationTable _durationTable;

    [SerializeField]
    private ConquestChunkCostTable _chunkCostTable;

    private readonly List<ConquestExpedition> _activeExpeditions = new();
    public IReadOnlyList<ConquestExpedition> ActiveExpeditions => _activeExpeditions;


    // 점령이 실제로 완료된 시점(며칠 뒤 밤 정산)에 발생 - 보상 지급 등은 이 이벤트를 구독해 처리한다.
    public UnityEvent<Vector2Int> OnConquestCompleted;

    // 원정이 발송된 시점(SendExpedition 성공 직후)에 발생 - 원정 인구 비용 배치 등은 이 이벤트를 구독해 처리한다.
    // cost는 SendExpedition이 이미 조회해 둔 값을 그대로 실어보낸다 - 구독자가 ConquestChunkCostTable을 다시 조회할 필요가 없다.
    public UnityEvent<Vector2Int, ResourceCost> OnExpeditionSent;

    // 원정이 추가되거나 진행되고, 밤 종료 후 점령이 확정될 때 발생 - 진행률 표시 UI가 구독한다.
    public UnityEvent OnExpeditionsChanged;

    private void OnEnable()
    {
        // CycleManager는 Grid.prefab을 쓰는 씬(다른 팀원 테스트 씬 등)에 항상 있는 게 아니므로,
        // 없는 씬에서는 밤 종료 정산 구독을 조용히 건너뛴다.
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.AddListener(OnSettlement);
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
            _cycleManager.OnNightEnd.RemoveListener(OnSettlement);
    }

    // 표시(UI 미리보기)와 실제 차감(SendExpedition)이 반드시 같은 값을 보도록,
    // 원정 비용을 조회하는 모든 지점이 이 메서드를 거친다.
    public bool TryGetExpeditionCost(Vector2Int targetChunkCoord, out ResourceCost cost)
    {
        if (!_chunkCostTable.TryResolve(targetChunkCoord, out ResourceCost baseCost))
        {
            cost = default;
            return false;
        }

        cost = ApplyCostReduction(baseCost);
        return true;
    }

    // 코스트 테이블에 등록된 청크인지 - UI(호버/하이라이트/선택 가능 여부) 판정의 단일 기준.
    // 대표 지형(DominantTerrain)만으로는 물이 대부분이고 육지가 1칸 끼어 우연히 Default가 아니게 된
    // 모서리 청크를 걸러내지 못하므로, 실제 등록 여부로 판정한다.
    public bool HasExpeditionCost(Vector2Int chunkCoord) => _chunkCostTable.TryResolve(chunkCoord, out _);

    public int GetDaysRequired(Vector2Int chunkCoord)
    {
        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        return ResolveDaysRequired(_durationTable.ResolveDaysRequired(chunk.DominantTerrain));
    }

    private ResourceCost ApplyCostReduction(ResourceCost baseCost)
    {
        float reductionRatio = Mathf.Clamp01(
            ResearchModifierQuery?.GetConquestCostReductionRatio() ?? 0f);

        return new ResourceCost
        {
            Population = baseCost.Population,
            Food = ReduceAmount(baseCost.Food, reductionRatio),
            Wood = ReduceAmount(baseCost.Wood, reductionRatio),
            Stone = ReduceAmount(baseCost.Stone, reductionRatio),
        };
    }

    private static int ReduceAmount(int amount, float reductionRatio) =>
        Mathf.Max(0, Mathf.RoundToInt(amount * (1f - reductionRatio)));

    private int ResolveDaysRequired(int baseDaysRequired)
    {
        int daysReduction = ResearchModifierQuery?.GetConquestDaysReduction() ?? 0;
        return Mathf.Max(MINIMUM_DAYS_REQUIRED, baseDaysRequired - daysReduction);
    }

    public EnemyEnhancementProfileSO PreviewEnemyEnhancementProfile(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolveEnemyEnhancementProfile(chunkCoord);

    public int GetPopulationReward(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolvePopulationReward(chunkCoord);

    public ResourceType GetUnlockedResources(Vector2Int chunkCoord) =>
        _chunkCostTable.ResolveUnlockedResources(chunkCoord);

    public TerrainType GetDominantTerrain(Vector2Int chunkCoord) =>
        _gridMap.GetChunk(chunkCoord).DominantTerrain;

    // 이 청크를 점령하면 나오는 원시 생산력 합계(자원별 배율·연구 강화 미포함) - 점령 UI 미리보기 표시 전용.
    public int GetChunkYield(Vector2Int chunkCoord) =>
        _gridMap.GetChunkBaseYield(chunkCoord);

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
        if (!TryGetExpeditionCost(targetChunkCoord, out ResourceCost cost))
            return false;

        return available.CanAfford(cost);
    }

    public bool SendExpedition(Vector2Int targetChunkCoord, ResourceCost available)
    {
        if (!CanSendExpedition(targetChunkCoord))
            return false;

        if (!TryGetExpeditionCost(targetChunkCoord, out ResourceCost cost))
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
        int daysRequired = ResolveDaysRequired(_durationTable.ResolveDaysRequired(chunk.DominantTerrain));

        _activeExpeditions.Add(new ConquestExpedition(targetChunkCoord, cost, daysRequired));
        OnExpeditionSent?.Invoke(targetChunkCoord, cost);
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

            CompleteConquest(expedition.TargetChunkCoord);
            _activeExpeditions.RemoveAt(i);
        }

        OnExpeditionsChanged?.Invoke();
    }

    private void CompleteConquest(Vector2Int targetChunkCoord)
    {
        _gridMap.SetChunkState(targetChunkCoord, ChunkState.Conquered);
        ExpandVisibility(targetChunkCoord);
        AnnexUnregisteredLandNeighbors(targetChunkCoord);
        ApplyEnemyEnhancement(targetChunkCoord);
        OnConquestCompleted?.Invoke(targetChunkCoord);
    }

    // [테스트 전용] 며칠 대기 없이 진행 중인 모든 원정을 즉시 완료 처리한다.
    public void DebugForceCompleteAllExpeditions()
    {
        for (int i = _activeExpeditions.Count - 1; i >= 0; i--)
        {
            CompleteConquest(_activeExpeditions[i].TargetChunkCoord);
            _activeExpeditions.RemoveAt(i);
        }

        OnExpeditionsChanged?.Invoke();
    }

    // [테스트 전용] 원정 진행 여부와 무관하게 맵의 모든 청크를 즉시 점령 완료 상태로 만든다.
    // 청크마다 SetChunkState를 따로 호출하면 청크 테두리 렌더러 등 OnChunkStateChanged 구독자가
    // 매번 맵 전체를 다시 계산해 청크 수가 많을 때 프레임이 멈추므로, 상태 전환은 한 번에 몰아서 처리한다.
    public void DebugForceConquerAllChunks()
    {
        _activeExpeditions.Clear();

        var chunksToConquer = new List<Vector2Int>();
        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            if (chunk.CurrentState != ChunkState.Conquered)
                chunksToConquer.Add(chunk.ChunkCoord);
        }

        if (chunksToConquer.Count == 0)
            return;

        _gridMap.SetChunkStatesBulk(chunksToConquer, ChunkState.Conquered);

        foreach (Vector2Int chunkCoord in chunksToConquer)
        {
            ExpandVisibility(chunkCoord);
            ApplyEnemyEnhancement(chunkCoord);
            OnConquestCompleted?.Invoke(chunkCoord);
        }

        OnExpeditionsChanged?.Invoke();
    }

    private void ExpandVisibility(Vector2Int chunkCoord)
    {
        foreach (Chunk neighbor in _gridMap.GetAdjacentChunks(chunkCoord))
        {
            if (neighbor.CurrentState == ChunkState.Hidden)
                _gridMap.SetChunkState(neighbor.ChunkCoord, ChunkState.Visible);
        }
    }

    private static readonly Vector3Int[] CELL_ORTHOGONAL_DIRECTIONS =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
    };

    // 청크 네 개가 만나는 꼭짓점에서는 육지 셀이 단 1칸만 우연히 맞닿는 경우가 있다(예: 대각선
    // 청크는 바다로 막혀 있는데 꼭짓점 셀 한 칸만 같은 육지 타입인 경우) - 이런 우연의 일치를
    // "연결됨"으로 치지 않도록 최소 접촉 셀 수를 요구한다.
    private const int MINIMUM_LAND_BORDER_CONTACT_CELLS = 2;

    // 코스트 테이블에 등록되지 않았지만(=독자 점령 불가) 땅이 남아있는 모서리 청크를,
    // 인접한 실제 점령 청크가 점령 완료되는 시점에 그 영토로 편입한다.
    // 편입은 SetChunkState만 호출한다 - 코스트 테이블에 데이터가 없으므로 인구 보상/적강화가
    // 애초에 없고, OnConquestCompleted도 발행하지 않아 원정 기반 리스너(인구 보상 코디네이터 등)에
    // 부작용이 없다. 영토 테두리 렌더러는 ChunkState.Conquered 여부만 보므로 자동으로 반영된다.
    private void AnnexUnregisteredLandNeighbors(Vector2Int chunkCoord)
    {
        CollectAnnexableNeighbors(chunkCoord, _annexBuffer);

        foreach (Vector2Int neighborCoord in _annexBuffer)
        {
            _gridMap.SetChunkState(neighborCoord, ChunkState.Conquered);
        }
    }

    private readonly List<Vector2Int> _annexBuffer = new();

    // 이 청크를 점령했을 때 함께 편입될 짜투리 청크 좌표를 result에 채운다 - 상태를 바꾸지 않는 조회 전용.
    // 점령 모드의 편입 미리보기 하이라이트와 실제 편입이 같은 판정을 공유하게 하기 위해 분리했다.
    // 판정이 이웃의 Conquered 여부와 불변 데이터(LandCellCoords)만 보므로, 점령 전에 미리 계산한
    // 결과와 점령 완료 시점에 계산한 결과가 일치한다.
    public void CollectAnnexableNeighbors(Vector2Int chunkCoord, List<Vector2Int> result)
    {
        result.Clear();

        Chunk sourceChunk = _gridMap.GetChunk(chunkCoord);
        if (sourceChunk == null)
            return;

        foreach (Chunk neighbor in _gridMap.GetOrthogonalAdjacentChunks(chunkCoord))
        {
            if (neighbor.CurrentState == ChunkState.Conquered)
                continue;

            if (HasExpeditionCost(neighbor.ChunkCoord))
                continue;

            if (!IsPrimaryLandConnection(neighbor, sourceChunk))
                continue;

            result.Add(neighbor.ChunkCoord);
        }
    }

    // scrapChunk 입장에서 conqueredChunk가 "가장 많이 맞닿아 있는" 이웃일 때만 true를 반환한다.
    // 청크 경계가 실제로는 대부분 물일 수 있으므로 셀 단위 접촉 수를 기준으로 삼고, scrapChunk의
    // 다른 이웃(아직 미점령)이 더 넓게 맞닿아 있다면 이번 점령으로는 편입하지 않고 보류한다 -
    // 그래야 나중에 진짜 주 접경 청크를 점령했을 때 자연스럽게 편입된다.
    private bool IsPrimaryLandConnection(Chunk scrapChunk, Chunk conqueredChunk)
    {
        int conqueredContact = CountLandBorderContact(scrapChunk, conqueredChunk);
        if (conqueredContact < MINIMUM_LAND_BORDER_CONTACT_CELLS)
            return false;

        foreach (Chunk otherNeighbor in _gridMap.GetOrthogonalAdjacentChunks(scrapChunk.ChunkCoord))
        {
            if (otherNeighbor.ChunkCoord == conqueredChunk.ChunkCoord)
                continue;

            if (CountLandBorderContact(scrapChunk, otherNeighbor) > conqueredContact)
                return false;
        }

        return true;
    }

    // chunkA의 육지 셀(Chunk가 생성 시점에 캐싱해 둔 LandCellCoords) 중 chunkB의 육지 셀과
    // 상하좌우로 맞닿아 있는 셀의 개수 - 청크 경계선이 아니라 실제 지형 경계(접촉 폭)를 기준으로 판정한다.
    private static int CountLandBorderContact(Chunk chunkA, Chunk chunkB)
    {
        int contactCount = 0;
        foreach (Vector3Int cellCoord in chunkA.LandCellCoords)
        {
            foreach (Vector3Int direction in CELL_ORTHOGONAL_DIRECTIONS)
            {
                if (chunkB.ContainsLandCell(cellCoord + direction))
                {
                    contactCount++;
                    break;
                }
            }
        }

        return contactCount;
    }

    private void ApplyEnemyEnhancement(Vector2Int chunkCoord)
    {
        EnemyEnhancementProfileSO profile =
            _chunkCostTable.ResolveEnemyEnhancementProfile(chunkCoord);

        if (profile == null)
        {
            return;
        }

        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        if (chunk == null)
        {
            return;
        }

        if (_enemyEnhancementManager == null)
        {
            Debug.LogError("[ConquestManager] EnemyEnhancementManager 참조가 없습니다.", this);
            return;
        }

        _enemyEnhancementManager.ApplyProfile(
            chunk.DominantTerrain,
            profile);
    }

}
