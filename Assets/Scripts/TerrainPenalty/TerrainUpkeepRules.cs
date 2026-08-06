using System.Collections.Generic;

/// <summary>
/// 지역 유지비의 실제 정산과 (추후) UI 미리보기가 공유하는 순수 계산 규칙이다.
/// PopulationUpkeepRules와 같은 역할이며, MonoBehaviour를 모르므로 단위 테스트가 가능하다.
/// </summary>
public static class TerrainUpkeepRules
{
    /// <summary>시설 한 개가 하루에 내야 하는 지역 유지비. 인구를 이미 곱해 정수화한 값이다.</summary>
    public readonly struct FacilityUpkeep
    {
        public int WoodAmount { get; }
        public int StoneAmount { get; }

        public bool HasUpkeep => WoodAmount > 0 || StoneAmount > 0;

        public FacilityUpkeep(int woodAmount, int stoneAmount)
        {
            WoodAmount = woodAmount;
            StoneAmount = stoneAmount;
        }
    }

    public static void AccumulateRequirement(
        IReadOnlyList<FacilityUpkeep> facilities,
        out int requiredWood,
        out int requiredStone)
    {
        requiredWood = 0;
        requiredStone = 0;

        if (facilities == null)
        {
            return;
        }

        for (int i = 0; i < facilities.Count; i++)
        {
            requiredWood += facilities[i].WoodAmount;
            requiredStone += facilities[i].StoneAmount;
        }
    }

    /// <summary>
    /// 미납분을 메우기 위해 비활성화(인구 회수)할 시설의 인덱스를 into에 채운다.
    /// 부족한 자원에 대한 기여가 큰 시설부터 고른다 - 같은 미납분을 메우는 데 꺼야 하는
    /// 시설 수가 가장 적어지기 때문이다. 기여가 같으면 입력 순서가 빠른 쪽을 골라
    /// 정산 결과가 호출 순서에만 의존하도록(재현 가능하도록) 만든다.
    /// 남은 시설을 다 꺼도 미납분이 남으면(예: 유지비 0인 시설만 남음) 그대로 종료한다.
    /// </summary>
    public static void SelectDeactivationTargets(
        IReadOnlyList<FacilityUpkeep> facilities,
        int woodShortfall,
        int stoneShortfall,
        List<int> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        if (facilities == null)
        {
            return;
        }

        int remainingWood = woodShortfall;
        int remainingStone = stoneShortfall;

        while (remainingWood > 0 || remainingStone > 0)
        {
            int bestIndex = -1;
            int bestContribution = 0;

            for (int i = 0; i < facilities.Count; i++)
            {
                if (into.Contains(i))
                {
                    continue;
                }

                // 이미 다 낸 자원의 유지비는 지금 끄더라도 미납분을 줄이지 못하므로 세지 않는다.
                int contribution =
                    (remainingWood > 0 ? facilities[i].WoodAmount : 0) +
                    (remainingStone > 0 ? facilities[i].StoneAmount : 0);

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
            remainingWood -= facilities[bestIndex].WoodAmount;
            remainingStone -= facilities[bestIndex].StoneAmount;
        }
    }
}
