using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵 카메라의 줌/추적을 담당한다.
///   - 줌 버튼으로 orthographicSize 조절
///   - 축소 상태에 따라 메인 카메라 위치를 따라가거나(확대) 맵 전체를 보여준다(충분히 축소)
///   - 각 축(x/y)마다 카메라 절반 범위가 맵 절반 크기보다 작으면 경계 안으로 클램프하고,
///     더 축소되어 그 축의 카메라 범위가 맵보다 커지면 해당 축은 맵 중앙으로 고정한다.
/// </summary>
public class MinimapController : MonoBehaviour
{
    private const float CAMERA_DEPTH_Z = -10f;

    [Header("참조")]
    [SerializeField] private Camera _minimapCamera;
    [SerializeField] private Transform _followTarget;
    [SerializeField] private Button _zoomInButton;
    [SerializeField] private Button _zoomOutButton;

    [Header("맵 범위 (World 좌표, 육지 기준)")]
    [SerializeField] private Vector2 _mapCenter = new Vector2(0f, 0.25f);
    [SerializeField] private Vector2 _mapHalfSize = new Vector2(28f, 14f);

    [Header("줌")]
    [SerializeField] private float _minOrthoSize = 4f;
    [SerializeField] private float _maxOrthoSize = 32.2f;
    [SerializeField] private float _zoomStep = 5f;

    private float _currentOrthoSize;

    private void Awake()
    {
        _currentOrthoSize = _maxOrthoSize;
    }

    private void OnEnable()
    {
        if (_zoomInButton != null)
            _zoomInButton.onClick.AddListener(ZoomIn);

        if (_zoomOutButton != null)
            _zoomOutButton.onClick.AddListener(ZoomOut);
    }

    private void OnDisable()
    {
        if (_zoomInButton != null)
            _zoomInButton.onClick.RemoveListener(ZoomIn);

        if (_zoomOutButton != null)
            _zoomOutButton.onClick.RemoveListener(ZoomOut);
    }

    private void Update()
    {
        if (_minimapCamera == null)
            return;

        _minimapCamera.orthographicSize = _currentOrthoSize;

        Vector2 desiredPos = _followTarget != null
            ? new Vector2(_followTarget.position.x, _followTarget.position.y)
            : _mapCenter;

        Vector3 camPos = _minimapCamera.transform.position;
        camPos.x = ResolveAxis(desiredPos.x, _mapCenter.x, _mapHalfSize.x);
        camPos.y = ResolveAxis(desiredPos.y, _mapCenter.y, _mapHalfSize.y);
        camPos.z = CAMERA_DEPTH_Z;
        _minimapCamera.transform.position = camPos;
    }

    // 카메라 절반 범위(_currentOrthoSize)가 맵 절반 크기 안에 들어오면 목표 위치를 경계 안으로 클램프.
    // 절반 범위가 맵보다 커지면(많이 축소됨) 이 축은 클램프가 불가능하므로 맵 중앙으로 고정한다.
    private float ResolveAxis(float desiredPos, float mapCenterAxis, float mapHalfAxis)
    {
        float availableHalfRange = mapHalfAxis - _currentOrthoSize;
        if (availableHalfRange < 0f)
            return mapCenterAxis;

        return Mathf.Clamp(desiredPos, mapCenterAxis - availableHalfRange, mapCenterAxis + availableHalfRange);
    }

    public void ZoomIn() => SetZoom(_currentOrthoSize - _zoomStep);
    public void ZoomOut() => SetZoom(_currentOrthoSize + _zoomStep);

    private void SetZoom(float size) => _currentOrthoSize = Mathf.Clamp(size, _minOrthoSize, _maxOrthoSize);
}
