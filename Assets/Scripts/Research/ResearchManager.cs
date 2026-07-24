using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class ResearchManager : MonoBehaviour,
    IChunkYieldMultiplierQuery,
    ITowerDamageMultiplierQuery
{
    private const int FIRST_TIER = 1;
    private const float BASE_YIELD_MULTIPLIER = 1f;
    private const float BASE_DAMAGE_MULTIPLIER = 1f;

    [SerializeField] private ResearchTreeData _tree;
    [SerializeField] private ResearchBalanceData _balance;

    [SerializeField] private UnityEvent<int> _researchPointsChanged = new();
    [SerializeField] private UnityEvent<ResearchNodeData> _nodeCompleted = new();
    [SerializeField] private UnityEvent<ResearchLab> _activeLabChanged = new();

    private readonly Dictionary<string, ResearchNodeData> _nodesById = new();
    private readonly HashSet<string> _completedNodeIds = new();

    private CycleManager _cycleManager;
    private ResourceManager _resourceManager;
    private GridMap _gridMap;
    private int _researchPoints;
    private bool _isConstructed;

    public int ResearchPoints => _researchPoints;
    public ResearchLab ActiveLab { get; private set; }
    public bool HasActiveLab => ActiveLab != null;
    public ResearchTreeData Tree => _tree;
    public UnityEvent<int> ResearchPointsChanged => _researchPointsChanged;
    public UnityEvent<ResearchNodeData> NodeCompleted => _nodeCompleted;
    public UnityEvent<ResearchLab> ActiveLabChanged => _activeLabChanged;

    public void Construct(
        CycleManager cycleManager,
        ResourceManager resourceManager,
        GridMap gridMap)
    {
        if (_isConstructed)
        {
            return;
        }

        _cycleManager = cycleManager;
        _resourceManager = resourceManager;
        _gridMap = gridMap;

        CacheNodes();

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.AddListener(GrantResearchPoints);
        }

        if (_gridMap != null)
        {
            _gridMap.YieldMultiplierQuery = this;
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }

        _isConstructed = true;
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(GrantResearchPoints);
        }

        if (_gridMap != null && ReferenceEquals(_gridMap.YieldMultiplierQuery, this))
        {
            _gridMap.YieldMultiplierQuery = null;
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
        }
    }

    public bool RegisterLab(ResearchLab lab)
    {
        if (lab == null || ActiveLab != null)
        {
            return false;
        }

        ActiveLab = lab;
        _activeLabChanged.Invoke(ActiveLab);
        return true;
    }

    public bool UnregisterLab(ResearchLab lab)
    {
        if (lab == null || ActiveLab != lab)
        {
            return false;
        }

        ActiveLab = null;
        _activeLabChanged.Invoke(null);
        return true;
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

        if (node.Tier != FIRST_TIER)
        {
            return ResearchNodeState.TierLocked;
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

    private void HandleBuildingAdded(Building building)
    {
        if (building is Tower tower && tower.Attack != null)
        {
            tower.Attack.SetDamageMultiplierQuery(this);
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is Tower tower && tower.Attack != null)
        {
            tower.Attack.SetDamageMultiplierQuery(null);
        }
    }

    private void CacheNodes()
    {
        _nodesById.Clear();

        if (_tree == null)
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

    private void GrantResearchPoints(int currentDay)
    {
        if (ActiveLab == null || ActiveLab.Population == null || _balance == null)
        {
            return;
        }

        int gained =
            ActiveLab.Population.AssignedPopulation *
            _balance.ResearchPointsPerPopulation;

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
            _ => ResearchFailureReason.None,
        };
    }
}
