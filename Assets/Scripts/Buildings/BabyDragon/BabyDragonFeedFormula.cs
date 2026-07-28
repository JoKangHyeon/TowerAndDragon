using UnityEngine;

/// <summary>
/// 새끼용 한 마리가 하루에 먹는 슬라임 양 공식. 실제 소비(BabyDragonFeedingSystem)와
/// 인벤토리 미리보기(UI_DragonInventoryWindow) 양쪽이 같은 공식을 쓰도록 여기 하나로 모은다.
/// </summary>
public static class BabyDragonFeedFormula
{
    // sameTypeCount/totalCount는 "이 용까지 포함해서 설치됐다고 가정했을 때"의 마릿수다.
    public static int ResolveDailyFeed(BabyDragonData data, int sameTypeCount, int totalCount)
    {
        return data.BaseFeed
            + data.AdditionalFeedPerSameType * Mathf.Max(0, sameTypeCount - 1)
            + data.AdditionalFeedPerTotal * Mathf.Max(0, totalCount - 1);
    }
}
