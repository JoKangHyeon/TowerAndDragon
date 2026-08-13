using System;
using System.Collections.Generic;

/// <summary>
/// 타워 유지비의 실제 정산과 UI 미리보기가 공유하는 순수 계산 규칙이다.
/// TerrainUpkeepRules와 같은 역할이며, MonoBehaviour를 모르므로 단위 테스트가 가능하다.
///
/// 지역 유지비가 통나무·돌 두 종류로 고정인 것과 달리 타워는 속성별 특화 자원까지 낼 수 있어,
/// 자원 종류를 고정하지 않고 사전(Dictionary)에 누적한다.
/// </summary>
public static class TowerUpkeepRules
{
    /// <summary>
    /// 타워 한 기가 하루에 내야 하는 유지비.
    /// 1명당 소모량 목록은 데이터 에셋이 소유한 배열을 그대로 참조한다 - 정산 때마다
    /// 타워 수만큼 배열을 새로 만들지 않기 위함이며, 실제 수량은 GetAmount에서 곱한다.
    /// </summary>
    public readonly struct TowerUpkeep
    {
        private readonly IReadOnlyList<ResourceAmount> _perPopulation;

        public int AssignedPopulation { get; }

        public IReadOnlyList<ResourceAmount> PerPopulation =>
            _perPopulation ?? Array.Empty<ResourceAmount>();

        public bool HasUpkeep
        {
            get
            {
                if (AssignedPopulation <= 0 || _perPopulation == null)
                {
                    return false;
                }

                for (int i = 0; i < _perPopulation.Count; i++)
                {
                    if (_perPopulation[i].Amount > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public TowerUpkeep(int assignedPopulation, IReadOnlyList<ResourceAmount> perPopulation)
        {
            AssignedPopulation = Math.Max(0, assignedPopulation);
            _perPopulation = perPopulation;
        }

        /// <summary>이 타워가 index번째 자원으로 하루에 내는 실제 수량.</summary>
        public int GetAmount(int index) =>
            PerPopulation[index].Amount * AssignedPopulation;
    }

    /// <summary>타워 목록이 하루에 낼 유지비를 자원 종류별로 into에 누적한다.</summary>
    public static void AccumulateRequirement(
        IReadOnlyList<TowerUpkeep> towers,
        IDictionary<ResourceType, int> into)
    {
        if (towers == null || into == null)
        {
            return;
        }

        for (int i = 0; i < towers.Count; i++)
        {
            TowerUpkeep tower = towers[i];

            for (int j = 0; j < tower.PerPopulation.Count; j++)
            {
                int amount = tower.GetAmount(j);
                if (amount <= 0)
                {
                    continue;
                }

                ResourceType type = tower.PerPopulation[j].Type;
                into[type] = into.TryGetValue(type, out int existing) ? existing + amount : amount;
            }
        }
    }

    /// <summary>
    /// 미납분을 메우기 위해 비활성화(인구 회수)할 타워의 인덱스를 into에 채운다.
    /// 부족한 자원에 대한 기여가 큰 타워부터 고른다 - 같은 미납분을 메우는 데 꺼야 하는
    /// 타워 수가 가장 적어지기 때문이다. 기여가 같으면 입력 순서가 빠른 쪽을 골라
    /// 정산 결과가 순회 순서에만 의존하도록(재현 가능하도록) 만든다.
    /// 남은 타워를 다 꺼도 미납분이 남으면 그대로 종료한다.
    ///
    /// shortfall은 이 메서드가 직접 깎으므로 호출부가 넘긴 사전이 변경된다
    /// (정산 1회용 버퍼를 넘기는 것을 전제로 한다).
    /// </summary>
    public static void SelectDeactivationTargets(
        IReadOnlyList<TowerUpkeep> towers,
        IDictionary<ResourceType, int> shortfall,
        List<int> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        if (towers == null || shortfall == null)
        {
            return;
        }

        while (HasRemainingShortfall(shortfall))
        {
            int bestIndex = -1;
            int bestContribution = 0;

            for (int i = 0; i < towers.Count; i++)
            {
                if (into.Contains(i))
                {
                    continue;
                }

                int contribution = ResolveContribution(towers[i], shortfall);

                if (contribution > bestContribution)
                {
                    bestContribution = contribution;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            into.Add(bestIndex);
            SubtractContribution(towers[bestIndex], shortfall);
        }
    }

    private static bool HasRemainingShortfall(IDictionary<ResourceType, int> shortfall)
    {
        foreach (KeyValuePair<ResourceType, int> pair in shortfall)
        {
            if (pair.Value > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 이미 다 낸 자원의 유지비는 지금 끄더라도 미납분을 줄이지 못하므로 세지 않는다.
    private static int ResolveContribution(
        in TowerUpkeep tower,
        IDictionary<ResourceType, int> shortfall)
    {
        int contribution = 0;

        for (int i = 0; i < tower.PerPopulation.Count; i++)
        {
            ResourceType type = tower.PerPopulation[i].Type;

            if (!shortfall.TryGetValue(type, out int remaining) || remaining <= 0)
            {
                continue;
            }

            contribution += tower.GetAmount(i);
        }

        return contribution;
    }

    private static void SubtractContribution(
        in TowerUpkeep tower,
        IDictionary<ResourceType, int> shortfall)
    {
        for (int i = 0; i < tower.PerPopulation.Count; i++)
        {
            ResourceType type = tower.PerPopulation[i].Type;

            if (!shortfall.TryGetValue(type, out int remaining))
            {
                continue;
            }

            shortfall[type] = remaining - tower.GetAmount(i);
        }
    }
}
