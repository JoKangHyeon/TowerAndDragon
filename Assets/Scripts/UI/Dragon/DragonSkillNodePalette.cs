using UnityEngine;

/// <summary>
/// 스킬트리 노드·연결선의 상태별 색. 속성 색(DragonAttributePalette)을 어떤 비율로 섞는지를
/// 여기 한 곳에 모아 둔다.
///
/// 규칙: 테두리는 어떤 상태에서도 속성 색을 유지한다(밝기만 낮춘다) - 잠긴 노드까지 전부
/// 검게 칠하면 어느 갈래가 어느 속성인지 한눈에 안 보이기 때문이다.
/// 채움은 어미용만 속성 색으로 꽉 채우고, 새끼용은 계속 비워 둔다(생김새 구분의 축).
/// </summary>
public static class DragonSkillNodePalette
{
    // 노드 안쪽 바탕. 속성 색을 섞기 전의 어두운 기준색이다.
    private static readonly Color DARK_FILL = new Color(0.08f, 0.06f, 0.05f, 1f);

    private static readonly Color LABEL_COMPLETED = new Color(0.96f, 0.92f, 0.87f, 1f);
    private static readonly Color LABEL_AVAILABLE = new Color(0.80f, 0.75f, 0.69f, 1f);
    private static readonly Color LABEL_SHORT = new Color(0.66f, 0.61f, 0.56f, 1f);
    private static readonly Color LABEL_LOCKED = new Color(0.46f, 0.42f, 0.39f, 1f);

    // 테두리 밝기 - 완료는 그대로, 잠길수록 어둡게. 색상(Hue)은 절대 바꾸지 않는다.
    private const float RING_BRIGHTNESS_COMPLETED = 1f;
    private const float RING_BRIGHTNESS_AVAILABLE = 0.85f;
    private const float RING_BRIGHTNESS_SHORT = 0.68f;
    private const float RING_BRIGHTNESS_LOCKED = 0.42f;

    // 어미용 채움에 섞는 속성 색 비율. 완료 시 원이 속성 색으로 꽉 찬다.
    private const float MOTHER_FILL_RATIO_COMPLETED = 1f;
    private const float MOTHER_FILL_RATIO_AVAILABLE = 0.42f;
    private const float MOTHER_FILL_RATIO_SHORT = 0.20f;
    private const float MOTHER_FILL_RATIO_LOCKED = 0.06f;

    // 새끼용 채움은 완료해도 거의 비워 둔다 - 테두리·알 아이콘 쪽으로 시선이 가게 한다.
    private const float KIN_FILL_RATIO_COMPLETED = 0.26f;
    private const float KIN_FILL_RATIO_AVAILABLE = 0.13f;
    private const float KIN_FILL_RATIO_SHORT = 0.07f;
    private const float KIN_FILL_RATIO_LOCKED = 0.03f;

    // 알 아이콘 밝기(아이콘 자체가 이미 속성별로 색이 다르므로 밝기만 조절한다).
    private const float ICON_BRIGHTNESS_COMPLETED = 1f;
    private const float ICON_BRIGHTNESS_AVAILABLE = 0.9f;
    private const float ICON_BRIGHTNESS_SHORT = 0.75f;
    private const float ICON_BRIGHTNESS_LOCKED = 0.5f;

    // 자원 부족(InsufficientResources/ExtraCost)은 "선행·게이트는 충족했고 자원만 모자란" 상태라
    // 불투명하게 두고, 그 외 잠금 사유(선행·게이트·낮밤·무효)는 더 멀리 잠겨 있음을 나타내도록
    // 살짝 투명하게 처리한다.
    private const float DEEP_LOCKED_ALPHA = 0.85f;

    // 아직 못 산 연결선에도 속성 색을 옅게 남긴다 - 갈래가 어느 속성인지 선만 보고도 알 수 있게.
    private const float EDGE_LOCKED_TINT_RATIO = 0.4f;

    public static Color RingColor(ProgressionNodeState state, Color attributeColor)
    {
        return EmphasisOf(state) switch
        {
            Emphasis.Completed => Scaled(attributeColor, RING_BRIGHTNESS_COMPLETED, 1f),
            Emphasis.Available => Scaled(attributeColor, RING_BRIGHTNESS_AVAILABLE, 1f),
            Emphasis.Short => Scaled(attributeColor, RING_BRIGHTNESS_SHORT, 1f),
            _ => Scaled(attributeColor, RING_BRIGHTNESS_LOCKED, DEEP_LOCKED_ALPHA),
        };
    }

    public static Color FillColor(ProgressionNodeState state, Color attributeColor, bool isKin)
    {
        Emphasis emphasis = EmphasisOf(state);
        float ratio = isKin ? KinFillRatio(emphasis) : MotherFillRatio(emphasis);
        Color fill = Color.Lerp(DARK_FILL, attributeColor, ratio);

        if (emphasis == Emphasis.Locked)
        {
            fill.a = DEEP_LOCKED_ALPHA;
        }

        return fill;
    }

    public static Color IconColor(ProgressionNodeState state)
    {
        return EmphasisOf(state) switch
        {
            Emphasis.Completed => Scaled(Color.white, ICON_BRIGHTNESS_COMPLETED, 1f),
            Emphasis.Available => Scaled(Color.white, ICON_BRIGHTNESS_AVAILABLE, 1f),
            Emphasis.Short => Scaled(Color.white, ICON_BRIGHTNESS_SHORT, 1f),
            _ => Scaled(Color.white, ICON_BRIGHTNESS_LOCKED, DEEP_LOCKED_ALPHA),
        };
    }

    public static Color LabelColor(ProgressionNodeState state)
    {
        return EmphasisOf(state) switch
        {
            Emphasis.Completed => LABEL_COMPLETED,
            Emphasis.Available => LABEL_AVAILABLE,
            Emphasis.Short => LABEL_SHORT,
            _ => LABEL_LOCKED,
        };
    }

    /// <summary>연결선 색. 아직 못 산 선도 속성 색을 옅게 남긴다.</summary>
    public static Color EdgeColor(bool unlocked, Color attributeColor, Color lockedBaseColor)
    {
        return unlocked
            ? attributeColor
            : Color.Lerp(lockedBaseColor, attributeColor, EDGE_LOCKED_TINT_RATIO);
    }

    private static float MotherFillRatio(Emphasis emphasis)
    {
        return emphasis switch
        {
            Emphasis.Completed => MOTHER_FILL_RATIO_COMPLETED,
            Emphasis.Available => MOTHER_FILL_RATIO_AVAILABLE,
            Emphasis.Short => MOTHER_FILL_RATIO_SHORT,
            _ => MOTHER_FILL_RATIO_LOCKED,
        };
    }

    private static float KinFillRatio(Emphasis emphasis)
    {
        return emphasis switch
        {
            Emphasis.Completed => KIN_FILL_RATIO_COMPLETED,
            Emphasis.Available => KIN_FILL_RATIO_AVAILABLE,
            Emphasis.Short => KIN_FILL_RATIO_SHORT,
            _ => KIN_FILL_RATIO_LOCKED,
        };
    }

    // 알파는 그대로 두고 RGB만 어둡게 한다 - Color * float은 알파까지 같이 깎아
    // "밝기만 낮추기"와 "투명하게 하기"를 구분할 수 없다.
    private static Color Scaled(Color color, float brightness, float alpha) =>
        new Color(color.r * brightness, color.g * brightness, color.b * brightness, alpha);

    // 8가지 노드 상태를 색을 매기는 4단계로 줄인다 - 상태별 색을 8개씩 나열하면
    // 값이 늘어날 때 손이 여러 군데로 간다.
    private enum Emphasis
    {
        Completed,
        Available,

        /// <summary>선행·게이트는 통과했고 자원(또는 추가 코스트)만 모자란 상태.</summary>
        Short,

        Locked,
    }

    private static Emphasis EmphasisOf(ProgressionNodeState state)
    {
        return state switch
        {
            ProgressionNodeState.Completed => Emphasis.Completed,
            ProgressionNodeState.Available => Emphasis.Available,
            ProgressionNodeState.InsufficientResources => Emphasis.Short,
            ProgressionNodeState.InsufficientExtraCost => Emphasis.Short,
            _ => Emphasis.Locked,
        };
    }
}
