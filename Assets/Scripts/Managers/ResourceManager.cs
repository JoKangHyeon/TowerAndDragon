using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 자원 보유량 관리자. 보유량의 단일 출처이며, 증감은 반드시 이 클래스의 API를 거친다.
/// 생산건물(추후)은 정산 시 Add()를 호출하고, UI는 ResourceChanged를 구독한다.
/// 인구는 별도의 인구 시스템이 담당한다(자원 아님 — 이 클래스에서 다루지 않는다).
/// (싱글톤 아님 — GameManager가 [SerializeField]로 보유하고 Awake에서 Construct를 호출한다.)
/// </summary>
public class ResourceManager : MonoBehaviour
{
    [Tooltip("전체 자원 목록. Construct 시 이 목록의 모든 자원을 0으로 시드한다.")]
    [SerializeField] private ResourceCatalog _catalog;

    [Tooltip("게임 시작 시 지급할 초기 자원.")]
    [SerializeField] private List<ResourceAmount> _initialResources;

    private readonly Dictionary<ResourceType, int> _amounts = new();

    /// <summary>자원 보유량 변경 시 (종류, 변경 후 보유량). UI가 구독한다.</summary>
    public UnityEvent<ResourceType, int> ResourceChanged;

    public ResourceCatalog Catalog => _catalog;

    public void Construct(GameManager gameManager)
    {
        // 카탈로그의 전 종류를 0으로 시드해 GetAmount가 항상 유효한 값을 반환하게 한다.
        if (_catalog != null)
        {
            foreach (ResourceData resource in _catalog.All)
            {
                if (resource != null)
                {
                    _amounts[resource.Type] = 0;
                }
            }
        }

        foreach (ResourceAmount initial in _initialResources)
        {
            Add(initial.Type, initial.Amount);
        }
    }

    // --- 개별 자원 API (생산건물/소비처가 쓰는 기본 계약) ---

    public int GetAmount(ResourceType type)
    {
        Debug.Assert(IsSingleType(type), $"[ResourceManager] 단일 자원 종류만 조회 가능: {type}");
        return _amounts.TryGetValue(type, out int amount) ? amount : 0;
    }

    public int ConsumeUpTo(
        ResourceType type, int requestedAmount
    )
    {
        Debug.Assert(IsSingleType(type),$"[ResourceManager] 단일 자원 종류만 소비 가능: {type}");

        if (requestedAmount <= 0)
        {
            return 0;
        }

        int currentAmount = GetAmount(type);
        int consumedAmount = Math.Min(currentAmount, requestedAmount);

        if (consumedAmount > 0)
        {
            TrySpend(type, consumedAmount);
        }

        return consumedAmount;
    }

    public void Add(ResourceType type, int amount)
    {
        Debug.Assert(IsSingleType(type), $"[ResourceManager] 단일 자원 종류만 추가 가능: {type}");
        if (amount <= 0)
        {
            return;
        }

        _amounts[type] = GetAmount(type) + amount;
        ResourceChanged?.Invoke(type, _amounts[type]);
    }

    public bool TrySpend(ResourceType type, int amount)
    {
        Debug.Assert(IsSingleType(type), $"[ResourceManager] 단일 자원 종류만 소비 가능: {type}");
        if (amount <= 0 || GetAmount(type) < amount)
        {
            return false;
        }

        _amounts[type] = GetAmount(type) - amount;
        ResourceChanged?.Invoke(type, _amounts[type]);
        return true;
    }

    // --- ResourceCost 브리지 (점령 시스템 호환) ---

    // 점령 시스템이 요구하는 보유량 스냅샷(기본 자원만).
    // Population은 이 매니저 소관이 아니므로 0으로 남는다 — 인구 시스템이 생기면 호출자가 채운다.
    public ResourceCost GetHoldingsSnapshot() => new ResourceCost
    {
        Food = GetAmount(ResourceType.Food),
        Wood = GetAmount(ResourceType.Wood),
        Stone = GetAmount(ResourceType.Stone),
    };

    // 점령 비용 중 자원만 차감한다(인구 차감은 인구 시스템 담당).
    public void Spend(ResourceCost cost)
    {
        TrySpend(ResourceType.Food, cost.Food);
        TrySpend(ResourceType.Wood, cost.Wood);
        TrySpend(ResourceType.Stone, cost.Stone);
    }

    // 단일 비트(자원 1종)인지 판정. None/복합 플래그 방어용.
    private static bool IsSingleType(ResourceType type) =>
        type != ResourceType.None && (type & (type - 1)) == ResourceType.None;
}