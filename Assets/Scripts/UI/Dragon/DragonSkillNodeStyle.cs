// 스킬트리 노드 한 개의 "생김새" 규격. 노드 종류(DragonNodeKind)마다 크기·테두리 두께·라벨 크기를
// 달리 줘서, 어미용 강화 노드와 새끼용(알) 노드가 한눈에 구분되게 만든다.
//
// 구분 원칙 - 어미용은 "속성 색으로 꽉 찬 큰 원", 새끼용은 "속성 색 테두리만 두른 작은 원 + 알 아이콘".
// 프리팹을 둘로 나누지 않고 이 규격으로 갈라 쓴다(공통 부분을 두 곳에서 고치지 않기 위해).
public readonly struct DragonSkillNodeStyle
{
    public DragonSkillNodeStyle(float diameter, float ringThickness, float labelFontSize, bool isKin)
    {
        Diameter = diameter;
        RingThickness = ringThickness;
        LabelFontSize = labelFontSize;
        IsKin = isKin;
    }

    public float Diameter { get; }

    /// <summary>바깥 테두리(속성 색) 두께. 안쪽 채움이 이만큼 안으로 들어간다.</summary>
    public float RingThickness { get; }

    public float LabelFontSize { get; }

    /// <summary>새끼용 노드인가 - 채움을 비우고 알 아이콘을 얹을지 결정한다.</summary>
    public bool IsKin { get; }
}

/// <summary>
/// 노드 종류 → 생김새 규격. 값의 단일 출처다(DragonAttributePalette와 같은 역할).
/// </summary>
public static class DragonSkillNodeStyleTable
{
    /// <summary>알 아이콘 지름 / 노드 지름. 비율로 잡아 노드 크기를 바꿔도 여백이 유지된다.</summary>
    public const float ICON_DIAMETER_RATIO = 0.62f;

    /// <summary>자물쇠 지름 / 노드 지름. 아이콘과 따로 두어 둘을 독립적으로 조절한다.</summary>
    public const float LOCK_DIAMETER_RATIO = 0.62f;

    // 어미용 노드는 루트(액티브 해금) > 궁극 > 나머지 갈래 순으로 크다 - 트리를 볼 때
    // 어디가 시작이고 어디가 끝인지 크기만으로 읽히게 한다.
    private const float ROOT_DIAMETER = 96f;
    private const float ULTIMATE_DIAMETER = 88f;
    private const float BRANCH_DIAMETER = 80f;
    private const float KIN_DIAMETER = 68f;

    private const float MOTHER_RING_THICKNESS = 5f;
    private const float ULTIMATE_RING_THICKNESS = 7f;

    // 새끼용은 채움이 아니라 테두리로 속성을 읽히게 하므로 어미용보다 두껍다.
    private const float KIN_RING_THICKNESS = 9f;

    private const float MOTHER_LABEL_FONT_SIZE = 15f;
    private const float KIN_LABEL_FONT_SIZE = 13f;

    private static readonly DragonSkillNodeStyle ROOT_STYLE =
        new(ROOT_DIAMETER, MOTHER_RING_THICKNESS, MOTHER_LABEL_FONT_SIZE, false);

    private static readonly DragonSkillNodeStyle ULTIMATE_STYLE =
        new(ULTIMATE_DIAMETER, ULTIMATE_RING_THICKNESS, MOTHER_LABEL_FONT_SIZE, false);

    private static readonly DragonSkillNodeStyle BRANCH_STYLE =
        new(BRANCH_DIAMETER, MOTHER_RING_THICKNESS, MOTHER_LABEL_FONT_SIZE, false);

    private static readonly DragonSkillNodeStyle KIN_STYLE =
        new(KIN_DIAMETER, KIN_RING_THICKNESS, KIN_LABEL_FONT_SIZE, true);

    public static DragonSkillNodeStyle For(DragonNodeKind kind)
    {
        return kind switch
        {
            DragonNodeKind.ActiveUnlock => ROOT_STYLE,
            DragonNodeKind.Ultimate => ULTIMATE_STYLE,
            DragonNodeKind.KinTower => KIN_STYLE,
            DragonNodeKind.KinArea => KIN_STYLE,
            _ => BRANCH_STYLE,
        };
    }
}
