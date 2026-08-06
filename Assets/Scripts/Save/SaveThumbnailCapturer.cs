using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 세이브 슬롯 목록에 띄울 점령 현황 썸네일을 한 장 렌더한다.
///
/// 화면을 그대로 찍지 않고 전용 카메라로 다시 그리는 이유: 메인 카메라는 orthographicSize가 5라
/// 맵의 극히 일부만 보고 있어, 화면 캡처로는 "지금 어디까지 점령했는가"가 슬롯마다 구분되지 않는다.
///
/// 구도는 점령지(+원정 중인 청크)를 감싸는 정사각형이다. 맵 전체는 가로로 긴 2:1이라
/// 정사각 프레임(Save_window의 ScreenShotFrame)에 통째로 넣으면 위아래 절반이 여백이 되고
/// 청크 하나가 십수 픽셀까지 작아진다. 점령지 기준으로 잡으면 초반에는 성 주변이 크게 보이고
/// 진행할수록 자연히 맵 전체로 넓어져, 썸네일만 보고도 진행도가 구분된다.
/// </summary>
public sealed class SaveThumbnailCapturer : MonoBehaviour
{
    // 표시는 120x120(Save_window)이지만 해상도 여유를 둔다. 정사각인 것이 중요하다.
    private const int THUMBNAIL_SIZE = 256;

    private const int DEPTH_BUFFER_BITS = 16;

    // 2D 씬의 카메라 z. MinimapCamera와 같은 값을 쓴다.
    private const float CAMERA_DEPTH_Z = -10f;

    // 점령지 경계에서 바깥으로 더 보여 줄 여백. 테두리가 프레임에 딱 붙지 않게 한다.
    private const float FRAME_MARGIN = 3f;

    // 점령지가 한 청크뿐일 때 지나치게 확대되어 무엇을 보는지 알 수 없게 되는 것을 막는다.
    private const float MIN_HALF_EXTENT = 8f;

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

        if (!TryResolveFrame(out Vector3 center, out float halfExtent))
        {
            return false;
        }

        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Camera camera = CreateCaptureCamera(center, halfExtent, out cameraObject);

            renderTexture = RenderTexture.GetTemporary(
                THUMBNAIL_SIZE,
                THUMBNAIL_SIZE,
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

            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture.active = renderTexture;
            texture = new Texture2D(THUMBNAIL_SIZE, THUMBNAIL_SIZE, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, THUMBNAIL_SIZE, THUMBNAIL_SIZE), 0, 0);
            texture.Apply();

            pngBytes = texture.EncodeToPNG();
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

    private Camera CreateCaptureCamera(Vector3 center, float halfExtent, out GameObject cameraObject)
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
        camera.orthographicSize = halfExtent;
        camera.cullingMask = _captureLayers;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = _backgroundColor;

        // URP 2D는 이 컴포넌트가 없으면 렌더러 설정을 찾지 못한다.
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        return camera;
    }

    /// <summary>
    /// 점령지(+원정 중인 청크)를 감싸는 정사각 구도를 계산한다.
    /// 아직 아무것도 점령하지 않았다면 맵 전체로 폴백한다.
    /// </summary>
    private bool TryResolveFrame(out Vector3 center, out float halfExtent)
    {
        center = Vector3.zero;
        halfExtent = MIN_HALF_EXTENT;

        if (!TryResolveMapBounds(out Bounds mapBounds))
        {
            return false;
        }

        if (!TryResolveTerritoryBounds(out Bounds territoryBounds))
        {
            territoryBounds = mapBounds;
        }

        halfExtent = Mathf.Max(
            MIN_HALF_EXTENT,
            Mathf.Max(territoryBounds.extents.x, territoryBounds.extents.y) + FRAME_MARGIN);

        // 점령지가 맵 가장자리에 치우쳐 있어도 프레임이 맵 밖으로 흘러 여백만 찍히지 않도록
        // 축마다 맵 경계 안으로 되돌린다(MinimapController.ResolveAxis와 같은 규칙).
        center = new Vector3(
            ResolveAxis(territoryBounds.center.x, mapBounds.center.x, mapBounds.extents.x, halfExtent),
            ResolveAxis(territoryBounds.center.y, mapBounds.center.y, mapBounds.extents.y, halfExtent),
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

    private bool TryResolveMapBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            EncapsulateChunk(chunk, ref bounds, ref hasAny);
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
