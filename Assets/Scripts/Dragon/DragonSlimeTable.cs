using System.Collections.Generic;

/// <summary>
/// 속성별 바이옴 슬라임. 기획의 속성-바이옴 대응(기획종합_v2 §10)을 고정한다.
/// 에셋에서 슬라임 종류를 따로 입력받지 않으므로 속성과 슬라임이 어긋날 수 없다.
/// 새끼용 먹이와 UI 아이콘 틴트가 같은 표를 공유한다.
/// </summary>
public static class DragonSlimeTable
{
    private static readonly Dictionary<DragonType, ResourceType> SLIME_BY_ATTRIBUTE = new()
    {
        { DragonType.Ice, ResourceType.SnowSlime },
        { DragonType.Fire, ResourceType.VolcanoSlime },
        { DragonType.Time, ResourceType.DesertSlime },
        { DragonType.Stone, ResourceType.RockSlime },
        { DragonType.Life, ResourceType.GrassSlime },
    };

    // 정방향 표를 뒤집어 만든다. 두 방향을 따로 적으면 한쪽만 고쳤을 때 조용히 어긋난다.
    private static readonly Dictionary<ResourceType, DragonType> ATTRIBUTE_BY_SLIME = BuildReverse();

    public static bool TryGetFeedSlime(DragonType dragonType, out ResourceType slimeType) =>
        SLIME_BY_ATTRIBUTE.TryGetValue(dragonType, out slimeType);

    /// <summary>슬라임 자원이면 그 속성을 돌려준다. 슬라임이 아닌 자원이면 false.</summary>
    public static bool TryGetAttribute(ResourceType slimeType, out DragonType attribute) =>
        ATTRIBUTE_BY_SLIME.TryGetValue(slimeType, out attribute);

    private static Dictionary<ResourceType, DragonType> BuildReverse()
    {
        Dictionary<ResourceType, DragonType> reverse = new();

        foreach (KeyValuePair<DragonType, ResourceType> pair in SLIME_BY_ATTRIBUTE)
        {
            reverse.Add(pair.Value, pair.Key);
        }

        return reverse;
    }
}
