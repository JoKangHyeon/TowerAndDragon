using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class ResearchManager : MonoBehaviour,
    IChunkYieldMultiplierQuery,
    ITowerStatMultiplierQuery,
    IConquestModifierQuery,
    IPopulationCapacityModifierQuery,
    IVisionRadiusBonusQuery,
    ITowerMaxHealthMultiplierQuery,
    IChunkResearchUnlockQuery,
    ISealStoneUnlockQuery,
    ITowerCombatRepairUnlockQuery,
    IResearchLabBuildLimitQuery
{
    private const float BASE_YIELD_MULTIPLIER = 1f;
    private const float BASE_DAMAGE_MULTIPLIER = 1f;
    private const float BASE_RANGE_MULTIPLIER = 1f;
    private const float BASE_ATTACK_SPEED_MULTIPLIER = 1f;
    private const float BASE_MAX_HEALTH_MULTIPLIER = 1f;
    private const int MINIMUM_POPULATION_CAPACITY = 1;

    [SerializeField] private ResearchTreeData _tree;
    [SerializeField] private ResearchBalanceData _balance;

    // 소비 지점(GridMap/TowerAttack)에 이 매니저를 직접 대입하지 않고 Composite에 등록한다 -
    // 용 스킬트리 등 다른 시스템도 같은 지점에 기여할 수 있어야 하기 때문이다
    // (단일 슬롯이면 서로 덮어쓴다). 시야·점령 쪽 등록은 각각 CastleVisionCoordinator·
    // ConquestResearchCoordinator가 담당한다(이 매니저를 이미 참조하고 있어 중복 배선이 없다).
    [SerializeField] private ChunkYieldMultiplierComposite _yieldComposite;
    [SerializeField] private TowerStatMultiplierComposite _statComposite;

    [Tooltip("타워 최대체력 배율 Composite. 비어 있으면 최대체력 연구(중갑 타워)가 적용되지 않는다 - " +
        "소비자가 TowerAttack이 아니라 TowerMaxHealthApplier라서 별도 Composite를 쓴다.")]
    [SerializeField] private TowerMaxHealthMultiplierComposite _maxHealthComposite;

    [SerializeField] private UnityEvent<int> _researchPointsChanged = new();
    [SerializeField] private UnityEvent<ResearchNodeData> _nodeCompleted = new();
    [SerializeField] private UnityEvent<ResearchLab> _activeLabChanged = new();

    private readonly Dictionary<string, ResearchNodeData> _nodesById = new();
    private readonly HashSet<string> _completedNodeIds = new();
    private readonly List<ResearchLab> _activeLabs = new();

    private CycleManager _cycleManager;
    private ResourceManager _resourceManager;
    private GridMap _gridMap;
    private WaveCycleProgression _cycleProgression;
    private int _researchPoints;
    private bool _isConstructed;

    public int ResearchPoints => _researchPoints;

    /// <summary>
    /// 랜드마크 보유 조회 슬롯. LandmarkResearchCoordinator가 대입한다
    /// (GridMap.YieldMultiplierQuery와 같은 관용구).
    ///
    /// 이 슬롯이 비어 있어도 <b>랜드마크 조건이 없는 노드는 그대로 열린다</b> - 랜드마크를
    /// 배치하지 않은 씬(튜토리얼·테스트)의 기존 연구는 영향을 받지 않는다.
    /// 반대로 조건이 걸린 노드는 슬롯이 비어 있으면 잠긴 것으로 본다(fail-closed).
    /// 배선을 빠뜨린 채 역설계 노드가 공짜로 열리는 것보다, 잠겨 있어 눈에 띄는 편이 낫다.
    /// </summary>
    public ILandmarkOwnershipQuery LandmarkOwnershipQuery { get; set; }

    /// <summary>세이브 캡처용 완료 노드 집합. DragonTreeManager.UnlockedIds와 대칭이다.</summary>
    public IReadOnlyCollection<string> CompletedNodeIds => _completedNodeIds;

    /// <summary>
    /// 티어 잠금 판정에 쓰는 현재 주기. 주기 진행 컴포넌트가 배선되지 않은 씬에서는
    /// 첫 주기로 간주해 T1만 열어 둔다(기존 동작과 동일).
    /// </summary>
    public int CurrentCycleNumber =>
        _cycleProgression != null
            ? _cycleProgression.CurrentCycleNumber
            : WaveCycleRules.FIRST_CYCLE_NUMBER;
    public ResearchLab ActiveLab => _activeLabs.Count > 0 ? _activeLabs[0] : null;
    public bool HasActiveLab => _activeLabs.Count > 0;
    public ResearchTreeData Tree => _tree;
    public UnityEvent<int> ResearchPointsChanged => _researchPointsChanged;
    public UnityEvent<ResearchNodeData> NodeCompleted => _nodeCompleted;
    public UnityEvent<ResearchLab> ActiveLabChanged => _activeLabChanged;

    public void Construct(
        CycleManager cycleManager,
        ResourceManager resourceManager,
        GridMap gridMap,
        WaveCycleProgression cycleProgression)
    {
        if (_isConstructed)
        {
            return;
        }

        _cycleManager = cycleManager;
        _resourceManager = resourceManager;
        _gridMap = gridMap;
        _cycleProgression = cycleProgression;

        if (_cycleProgression == null)
        {
            Debug.LogError(
                "[ResearchManager] WaveCycleProgression 참조가 없어 주기가 진행돼도 T1만 열립니다.",
                this);
        }

        CacheNodes();

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.AddListener(GrantResearchPoints);
        }

        if (_yieldComposite == null)
        {
            Debug.LogError(
                "[ResearchManager] ChunkYieldMultiplierComposite 참조가 없어 연구 생산 배율이 전혀 적용되지 않습니다.",
                this);
        }

        if (_statComposite == null)
        {
            Debug.LogError(
                "[ResearchManager] TowerStatMultiplierComposite 참조가 없어 연구 타워 배율이 전혀 적용되지 않습니다.",
                this);
        }

        _yieldComposite?.Register(this);
        _statComposite?.Register(this);
        _maxHealthComposite?.Register(this);

        if (_gridMap != null)
        {
            _gridMap.ResearchUnlockQuery = this;
            _gridMap.ResearchLabBuildLimitQuery = this;
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAddedForTowerCombatRepair);
            AssignTowerCombatRepairUnlockQueryToExistingTowers();
        }

        _isConstructed = true;
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(GrantResearchPoints);
        }

        _yieldComposite?.Unregister(this);
        _statComposite?.Unregister(this);
        _maxHealthComposite?.Unregister(this);

        if (_gridMap != null && ReferenceEquals(_gridMap.ResearchUnlockQuery, this))
        {
            _gridMap.ResearchUnlockQuery = null;
        }

        if (_gridMap != null && ReferenceEquals(_gridMap.ResearchLabBuildLimitQuery, this))
        {
            _gridMap.ResearchLabBuildLimitQuery = null;
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAddedForTowerCombatRepair);
        }
    }

    public bool RegisterLab(ResearchLab lab)
    {
        if (lab == null || _activeLabs.Contains(lab))
        {
            return false;
        }

        bool hadActiveLab = HasActiveLab;
        _activeLabs.Add(lab);

        if (!hadActiveLab)
        {
            _activeLabChanged.Invoke(ActiveLab);
        }

        return true;
    }

    public bool UnregisterLab(ResearchLab lab)
    {
        if (lab == null)
        {
            return false;
        }

        ResearchLab previousActiveLab = ActiveLab;
        bool removed = _activeLabs.Remove(lab);

        if (removed && previousActiveLab != ActiveLab)
        {
            _activeLabChanged.Invoke(ActiveLab);
        }

        return removed;
    }

    public bool IsCompleted(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _completedNodeIds.Contains(nodeId);
    }

    public ResearchNodeState GetNodeState(ResearchNodeData node)
    {
        if (!IsRegisteredNode(node))
        {
            return ResearchNodeState.Invalid;
        }

        if (IsCompleted(node.NodeId))
        {
            return ResearchNodeState.Completed;
        }

        if (_cycleManager == null || _cycleManager.CurrentCycle != CycleManager.CycleState.Day)
        {
            return ResearchNodeState.UnavailablePhase;
        }

        if (!ResearchTierRules.IsTierUnlocked(node.Tier, CurrentCycleNumber))
        {
            return ResearchNodeState.TierLocked;
        }

        if (!IsRequiredLandmarkClaimed(node))
        {
            return ResearchNodeState.LandmarkLocked;
        }

        foreach (ResearchNodeData prerequisite in node.Prerequisites)
        {
            if (prerequisite == null || !IsCompleted(prerequisite.NodeId))
            {
                return ResearchNodeState.PrerequisiteLocked;
            }
        }

        if (_researchPoints < node.ResearchPointCost)
        {
            return ResearchNodeState.InsufficientResearchPoints;
        }

        if (_resourceManager == null || !_resourceManager.CanAfford(node.ResourceCost))
        {
            return ResearchNodeState.InsufficientResources;
        }

        return ResearchNodeState.Available;
    }

    /// <summary>
    /// 이 타워를 건설 메뉴에 노출해도 되는지. 해금이 필요 없는 기본 타워는 항상 true다.
    /// 해금 상태는 완료 노드에서 매번 파생시킨다 - 별도 집합을 캐시해두면 세이브 복원
    /// (RestoreProgress)이나 노드 완료 때마다 동기화를 잊을 여지가 생긴다.
    /// </summary>
    public bool IsTowerUnlocked(TowerData towerData)
    {
        if (towerData == null || !towerData.RequiresResearchUnlock)
        {
            return true;
        }

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null && effect.GetUnlockedTower() == towerData)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 랜드마크 조건이 없는 노드(대부분)는 항상 통과한다. 조건이 있는데 조회 슬롯이 비어 있으면
    // 판정할 방법이 없으므로 잠긴 것으로 본다 - 배선을 빠뜨린 채 역설계 노드가 열리는 편보다
    // 닫혀 있는 편이 눈에 띄어 고치기 쉽다.
    private bool IsRequiredLandmarkClaimed(ResearchNodeData node)
    {
        LandmarkDataSO requiredLandmark = node.RequiredLandmark;

        if (requiredLandmark == null)
        {
            return true;
        }

        return LandmarkOwnershipQuery != null &&
            LandmarkOwnershipQuery.IsClaimed(requiredLandmark.LandmarkId);
    }

    public bool TryResearch(
        ResearchNodeData node,
        out ResearchFailureReason failureReason)
    {
        ResearchNodeState state = GetNodeState(node);
        failureReason = ToFailureReason(state);

        if (state != ResearchNodeState.Available)
        {
            return false;
        }

        _resourceManager.Spend(node.ResourceCost);
        _researchPoints -= node.ResearchPointCost;
        _completedNodeIds.Add(node.NodeId);

        _researchPointsChanged.Invoke(_researchPoints);
        _nodeCompleted.Invoke(node);
        failureReason = ResearchFailureReason.None;
        return true;
    }

    /// <summary>
    /// 세이브 복원 전용. RP와 완료 노드 집합을 저장값으로 갈아 끼운다.
    /// TryResearch 경로를 타지 않으므로 자원과 RP가 다시 차감되지 않는다.
    ///
    /// 연구 효과는 전부 pull 방식(GetYieldMultiplier 등이 호출 시점에 _completedNodeIds를 순회)이라
    /// 집합만 복원하면 자동으로 살아난다. NodeCompleted를 재발화하는 것은 push 방식 구독자
    /// (성 시야 확장, 인구 정원 재조정, UI 갱신)를 위해서이며 이들은 전부 멱등하다.
    /// </summary>
    public void RestoreProgress(int researchPoints, IReadOnlyList<string> completedNodeIds)
    {
        _researchPoints = Mathf.Max(0, researchPoints);
        _completedNodeIds.Clear();

        foreach (string nodeId in completedNodeIds)
        {
            // 트리에 없는 id(밸런싱으로 삭제된 노드)는 버린다 - 남기면 IsCompleted는 true인데
            // 효과 계산에서는 매번 조회에 실패하는 어긋난 상태가 된다.
            if (string.IsNullOrWhiteSpace(nodeId) || !_nodesById.ContainsKey(nodeId))
            {
                Debug.LogWarning($"[ResearchManager] 트리에 없는 연구 노드 ID를 건너뜁니다: {nodeId}");
                continue;
            }

            _completedNodeIds.Add(nodeId);
        }

        _researchPointsChanged.Invoke(_researchPoints);

        // 구독자가 완료 집합을 건드려도 순회가 깨지지 않도록 복사본을 돌린다.
        foreach (string nodeId in new List<string>(_completedNodeIds))
        {
            _nodeCompleted.Invoke(_nodesById[nodeId]);
        }
    }

    public float GetYieldMultiplier(
        Vector2Int chunkCoord,
        ResourceType resourceType)
    {
        float bonusRatio = 0f;
        Chunk chunk = _gridMap != null ? _gridMap.GetChunk(chunkCoord) : null;
        TerrainType terrainType = chunk != null
            ? chunk.DominantTerrain
            : TerrainType.Default;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
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

    public bool IsUnlocked(Vector2Int chunkCoord, ResourceType resourceType)
    {
        Chunk chunk = _gridMap != null ? _gridMap.GetChunk(chunkCoord) : null;
        TerrainType terrainType = chunk != null
            ? chunk.DominantTerrain
            : TerrainType.Default;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null &&
                    effect.UnlocksResourceNode(chunkCoord, terrainType, resourceType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsTowerCombatRepairUnlocked
    {
        get
        {
            foreach (string nodeId in _completedNodeIds)
            {
                if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
                {
                    continue;
                }

                foreach (ResearchEffectSO effect in node.Effects)
                {
                    if (effect != null && effect.UnlocksTowerCombatRepair())
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public float GetDamageMultiplier(TowerData towerData)
    {
        float bonusRatio = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    bonusRatio += effect.GetTowerDamageMultiplierBonus(towerData);
                }
            }
        }

        return BASE_DAMAGE_MULTIPLIER + bonusRatio;
    }

    public float GetRangeMultiplier(TowerData towerData)
    {
        float bonusRatio = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    bonusRatio += effect.GetTowerRangeMultiplierBonus(towerData);
                }
            }
        }

        return BASE_RANGE_MULTIPLIER + bonusRatio;
    }

    public float GetAttackSpeedMultiplier(TowerData towerData)
    {
        float bonusRatio = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    bonusRatio += effect.GetTowerAttackSpeedMultiplierBonus(towerData);
                }
            }
        }

        return BASE_ATTACK_SPEED_MULTIPLIER + bonusRatio;
    }

    // --- ITowerMaxHealthMultiplierQuery ---

    public float GetMaxHealthMultiplier(TowerData towerData)
    {
        float bonusRatio = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    bonusRatio += effect.GetTowerMaxHealthMultiplierBonus(towerData);
                }
            }
        }

        return BASE_MAX_HEALTH_MULTIPLIER + bonusRatio;
    }

    public float GetConquestCostReductionRatio()
    {
        float reductionRatio = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    reductionRatio += effect.GetConquestCostReductionRatio();
                }
            }
        }

        return reductionRatio;
    }

    public int GetConquestDaysReduction()
    {
        int daysReduction = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    daysReduction += effect.GetConquestDaysReduction();
                }
            }
        }

        return daysReduction;
    }

    public int GetConquestPopulationReduction()
    {
        int populationReduction = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    populationReduction += effect.GetConquestPopulationReduction();
                }
            }
        }

        return populationReduction;
    }

    public float GetCastleDailyRegenAmount()
    {
        float regenAmount = 0f;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    regenAmount += effect.GetCastleDailyRegenAmount();
                }
            }
        }

        return regenAmount;
    }

    public int ResolveCapacity(
        PopulationAssignmentType assignmentType,
        int baseCapacity,
        ResourceType producedResources)
    {
        int capacityDelta = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    capacityDelta += effect.GetPopulationCapacityDelta(
                        assignmentType, producedResources);
                }
            }
        }

        return Mathf.Max(MINIMUM_POPULATION_CAPACITY, baseCapacity + capacityDelta);
    }

    public int GetVisionRadiusBonus()
    {
        int radiusBonus = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    radiusBonus += effect.GetVisionRadiusBonus();
                }
            }
        }

        return radiusBonus;
    }

    public int GetMoveAllowance()
    {
        int moveAllowance = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    moveAllowance += effect.GetMoveAllowanceBonus();
                }
            }
        }

        return moveAllowance;
    }

    public int GetResearchLabBuildLimitBonus()
    {
        int buildLimitBonus = 0;

        foreach (string nodeId in _completedNodeIds)
        {
            if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
            {
                continue;
            }

            foreach (ResearchEffectSO effect in node.Effects)
            {
                if (effect != null)
                {
                    buildLimitBonus += effect.GetResearchLabBuildLimitBonus();
                }
            }
        }

        return buildLimitBonus;
    }

    // --- ISealStoneUnlockQuery ---
    // 봉인석은 승리 조건(4포탈 동시 봉인)에 직결되므로 한 노드라도 해금하면 열린다.
    public bool IsSealStoneUnlocked
    {
        get
        {
            foreach (string nodeId in _completedNodeIds)
            {
                if (!_nodesById.TryGetValue(nodeId, out ResearchNodeData node))
                {
                    continue;
                }

                foreach (ResearchEffectSO effect in node.Effects)
                {
                    if (effect != null && effect.UnlocksSealStone())
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private void CacheNodes()
    {
        _nodesById.Clear();

        if (!WiringGuard.Require(_tree, nameof(_tree), this))
        {
            return;
        }

        foreach (ResearchNodeData node in _tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            if (!_nodesById.TryAdd(node.NodeId, node))
            {
                Debug.LogError(
                    $"[ResearchManager] 중복 연구 노드 ID: {node.NodeId}",
                    node);
            }
        }
    }

    private void AssignTowerCombatRepairUnlockQueryToExistingTowers()
    {
        foreach (Building building in _gridMap.Buildings)
        {
            HandleBuildingAddedForTowerCombatRepair(building);
        }
    }

    private void HandleBuildingAddedForTowerCombatRepair(Building building)
    {
        if (building is Tower tower)
        {
            tower.SetCombatRepairUnlockQuery(this);
        }
    }

    // 정산 시 이 연구소가 받게 될 RP. 등록되지 않은 연구소면 인구를 배치해도 0이다
    // (UI가 그 사실을 그대로 보여줄 수 있게 public).
    public int PreviewResearchPointsPerDay(ResearchLab lab, int assignedPopulation)
    {
        if (lab == null || !_activeLabs.Contains(lab))
        {
            return 0;
        }

        if (!WiringGuard.Require(_balance, nameof(_balance), this))
        {
            return 0;
        }

        return assignedPopulation * _balance.ResearchPointsPerPopulation;
    }

    /// <summary>
    /// 연구 점수를 직접 더한다. 튜토리얼처럼 "지금 반드시 연구가 되어야 하는" 상황에서만 쓴다 -
    /// 평소 획득 경로는 연구소에 배치한 인구(GrantResearchPoints)뿐이다.
    /// </summary>
    public void AddResearchPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _researchPoints += amount;
        _researchPointsChanged.Invoke(_researchPoints);
    }

    private void GrantResearchPoints(int currentDay)
    {
        int gained = 0;
        foreach (ResearchLab lab in _activeLabs)
        {
            if (lab == null || lab.Population == null)
            {
                continue;
            }

            gained += PreviewResearchPointsPerDay(
                lab,
                lab.Population.AssignedPopulation);
        }

        if (gained <= 0)
        {
            return;
        }

        _researchPoints += gained;
        _researchPointsChanged.Invoke(_researchPoints);
    }

    private bool IsRegisteredNode(ResearchNodeData node)
    {
        return node != null &&
            !string.IsNullOrWhiteSpace(node.NodeId) &&
            _nodesById.TryGetValue(node.NodeId, out ResearchNodeData registered) &&
            registered == node;
    }

    private static ResearchFailureReason ToFailureReason(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Invalid => ResearchFailureReason.InvalidNode,
            ResearchNodeState.Completed => ResearchFailureReason.AlreadyCompleted,
            ResearchNodeState.UnavailablePhase => ResearchFailureReason.UnavailablePhase,
            ResearchNodeState.TierLocked => ResearchFailureReason.TierLocked,
            ResearchNodeState.PrerequisiteLocked => ResearchFailureReason.PrerequisiteLocked,
            ResearchNodeState.InsufficientResearchPoints => ResearchFailureReason.InsufficientResearchPoints,
            ResearchNodeState.InsufficientResources => ResearchFailureReason.InsufficientResources,
            ResearchNodeState.LandmarkLocked => ResearchFailureReason.LandmarkLocked,
            _ => ResearchFailureReason.None,
        };
    }
}
