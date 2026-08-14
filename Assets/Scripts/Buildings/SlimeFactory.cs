using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬라임 농장. 풋프린트에 걸친 지형별 슬라임을 건물 위 슬라임 스프라이트 칸에 나눠 표시한다.
/// 칸에 씌우는 색은 DragonAttributePalette가 단일 출처다(공용 흰 슬라임 스프라이트 하나를 틴트로 구분).
/// </summary>
public class SlimeFactory : Factory
{
    // 슬라임은 자기 농장 프레임보다 정확히 한 칸 앞에 그린다. 프레임의 정렬 순서는 앵커 좌표로
    // 런타임에 정해지므로(Building.SetDepthSortOrder ← IsometricMath.ComputeDepthSortOrder),
    // 프리팹에 고정 값을 박아두면 프레임 순서가 그 값보다 큰 지역(맵 남쪽 용암 지대 등)에서
    // 슬라임이 프레임 뒤로 숨어 아예 보이지 않는다.
    private const int SLIME_DEPTH_SORT_OFFSET = 1;

    [Tooltip("건물 위에 표시할 슬라임 스프라이트 칸. 칸 수보다 슬라임 종류가 많으면 생산량이 많은 종류부터 표시한다.")]
    [SerializeField] private List<SpriteRenderer> _slimes;

    // 종류별 표시 몫. 칸 배분은 최대 잉여법(내림 → 소수부 큰 순서로 남은 칸 배분)을 쓴다.
    private readonly struct SlimeShare
    {
        public readonly ResourceType Type;
        public readonly int Amount;
        public readonly int SlotCount;
        public readonly float Remainder;

        public SlimeShare(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
            SlotCount = 0;
            Remainder = 0f;
        }

        private SlimeShare(ResourceType type, int amount, int slotCount, float remainder)
        {
            Type = type;
            Amount = amount;
            SlotCount = slotCount;
            Remainder = remainder;
        }

        public SlimeShare WithSlots(int slotCount, float remainder) =>
            new SlimeShare(Type, Amount, slotCount, remainder);

        public SlimeShare WithOneMoreSlot() =>
            new SlimeShare(Type, Amount, SlotCount + 1, Remainder);
    }

    // 인구 변경마다 다시 계산하므로 버퍼를 재사용한다.
    private readonly List<SlimeShare> _shares = new();
    private readonly List<int> _shareIndicesByRemainder = new();

    private GridMap _gridMap;

    public override bool Initialize(ResourceManager resourceManager, CycleManager cycleManager, GridMap gridMap)
    {
        // 실패하면 base가 _population을 채우지 않으므로 여기서 더 진행하면 안 된다.
        if (!base.Initialize(resourceManager, cycleManager, gridMap))
        {
            return false;
        }

        _gridMap = gridMap;
        _population.OnPopulationChanged.AddListener(HandleOnPopulationChanged);

        // 풋프린트가 바뀌면 나오는 슬라임 종류도 바뀐다. 이동은 인구 변경을 일으키지 않으므로 따로 듣는다.
        _gridMap.OnBuildingMoved.AddListener(HandleOnBuildingMoved);

        // FactoryPopulation.Initialize는 자기 안에서 OnPopulationChanged를 한 번 발화하는데,
        // 두 초기화(FactoryResourceCoordinator / FactoryPopulationCoordinator)는 각자
        // GridMap.OnBuildingAdded를 구독해 호출되므로 순서가 보장되지 않는다. 인구 쪽이 먼저
        // 돌면 그 첫 발화를 놓쳐 프리팹 기본 상태(흰 슬라임)가 그대로 남으므로 여기서 한 번 반영한다.
        ShowSlimes();
        return true;
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingMoved.RemoveListener(HandleOnBuildingMoved);
        }
    }

    private void HandleOnPopulationChanged() => ShowSlimes();

    private void HandleOnBuildingMoved(Building movedBuilding)
    {
        if (movedBuilding == this)
        {
            ShowSlimes();
        }
    }

    public override void SetDepthSortOrder(int sortingOrder)
    {
        base.SetDepthSortOrder(sortingOrder);
        ApplySlimeDepthSortOrder(sortingOrder + SLIME_DEPTH_SORT_OFFSET);
    }

    private void ApplySlimeDepthSortOrder(int sortingOrder)
    {
        if (_slimes == null)
        {
            return;
        }

        foreach (SpriteRenderer slime in _slimes)
        {
            if (slime != null)
            {
                slime.sortingOrder = sortingOrder;
            }
        }
    }

    public void ShowSlimes()
    {
        if (_slimes == null || _slimes.Count == 0)
        {
            return;
        }

        CollectShares();

        int usedSlotCount = 0;

        if (_shares.Count > 0)
        {
            DistributeSlots();
            usedSlotCount = PaintSlots();
        }

        // 이번에 쓰지 않은 칸은 끈다 - 종류가 줄어들었을 때 이전 색이 남지 않게 한다.
        for (int i = usedSlotCount; i < _slimes.Count; i++)
        {
            if (_slimes[i] != null)
            {
                _slimes[i].gameObject.SetActive(false);
            }
        }
    }

    // 다음 정산에서 실제로 들어올 양이 있는 슬라임 종류만 모은다.
    private void CollectShares()
    {
        _shares.Clear();

        foreach (ResourceType resourceType in EnumerateProducedResourceTypes())
        {
            if (!IsSlime(resourceType))
            {
                continue;
            }

            int amount = GetCurrentYield(resourceType);
            if (amount <= 0)
            {
                continue;
            }

            _shares.Add(new SlimeShare(resourceType, amount));
        }
    }

    // 칸을 종류별로 나눈다. 종류마다 최소 한 칸을 주고, 남은 칸을 생산량 비율대로 배분한다.
    private void DistributeSlots()
    {
        // 생산량 내림차순 - 칸이 종류보다 적을 때 많이 나는 종류부터 살린다.
        _shares.Sort((a, b) => b.Amount.CompareTo(a.Amount));

        // 표시할 종류를 칸 수만큼으로 줄인다. 잘려나간 종류는 비율 계산에서도 빠져야 하므로 합계를 여기서 낸다.
        if (_shares.Count > _slimes.Count)
        {
            _shares.RemoveRange(_slimes.Count, _shares.Count - _slimes.Count);
        }

        int totalAmount = 0;
        foreach (SlimeShare share in _shares)
        {
            totalAmount += share.Amount;
        }

        int slotsAfterMinimum = _slimes.Count - _shares.Count;
        int distributedSlotCount = 0;

        for (int i = 0; i < _shares.Count; i++)
        {
            SlimeShare share = _shares[i];
            float exactSlotCount = (float)share.Amount / totalAmount * slotsAfterMinimum;
            int flooredSlotCount = Mathf.FloorToInt(exactSlotCount);

            _shares[i] = share.WithSlots(1 + flooredSlotCount, exactSlotCount - flooredSlotCount);
            distributedSlotCount += flooredSlotCount;
        }

        // 내림 때문에 남은 칸은 소수부가 큰 종류에 하나씩 얹는다. 남는 칸 수는 항상 종류 수보다 적으므로 한 바퀴로 끝난다.
        int leftoverSlotCount = slotsAfterMinimum - distributedSlotCount;
        if (leftoverSlotCount <= 0)
        {
            return;
        }

        _shareIndicesByRemainder.Clear();
        for (int i = 0; i < _shares.Count; i++)
        {
            _shareIndicesByRemainder.Add(i);
        }

        _shareIndicesByRemainder.Sort((a, b) => _shares[b].Remainder.CompareTo(_shares[a].Remainder));

        for (int i = 0; i < leftoverSlotCount; i++)
        {
            int index = _shareIndicesByRemainder[i];
            _shares[index] = _shares[index].WithOneMoreSlot();
        }
    }

    // 배분된 몫대로 칸을 켜고 속성 색을 씌운다. 실제로 쓴 칸 수를 돌려준다.
    private int PaintSlots()
    {
        int slotIndex = 0;

        foreach (SlimeShare share in _shares)
        {
            Color tint = DragonAttributePalette.TintFor(share.Type);

            for (int i = 0; i < share.SlotCount && slotIndex < _slimes.Count; i++)
            {
                SpriteRenderer slime = _slimes[slotIndex];
                slotIndex++;

                if (slime == null)
                {
                    continue;
                }

                slime.gameObject.SetActive(true);
                slime.color = tint;
            }
        }

        return slotIndex;
    }

    private bool IsSlime(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.GrassSlime:
            case ResourceType.RockSlime:
            case ResourceType.VolcanoSlime:
            case ResourceType.DesertSlime:
            case ResourceType.SnowSlime:
                return true;
            default:
                return false;
        }
    }
}
