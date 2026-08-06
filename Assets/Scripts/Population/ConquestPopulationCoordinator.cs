using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ConquestManager의 원정 발송/완료 이벤트를 받아 PopulationManager에 반영한다.
/// 원정 발송 시 비용만큼 인구를 배치(Conquest 배정)하고, 완료 시 그 배치를 반환하며 인구 보상을 지급한다.
/// ConquestManager와 PopulationManager가 서로를 직접 참조하지 않도록 중간에서 조정한다.
/// </summary>
public class ConquestPopulationCoordinator : MonoBehaviour
{
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private PopulationManager _populationManager;

    // 청크당 원정은 동시에 하나만 존재하므로(ConquestManager.CanSendExpedition), 청크 좌표로 배치를 식별한다.
    private readonly Dictionary<Vector2Int, PopulationAllocation> _expeditionAllocations = new();

    private void OnEnable()
    {
        if (!WiringGuard.Require(_conquestManager, nameof(_conquestManager), this))
        {
            return;
        }

        _conquestManager.OnExpeditionSent.AddListener(HandleExpeditionSent);
        _conquestManager.OnConquestCompleted.AddListener(HandleConquestCompleted);
    }

    private void OnDisable()
    {
        if (_conquestManager == null)
        {
            return;
        }

        _conquestManager.OnExpeditionSent.RemoveListener(HandleExpeditionSent);
        _conquestManager.OnConquestCompleted.RemoveListener(HandleConquestCompleted);
    }

    private void HandleExpeditionSent(Vector2Int chunkCoord, ResourceCost cost)
    {
        if (!WiringGuard.Require(_populationManager, nameof(_populationManager), this))
        {
            return;
        }

        if (cost.Population <= 0)
        {
            return;
        }

        bool reserved =
            _populationManager.TryCreateAllocation(PopulationAssignmentType.Conquest, cost.Population, out PopulationAllocation allocation) &&
            _populationManager.TryAssign(allocation, cost.Population);

        if (!reserved)
        {
            Debug.LogWarning($"[ConquestPopulationCoordinator] 원정 인구 배치 실패 - {chunkCoord}");
            return;
        }

        _expeditionAllocations[chunkCoord] = allocation;
    }

    private void HandleConquestCompleted(Vector2Int chunkCoord)
    {
        if (!WiringGuard.Require(_populationManager, nameof(_populationManager), this))
        {
            return;
        }

        if (_expeditionAllocations.TryGetValue(chunkCoord, out PopulationAllocation allocation))
        {
            _populationManager.TryRemoveAllocation(allocation);
            _expeditionAllocations.Remove(chunkCoord);
        }

        int populationReward = _conquestManager.GetPopulationReward(chunkCoord);
        if (populationReward > 0)
        {
            _populationManager.TryIncreaseMaxPopulation(populationReward);
        }
    }
}
