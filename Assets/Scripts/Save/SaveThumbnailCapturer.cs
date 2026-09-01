using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 세이브 슬롯 목록에 띄울 점령 현황 썸네일을 한 장 렌더한다.
///
/// 화면을 그대로 찍지 않고 전용 카메라로 다시 그리는 이유: 메인 카메라는 orthographicSize가 5라
/// 맵의 극히 일부만 보고 있어, 화면 캡처로는 "지금 어디까지 점령했는가"가 슬롯마다 구분되지 않는다.
///
/// 구도는 점령지(+원정 중인 청크)를 감싸는 2:1 직사각형이다. 아이소메트릭 격자(셀 1x0.5)에서는
/// 타일 영역의 월드 경계가 어떤 모양으로 칠해도 가로:세로 2:1이 되므로, 프레임도 같은 비율이어야
/// 위아래에 배경색 띠가 남지 않는다. 정사각으로 찍던 동안은 완전 점령 때뿐 아니라 모든 슬롯에서
/// 세로 절반가량이 늘 비어 있었다 - 점령지 경계도 마름모라 항상 가로가 더 길기 때문이다.
///
/// 맵 전체가 아니라 점령지 기준으로 잡는 이유: 초반에는 성 주변이 크게 보이고 진행할수록 자연히
/// 맵 전체로 넓어져, 썸네일만 보고도 진행도가 구분된다.
/// </summary>
public sealed class SaveThumbnailCapturer : MonoBehaviour
{
    // 표시는 160x80(Slot_SaveSlot의 Left_Slot)이지만 해상도 여유를 둔다. 2:1인 것이 중요하다.
    private const int THUMBNAIL_WIDTH = 256;
    private const int THUMBNAIL_HEIGHT = 128;

    private const float THUMBNAIL_ASPECT = (float)THUMBNAIL_WIDTH / THUMBNAIL_HEIGHT;

    private const int DEPTH_BUFFER_BITS = 16;

    // 2D 씬의 카메라 z. MinimapCamera와 같은 값을 쓴다.
    private const float CAMERA_DEPTH_Z = -10f;

    // 점령지 경계에서 바깥으로 더 보여 줄 여백. 테두리가 프레임에 딱 붙지 않게 한다.
    private const float FRAME_MARGIN = 3f;

    // 점령지가 한 청크뿐일 때 지나치게 확대되어 무엇을 보는지 알 수 없게 되는 것을 막는다.
    // 세로 반범위 기준이므로 가로로는 이 값의 THUMBNAIL_ASPECT배까지 보인다.
    private const float MIN_HALF_HEIGHT = 4f;

    // [임시 계측] 밤→낮 전환 프리즈 조사용. CycleManager.cs의 TND. 명명 규칙을 그대로 따른다.
    private const string RESOLVE_FRAME_MARKER_NAME = "TND.Thumb.ResolveFrame";
    private const string RENDER_MARKER_NAME = "TND.Thumb.Render";
    private const string READBACK_MARKER_NAME = "TND.Thumb.Readback";
    private const string ENCODE_MARKER_NAME = "TND.Thumb.Encode";

    private static readonly ProfilerMarker RESOLVE_FRAME_MARKER = new(RESOLVE_FRAME_MARKER_NAME);
    private static readonly ProfilerMarker RENDER_MARKER = new(RENDER_MARKER_NAME);
    private static readonly ProfilerMarker READBACK_MARKER = new(READBACK_MARKER_NAME);
    private static readonly ProfilerMarker ENCODE_MARKER = new(ENCODE_MARKER_NAME);

    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ConquestManager _conquestManager;

    [Tooltip("썸네일에 담을 레이어. UI와 MinimapOnly는 해제한다(캔버스와 미니맵 뷰포트 표시선 제외).")]
    [SerializeField] private LayerMask _captureLayers = ~0;

    [Tooltip("맵 바깥 여백 색. 메인 카메라의 배경색과 맞추면 자연스럽다.")]
    [SerializeField] private Color _backgroundColor = new Color(0.192f, 0.302f, 0.475f, 1f);

    /// <summary>
    /// 썸네일을 PNG 바이트로 렌더한다. 반드시 메인 스레드에서 동기로 부른다(카메라·텍스처를 만진다).
    /// 실패는 저장 자체를 막지 않는다 - 호출자는 null로 진행하면 된다.
    /// </summary>
    public bool TryCapturePng(out byte[] pngBytes)
    {
        pngBytes = null;

        if (_gridMap == null)
        {
            Debug.LogWarning("[SaveThumbnailCapturer] GridMap이 없어 썸네일을 건너뜁니다.");
            return false;
        }

        bool hasFrame;
        Vector3 center;
        float halfHeight;
        using (RESOLVE_FRAME_MARKER.Auto())
        {
            hasFrame = TryResolveFrame(out center, out halfHeight);
        }

        if (!hasFrame)
        {
            return false;
        }

        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Camera camera = CreateCaptureCamera(center, halfHeight, out cameraObject);

            renderTexture = RenderTexture.GetTemporary(
                THUMBNAIL_WIDTH,
                THUMBNAIL_HEIGHT,
                DEPTH_BUFFER_BITS,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);

            // URP에서 카메라 하나를 지정한 타깃으로 1회만 그리는 정식 경로.
            // camera.Render()와 달리 카메라의 targetTexture를 건드리지 않는다.
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };

            if (!RenderPipeline.SupportsRenderRequest(camera, request))
            {
                Debug.LogError("[SaveThumbnailCapturer] 현재 렌더 파이프라인이 단일 카메라 렌더 요청을 지원하지 않습니다.");
                return false;
            }

            using (RENDER_MARKER.Auto())
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }

            using (READBACK_MARKER.Auto())
            {
                RenderTexture.active = renderTexture;
                texture = new Texture2D(THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT), 0, 0);
                texture.Apply();
            }

            using (ENCODE_MARKER.Auto())
            {
                pngBytes = texture.EncodeToPNG();
            }

            return pngBytes != null;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }

            if (texture != null)
            {
                Destroy(texture);
            }

            if (cameraObject != null)
            {
                Destroy(cameraObject);
            }
        }
    }

    private Camera CreateCaptureCamera(Vector3 center, float halfHeight, out GameObject cameraObject)
    {
        cameraObject = new GameObject(nameof(SaveThumbnailCapturer))
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        cameraObject.transform.position = new Vector3(center.x, center.y, CAMERA_DEPTH_Z);

        Camera camera = cameraObject.AddComponent<Camera>();

        // 매 프레임 자동으로 그리지 않게 한다 - 저장 시점에 수동으로 한 번만 렌더한다.
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = halfHeight;

        // targetTexture를 쓰지 않고 SingleCameraRequest의 destination으로만 그리므로 카메라는
        // 렌더 타깃의 종횡비를 알지 못한다 - 명시하지 않으면 화면 종횡비로 투영되어 가로가 어긋난다.
        camera.aspect = THUMBNAIL_ASPECT;

        camera.cullingMask = _captureLayers;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = _backgroundColor;

        // URP 2D는 이 컴포넌트가 없으면 렌더러 설정을 찾지 못한다.
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        return camera;
    }

    /// <summary>
    /// 점령지(+원정 중인 청크)를 감싸는 2:1 구도를 계산한다. 돌려주는 값은 카메라 세로 반범위
    /// (= orthographicSize)이며, 가로는 카메라가 THUMBNAIL_ASPECT를 곱해 잡는다.
    /// 아직 아무것도 점령하지 않았다면 맵 전체로 폴백한다.
    /// </summary>
    private bool TryResolveFrame(out Vector3 center, out float halfHeight)
    {
        center = Vector3.zero;
        halfHeight = MIN_HALF_HEIGHT;

        if (!TryResolveMapBounds(out Bounds mapBounds))
        {
            return false;
        }

        if (!TryResolveTerritoryBounds(out Bounds territoryBounds))
        {
            territoryBounds = mapBounds;
        }

        // 두 축이 각자 요구하는 세로 반범위 중 큰 쪽을 쓴다 - 가로 요구치는 종횡비로 나눠 세로 기준으로 환산한다.
        // 여백은 축마다 따로 더해, 세로로 좁고 가로로 긴 점령지에서도 테두리가 프레임에 붙지 않게 한다.
        halfHeight = Mathf.Max(
            MIN_HALF_HEIGHT,
            Mathf.Max(
                territoryBounds.extents.y + FRAME_MARGIN,
                (territoryBounds.extents.x + FRAME_MARGIN) / THUMBNAIL_ASPECT));

        // 점령지가 맵 가장자리에 치우쳐 있어도 프레임이 맵 밖으로 흘러 여백만 찍히지 않도록
        // 축마다 맵 경계 안으로 되돌린다(MinimapController.ResolveAxis와 같은 규칙).
        float halfWidth = halfHeight * THUMBNAIL_ASPECT;

        center = new Vector3(
            ResolveAxis(territoryBounds.center.x, mapBounds.center.x, mapBounds.extents.x, halfWidth),
            ResolveAxis(territoryBounds.center.y, mapBounds.center.y, mapBounds.extents.y, halfHeight),
            0f);

        return true;
    }

    // 카메라 절반 범위(halfView)가 맵 절반 크기 안에 들어오면 경계 안으로 클램프하고,
    // 맵보다 커지면 클램프가 불가능하므로 맵 중앙으로 고정한다.
    private static float ResolveAxis(float desiredPos, float mapCenterAxis, float mapHalfAxis, float halfView)
    {
        float availableHalfRange = mapHalfAxis - halfView;

        if (availableHalfRange < 0f)
        {
            return mapCenterAxis;
        }

        return Mathf.Clamp(desiredPos, mapCenterAxis - availableHalfRange, mapCenterAxis + availableHalfRange);
    }

    // 맵 경계는 GridMap.Awake 이후 게임 내내 바뀌지 않으므로 최초 1회만 계산해 캐시한다 -
    // 저장할 때마다(하루에 한 번 이상) 맵 전체 셀을 좌표 변환하는 비용을 없앤다.
    private bool _hasMapBoundsCache;
    private Bounds _mapBoundsCache;

    private bool TryResolveMapBounds(out Bounds bounds)
    {
        if (_hasMapBoundsCache)
        {
            bounds = _mapBoundsCache;
            return true;
        }

        bounds = default;
        bool hasAny = false;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            EncapsulateChunk(chunk, ref bounds, ref hasAny);
        }

        if (hasAny)
        {
            _mapBoundsCache = bounds;
            _hasMapBoundsCache = true;
        }

        return hasAny;
    }

    private bool TryResolveTerritoryBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            if (chunk.CurrentState != ChunkState.Conquered)
            {
                continue;
            }

            EncapsulateChunk(chunk, ref bounds, ref hasAny);
        }

        // 원정 중인 청크도 회색 테두리로 그려지므로 구도에 포함한다 - 다음 목표가 어디인지가 함께 남는다.
        if (_conquestManager != null)
        {
            foreach (ConquestExpedition expedition in _conquestManager.ActiveExpeditions)
            {
                EncapsulateChunk(_gridMap.GetChunk(expedition.TargetChunkCoord), ref bounds, ref hasAny);
            }
        }

        return hasAny;
    }

    // 청크 중심이 아니라 셀 하나하나의 월드 좌표를 넣는다 - 아이소메트릭이라 청크가 마름모꼴이어서
    // 중심점만으로는 실제로 차지하는 화면 영역이 나오지 않는다.
    private void EncapsulateChunk(Chunk chunk, ref Bounds bounds, ref bool hasAny)
    {
        if (chunk == null)
        {
            return;
        }

        foreach (GridCell cell in chunk.Cells)
        {
            Vector3 worldPosition = _gridMap.ConvertGridToWorld(cell.Coord);

            if (!hasAny)
            {
                bounds = new Bounds(worldPosition, Vector3.zero);
                hasAny = true;
                continue;
            }

            bounds.Encapsulate(worldPosition);
        }
    }
}
