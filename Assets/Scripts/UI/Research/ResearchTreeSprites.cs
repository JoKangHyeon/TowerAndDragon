using UnityEngine;

// 연구 트리가 쓰는 스프라이트를 코드로 만든다. 기획 블랙보드(Docs/연구트리_블랙보드.html)의
// CSS 표현을 uGUI로 옮기기 위한 것이다.
//   - .node 의 border-radius 8px / border 2px / box-shadow -> 9슬라이스 둥근 사각형 3종
//   - .connection-path 의 stroke-dasharray 6               -> 점선 텍스처
//
// 에셋으로 두지 않고 만드는 이유: 이런 작은 패턴·모서리 텍스처는 임포트 설정(압축·밉맵·필터)에
// 따라 뭉개지기 쉬워 저작물로 관리하면 오히려 잘 깨진다. 여기서 만들면 항상 같은 그림이 나온다.
public static class ResearchTreeSprites
{
    private const int DASH_ON_PIXELS = 6;
    private const int DASH_OFF_PIXELS = 6;

    // 블랙보드 .node 의 border-radius / border-width.
    private const int CARD_RADIUS = 8;
    private const float CARD_BORDER = 2f;

    // box-shadow 의 blur 6px.
    private const int CARD_SHADOW_BLUR = 6;

    // Canvas.referencePixelsPerUnit 기본값과 맞춰야 스프라이트 1픽셀이 UI 1픽셀이 된다.
    private const float PIXELS_PER_UNIT = 100f;

    /// <summary>점선 한 마디의 길이(칠 + 빔). 곡선 연결선이 UV 주기로 쓴다.</summary>
    public static float DashPeriod => DASH_ON_PIXELS + DASH_OFF_PIXELS;

    private static Texture2D _dashTexture;
    private static Sprite _dashSprite;
    private static Sprite _cardFill;
    private static Sprite _cardOutline;
    private static Sprite _cardShadow;

    /// <summary>곡선 연결선용 점선 텍스처. u축(가로)으로 흐르고 wrap이 Repeat라 UV로 이어 붙는다.</summary>
    public static Texture DashTexture =>
        _dashTexture != null ? _dashTexture : _dashTexture = CreateDashTexture();

    /// <summary>직선 점선(Image.type = Tiled 전용). 블랙보드의 주기 구분선 dashed 선에 쓴다.</summary>
    public static Sprite DashSprite =>
        _dashSprite != null ? _dashSprite : _dashSprite = CreateDashSprite();

    /// <summary>둥근 사각형 채움. Image.type = Sliced로 쓴다.</summary>
    public static Sprite CardFill =>
        _cardFill != null ? _cardFill : _cardFill = CreateRounded(RoundedKind.Fill);

    /// <summary>둥근 사각형 테두리(2px 링).</summary>
    public static Sprite CardOutline =>
        _cardOutline != null ? _cardOutline : _cardOutline = CreateRounded(RoundedKind.Outline);

    /// <summary>둥근 사각형 그림자(바깥으로 흐려진다).</summary>
    public static Sprite CardShadow =>
        _cardShadow != null ? _cardShadow : _cardShadow = CreateRounded(RoundedKind.Shadow);

    /// <summary>그림자 스프라이트가 카드 밖으로 번지는 폭. 배치할 때 그만큼 키워 준다.</summary>
    public static float CardShadowBlur => CARD_SHADOW_BLUR;

    private enum RoundedKind
    {
        Fill,
        Outline,
        Shadow,
    }

    private static Texture2D CreateDashTexture()
    {
        int period = DASH_ON_PIXELS + DASH_OFF_PIXELS;
        var texture = NewTexture(period, 1, FilterMode.Point, TextureWrapMode.Repeat);

        for (int x = 0; x < period; x++)
        {
            texture.SetPixel(x, 0, x < DASH_ON_PIXELS ? Color.white : Color.clear);
        }

        texture.Apply();
        return texture;
    }

    // Tiled Image가 가로로 반복해 깔 점선 한 마디. Sliced가 아니라 Tiled이므로 테두리는 0이다.
    private static Sprite CreateDashSprite()
    {
        Sprite sprite = Sprite.Create(
            (Texture2D)DashTexture,
            new Rect(0f, 0f, DashPeriod, 1f),
            new Vector2(0.5f, 0.5f),
            PIXELS_PER_UNIT,
            0,
            SpriteMeshType.FullRect);

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // 9슬라이스용 둥근 사각형. 가운데 2px만 늘어나도록 테두리를 잡는다.
    // 모서리는 거리장(SDF)으로 계산해 알파를 매겨 톱니가 보이지 않게 한다.
    private static Sprite CreateRounded(RoundedKind kind)
    {
        int pad = kind == RoundedKind.Shadow ? CARD_RADIUS + CARD_SHADOW_BLUR : CARD_RADIUS;
        int size = pad * 2 + 2;
        var texture = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);

        float half = size * 0.5f;
        float inset = kind == RoundedKind.Shadow ? CARD_SHADOW_BLUR : 0f;
        float boxHalf = half - inset - CARD_RADIUS;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float distance = RoundedBoxDistance(point, boxHalf, CARD_RADIUS);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, ResolveAlpha(kind, distance)));
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            PIXELS_PER_UNIT,
            0,
            SpriteMeshType.FullRect,
            new Vector4(pad, pad, pad, pad));

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static float ResolveAlpha(RoundedKind kind, float distance)
    {
        switch (kind)
        {
            case RoundedKind.Outline:
                // 도형 안쪽으로 CARD_BORDER 두께만 남긴다.
                return Mathf.Clamp01(0.5f - distance) -
                       Mathf.Clamp01(0.5f - (distance + CARD_BORDER));

            case RoundedKind.Shadow:
                // 경계 밖으로 부드럽게 흐려진다.
                return Mathf.Clamp01(1f - distance / CARD_SHADOW_BLUR);

            case RoundedKind.Fill:
            default:
                // 경계에서 1픽셀 안티에일리어싱.
                return Mathf.Clamp01(0.5f - distance);
        }
    }

    // 둥근 사각형까지의 부호 있는 거리. 안쪽이 음수다.
    private static float RoundedBoxDistance(Vector2 point, float boxHalf, float radius)
    {
        float qx = Mathf.Abs(point.x) - boxHalf;
        float qy = Mathf.Abs(point.y) - boxHalf;

        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);

        return outside + inside - radius;
    }

    private static Texture2D NewTexture(int width, int height, FilterMode filterMode, TextureWrapMode wrapMode)
    {
        // 인자 이름이 유니티 버전마다 mipmap/mipChain으로 갈려 위치 인자로 넘긴다.
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = filterMode,
            wrapMode = wrapMode,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
