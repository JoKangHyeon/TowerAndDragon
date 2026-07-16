using UnityEngine;

/// <summary>
/// 메인 카메라(게임 화면)가 비추는 월드 영역을 미니맵에 사각형 테두리로 표시한다.
///   - 미니맵 전용 레이어에 놓인 LineRenderer의 네 꼭짓점을
///     메인 카메라 오쏘그래픽 뷰포트에 맞춰 매 프레임 갱신
///   - 선 굵기는 미니맵 줌 수준과 무관하게 일정한 픽셀 두께를 유지
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MinimapViewportIndicator : MonoBehaviour
{
    private const int CORNER_COUNT = 4;

    [Header("참조")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _minimapCamera;

    [Header("표시")]
    [Tooltip("미니맵 렌더텍스처 기준 테두리 두께 (픽셀)")]
    [SerializeField] private float _lineWidthPixels = 2f;

    private LineRenderer _lineRenderer;
    private readonly Vector3[] _corners = new Vector3[CORNER_COUNT];

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = CORNER_COUNT;

        if (_mainCamera == null)
            _mainCamera = Camera.main;
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
    }

    /// 미니맵 줌과 무관하게 화면상 두께가 일정하도록 월드 단위 선 굵기를 환산
    private void UpdateLineWidth()
    {
        float rtHeight = _minimapCamera.targetTexture != null
            ? _minimapCamera.targetTexture.height
            : _minimapCamera.pixelHeight;

        float worldPerPixel = _minimapCamera.orthographicSize * 2f / rtHeight;
        _lineRenderer.widthMultiplier = worldPerPixel * _lineWidthPixels;
    }
}
