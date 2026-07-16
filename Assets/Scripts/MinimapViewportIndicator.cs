using UnityEngine;

/// <summary>
/// 메인 카메라(게임 화면)가 비추는 월드 영역을 미니맵에 사각형 테두리로 표시한다.
///   - 미니맵 전용 레이어에 놓인 LineRenderer의 네 꼭짓점을
///     메인 카메라 오쏘그래픽 뷰포트에 맞춰 매 프레임 갱신
///   - 선 굵기는 미니맵 줌 수준과 무관하게 일정한 픽셀 두께를 유지
///   - 밝은 안쪽 선 + 검은 외곽선(이중 테두리)으로 어떤 지형 위에서도 잘 보이게 한다
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MinimapViewportIndicator : MonoBehaviour
{
    private const int CORNER_COUNT = 4;

    [Header("참조")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _minimapCamera;
    [Tooltip("안쪽 선 뒤에 깔리는 외곽선용 LineRenderer (선택)")]
    [SerializeField] private LineRenderer _outlineRenderer;

    [Header("표시")]
    [Tooltip("미니맵 렌더텍스처 기준 안쪽 선 두께 (픽셀)")]
    [SerializeField] private float _lineWidthPixels = 2f;
    [Tooltip("안쪽 선 좌우로 드러나는 외곽선 두께 (픽셀, 한쪽 기준)")]
    [SerializeField] private float _outlineWidthPixels = 1f;

    private LineRenderer _lineRenderer;
    private readonly Vector3[] _corners = new Vector3[CORNER_COUNT];

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        ConfigureLineRenderer(_lineRenderer);
        if (_outlineRenderer != null)
            ConfigureLineRenderer(_outlineRenderer);

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    private static void ConfigureLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = CORNER_COUNT;
    }

    // 카메라 이동(CameraController)과 미니맵 추적(MinimapController)이 모두 Update에서
    // 끝난 뒤의 최종 상태를 반영하기 위해 LateUpdate에서 갱신한다.
    private void LateUpdate()
    {
        if (_mainCamera == null || _minimapCamera == null)
            return;

        UpdateRect();
        UpdateLineWidth();
    }

    /// 메인 카메라 오쏘그래픽 뷰포트의 월드 사각형을 LineRenderer 네 꼭짓점에 반영
    private void UpdateRect()
    {
        Vector3 center     = _mainCamera.transform.position;
        float   halfHeight = _mainCamera.orthographicSize;
        float   halfWidth  = halfHeight * _mainCamera.aspect;
        float   z          = transform.position.z;

        int i = 0;
        _corners[i++] = new Vector3(center.x - halfWidth, center.y - halfHeight, z);
        _corners[i++] = new Vector3(center.x - halfWidth, center.y + halfHeight, z);
        _corners[i++] = new Vector3(center.x + halfWidth, center.y + halfHeight, z);
        _corners[i++] = new Vector3(center.x + halfWidth, center.y - halfHeight, z);
        _lineRenderer.SetPositions(_corners);
        if (_outlineRenderer != null)
            _outlineRenderer.SetPositions(_corners);
    }

    /// 미니맵 줌과 무관하게 화면상 두께가 일정하도록 월드 단위 선 굵기를 환산
    private void UpdateLineWidth()
    {
        float rtHeight = _minimapCamera.targetTexture != null
            ? _minimapCamera.targetTexture.height
            : _minimapCamera.pixelHeight;

        float worldPerPixel = _minimapCamera.orthographicSize * 2f / rtHeight;
        _lineRenderer.widthMultiplier = worldPerPixel * _lineWidthPixels;

        // 외곽선은 안쪽 선 양옆으로 _outlineWidthPixels씩 드러나도록 더 두껍게
        if (_outlineRenderer != null)
            _outlineRenderer.widthMultiplier = worldPerPixel * (_lineWidthPixels + _outlineWidthPixels * 2f);
    }
}
