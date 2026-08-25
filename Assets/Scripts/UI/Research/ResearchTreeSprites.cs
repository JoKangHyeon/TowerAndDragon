using UnityEngine;

// 연구 트리 연결선의 점선 무늬를 코드로 만든다. 기획 블랙보드(Docs/연구트리_블랙보드.html)의
// .connection-path stroke-dasharray 6 을 uGUI로 옮긴 것이다.
//
// 에셋으로 두지 않고 만드는 이유: 이런 작은 반복 패턴은 임포트 설정(압축·밉맵·필터)에 따라
// 뭉개지기 쉬워 저작물로 관리하면 오히려 잘 깨진다. 여기서 만들면 항상 같은 그림이 나온다.
//
// 노드 카드의 둥근 모서리·테두리·그림자도 여기서 만들었지만, 생김새를 프리팹
// (ResearchNode.prefab)이 맡게 되면서 걷어냈다.
public static class ResearchTreeSprites
{
    private const int DASH_ON_PIXELS = 6;
    private const int DASH_OFF_PIXELS = 6;

    // Canvas.referencePixelsPerUnit 기본값과 맞춰야 스프라이트 1픽셀이 UI 1픽셀이 된다.
    private const float PIXELS_PER_UNIT = 100f;

    /// <summary>점선 한 마디의 길이(칠 + 빔). 곡선 연결선이 UV 주기로 쓴다.</summary>
    public static float DashPeriod => DASH_ON_PIXELS + DASH_OFF_PIXELS;

    private static Texture2D _dashTexture;
    private static Sprite _dashSprite;

    /// <summary>곡선 연결선용 점선 텍스처. u축(가로)으로 흐르고 wrap이 Repeat라 UV로 이어 붙는다.</summary>
    public static Texture DashTexture =>
        _dashTexture != null ? _dashTexture : _dashTexture = CreateDashTexture();

    /// <summary>직선 점선(Image.type = Tiled 전용). 블랙보드의 주기 구분선 dashed 선에 쓴다.</summary>
    public static Sprite DashSprite =>
        _dashSprite != null ? _dashSprite : _dashSprite = CreateDashSprite();

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
