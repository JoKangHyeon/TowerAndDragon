using UnityEngine;

// 연구 트리 티어 구분선의 점선 무늬를 코드로 만든다.
//
// 에셋으로 두지 않고 만드는 이유: 이런 작은 반복 패턴은 임포트 설정(압축·밉맵·필터)에 따라
// 뭉개지기 쉬워 저작물로 관리하면 오히려 잘 깨진다. 여기서 만들면 항상 같은 그림이 나온다.
//
// 노드 카드의 둥근 모서리·테두리·그림자도 여기서 만들었지만, 생김새를 프리팹
// (ResearchNode.prefab)이 맡게 되면서 걷어냈다.
// 노드를 잇는 연결선도 점선이었으나 실선으로 바뀌어(UI_ResearchEdge) 더는 쓰지 않는다 -
// 남은 점선은 티어 행 사이 구분선 하나뿐이다.
public static class ResearchTreeSprites
{
    private const int DASH_ON_PIXELS = 6;
    private const int DASH_OFF_PIXELS = 6;

    // Canvas.referencePixelsPerUnit 기본값과 맞춰야 스프라이트 1픽셀이 UI 1픽셀이 된다.
    private const float PIXELS_PER_UNIT = 100f;

    // 점선 한 마디의 길이(칠 + 빔).
    private static float DashPeriod => DASH_ON_PIXELS + DASH_OFF_PIXELS;

    private static Texture2D _dashTexture;
    private static Sprite _dashSprite;

    // 점선 스프라이트의 원본 텍스처. wrap이 Repeat라 Tiled Image가 이어 붙일 수 있다.
    private static Texture2D DashTexture =>
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
            DashTexture,
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
