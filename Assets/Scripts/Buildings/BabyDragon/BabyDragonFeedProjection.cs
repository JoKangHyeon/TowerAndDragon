using System.Collections.Generic;

/// <summary>
/// 설치된 새끼용들이 하루에 먹을 슬라임을 집계한다. 실제 지불(BabyDragonFeedingSystem.FeedAll)과
/// 예상치(ResourceForecast)가 같은 경로를 쓰게 해 표시값과 실제 차감액이 어긋나지 않도록 한다
/// (TerrainUpkeepSystem.TrySettle ↔ AccumulateProjectedUpkeep과 같은 구조).
///
/// 마릿수 세기만 다루므로 BabyDragonFeedFormula(순수 공식)와는 분리해 둔다 - 공식 쪽은
/// MonoBehaviour를 몰라야 EditMode에서 그대로 검증할 수 있다.
/// </summary>
public static class BabyDragonFeedProjection
{
    // AccumulateDailyFeed 안에서만 쓰는 재사용 버퍼. 예상치는 건물·인구가 바뀔 때마다 다시 계산되므로
    // 호출마다 새 Dictionary를 만들지 않는다(ResourceForecastTooltipBuilder.BODY_BUILDER와 같은 관례).
    // 메인 스레드 전용이고 서로 중첩 호출되지 않아 안전하다.
    private static readonly Dictionary<DragonType, int> INSTALLED_COUNT_BUFFER = new();

    /// <summary>
    /// 설치된 새끼용의 속성별 마릿수와 전체 마릿수를 센다. 버퍼는 호출자가 소유하며 내용은 먼저 비운다.
    ///
    /// DragonData가 아직 주입되지 않은 개체(배치 이벤트 순서상 Setup 전일 수 있다)는 속성을 알 수 없어
    /// 속성별 집계에서는 빠지지만 totalCount에는 포함한다 - 전체 마릿수 추가분이 실제 지불과
    /// 같은 기준으로 계산되어야 하기 때문이다.
    /// </summary>
    public static void AccumulateInstalledCounts(
        IEnumerable<Building> buildings,
        IDictionary<DragonType, int> into,
        out int totalCount)
    {
        totalCount = 0;

        if (buildings == null || into == null)
        {
            return;
        }

        into.Clear();

        foreach (Building building in buildings)
        {
            if (!(building is BabyDragonTower babyDragon))
            {
                continue;
            }

            totalCount += 1;

            if (babyDragon.DragonData == null)
            {
                continue;
            }

            DragonType dragonType = babyDragon.DragonData.DragonType;
            into.TryGetValue(dragonType, out int count);
            into[dragonType] = count + 1;
        }
    }

    /// <summary>
    /// 다음 아침에 걷힐 하루치 먹이를 슬라임 종류별로 누적한다. 자원을 소비하지 않는다.
    ///
    /// 실제 지불은 새끼용 한 마리씩 전부-또는-전무로 판정하지만(BabyDragonFeedingSystem.Feed),
    /// 예상치는 "이대로면 얼마가 필요한가"이므로 요구량을 그대로 합산한다
    /// (자재가 모자라면 시설을 비활성화하는 지역 유지비도 예상치는 요구량 전액을 보여준다).
    /// </summary>
    public static void AccumulateDailyFeed(
        IEnumerable<Building> buildings,
        IDictionary<ResourceType, int> into)
    {
        if (buildings == null || into == null)
        {
            return;
        }

        AccumulateInstalledCounts(buildings, INSTALLED_COUNT_BUFFER, out int totalCount);

        foreach (Building building in buildings)
        {
            if (!(building is BabyDragonTower babyDragon) || babyDragon.DragonData == null)
            {
                continue;
            }

            BabyDragonData data = babyDragon.DragonData;

            if (!DragonSlimeTable.TryGetFeedSlime(data.DragonType, out ResourceType slimeType))
            {
                continue;
            }

            INSTALLED_COUNT_BUFFER.TryGetValue(data.DragonType, out int sameTypeCount);
            int requiredFeed = BabyDragonFeedFormula.ResolveDailyFeed(data, sameTypeCount, totalCount);

            if (requiredFeed <= 0)
            {
                continue;
            }

            into[slimeType] = into.TryGetValue(slimeType, out int existing)
                ? existing + requiredFeed
                : requiredFeed;
        }
    }
}
