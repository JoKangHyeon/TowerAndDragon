using System.Collections.Generic;

/// <summary>
/// 새끼용 속성별 먹이 슬라임. 기획의 속성-바이옴 대응(기획종합_v2 §10)을 고정한다.
/// 에셋에서 슬라임 종류를 따로 입력받지 않으므로 속성과 먹이가 어긋날 수 없다.
/// </summary>
public static class BabyDragonSlimeTable
{
    private static readonly Dictionary<DragonType, ResourceType> FEED_SLIME_BY_DRAGON_TYPE = new()
    {
        { DragonType.Ice, ResourceType.SnowSlime },
        { DragonType.Fire, ResourceType.VolcanoSlime },
        { DragonType.Time, ResourceType.DesertSlime },
        { DragonType.Stone, ResourceType.RockSlime },
        { DragonType.Life, ResourceType.GrassSlime },
    };

    public static bool TryGetFeedSlime(DragonType dragonType, out ResourceType slimeType) =>
        FEED_SLIME_BY_DRAGON_TYPE.TryGetValue(dragonType, out slimeType);
}
