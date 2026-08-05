using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5속성의 대표 색. 스킬트리·새끼용 인벤토리 등이 공유하는 단일 출처다.
/// 값은 기획 프로토타입(Docs/Sangwook/용_스킬트리_프로토타입.html)의 속성 색과 일치시킨다.
/// 창마다 인스펙터로 따로 지정하면 값이 어긋나므로 여기서만 정의한다.
/// </summary>
public static class DragonAttributePalette
{
    private static readonly Dictionary<DragonType, Color> COLOR_BY_ATTRIBUTE = new()
    {
        { DragonType.Ice, new Color(0.357f, 0.753f, 0.878f) },
        { DragonType.Fire, new Color(0.878f, 0.376f, 0.235f) },
        { DragonType.Time, new Color(0.690f, 0.490f, 0.878f) },
        { DragonType.Stone, new Color(0.788f, 0.635f, 0.290f) },
        { DragonType.Life, new Color(0.498f, 0.820f, 0.310f) },
    };

    public static Color ColorOf(DragonType attribute) =>
        COLOR_BY_ATTRIBUTE.TryGetValue(attribute, out Color color) ? color : Color.white;
}
