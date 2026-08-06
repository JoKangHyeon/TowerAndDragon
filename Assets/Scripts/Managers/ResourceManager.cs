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
        SeedCatalog();

        foreach (ResourceAmount initial in _initialResources)
        {
            AddInitial(initial.Type, initial.Amount);
        }
    }

    /// <summary>
    /// 세이브 복원 전용. 보유량을 저장된 절대값으로 통째로 덮어쓴다.
    /// Construct는 시드와 초기 자원 지급이 붙어 있어 재사용할 수 없고, Add는 증분이라 절대값을
    /// 넣을 수 없어 별도 경로가 필요하다. 이어하기 경로에서는 Construct를 부르지 않으므로
    /// 여기서 카탈로그 시드까지 함께 한다.
    /// 카탈로그 전 종류에 대해 ResourceChanged를 발화해, 세이브에 없던 자원 행이 UI에 낡은 값으로
    /// 남지 않게 한다.
    /// </summary>
    public void RestoreAmounts(IReadOnlyList<ResourceAmount> amounts)
    {
        SeedCatalog();

        foreach (ResourceAmount entry in amounts)
        {
            if (IsSingleType(entry.Type))
            {
                _amounts[entry.Type] = Math.Max(0, entry.Amount);
            }
        }

        foreach (ResourceType type in new List<ResourceType>(_amounts.Keys))
        {
            ResourceChanged?.Invoke(type, _amounts[type]);
        }
    }

    /// <summary>카탈로그의 전 종류를 0으로 시드해 GetAmount가 항상 유효한 값을 반환하게 한다.</summary>
    private void SeedCatalog()
    {
        if (_catalog == null)
        {
            return;
        }

        foreach (ResourceData resource in _catalog.All)
        {
            if (resource != null)
            {
                _amounts[resource.Type] = 0;
            }
        }
    }

    // 초기 자원을 지급한다. 인스펙터에서 Everything처럼 복합 플래그를 고르면(모든 비트 켜짐),
    // 카탈로그에 존재하는 단일 자원 각각에 같은 수량을 지급한다.
    private void AddInitial(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (IsSingleType(type))
        {
            Add(type, amount);
            return;
        }

        if (_catalog == null)
        {
            return;
        }

        foreach (ResourceData resource in _catalog.All)
        {
            if (resource == null || !IsSingleType(resource.Type))
            {
                continue;
            }

            // resource.Type의 비트가 복합 플래그 type에 포함돼 있으면 지급.
            if ((type & resource.Type) == resource.Type)
            {
                Add(resource.Type, amount);
            }
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

    // --- 자원 종류 무관 비용 묶음 API (건물 건설 비용 등, 특화 자원 포함 임의 조합) ---

    // cost의 모든 항목을 보유량이 충족하는지 확인 - Spend/Add 호출 전에 반드시 이걸로 먼저 확인할 것
    // (Spend는 부족한 항목을 만나도 나머지를 계속 진행하므로, 부분 차감을 막으려면 호출자가 미리 걸러야 한다).
    public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
    {
        foreach (ResourceAmount entry in cost)
        {
            if (GetAmount(entry.Type) < entry.Amount)
                return false;
        }
        return true;
    }

    public void Spend(IReadOnlyList<ResourceAmount> cost)
    {
        foreach (ResourceAmount entry in cost)
            TrySpend(entry.Type, entry.Amount);
    }

    public void Add(IReadOnlyList<ResourceAmount> amounts)
    {
        foreach (ResourceAmount entry in amounts)
            Add(entry.Type, entry.Amount);
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