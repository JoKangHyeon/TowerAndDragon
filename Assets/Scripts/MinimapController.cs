using UnityEngine;
using UnityEngine.Tilemaps;
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

    [Header("맵 범위 자동 계산")]
    [Tooltip("지정 시 시작할 때 타일맵 실제 경계로 맵 범위를 덮어씀. 비우면 아래 수동 값 사용")]
    [SerializeField] private Tilemap _boundsTilemap;
    [Tooltip("경계 계산에서 제외할 타일 (예: 바다). 지정 시 나머지 타일 기준으로 경계 계산")]
    [SerializeField] private TileBase _excludedBoundsTile;
    [Tooltip("계산된 경계를 바깥으로 넓히는 여유 — 서쪽(x)·남쪽(y) 방향 (World 단위)")]
    [SerializeField] private Vector2 _boundsMarginMin = Vector2.zero;
    [Tooltip("계산된 경계를 바깥으로 넓히는 여유 — 동쪽(x)·북쪽(y) 방향 (World 단위)")]
    [SerializeField] private Vector2 _boundsMarginMax = Vector2.zero;

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
        InitMapBounds();
    }

    /// _boundsTilemap이 지정된 경우 타일맵 실제 월드 경계로 _mapCenter/_mapHalfSize를 갱신.
    /// 계산 불가 시 인스펙터 수동 값을 그대로 사용한다.
    private void InitMapBounds()
    {
        if (!TilemapBoundsCalculator.TryCalculate(_boundsTilemap, _excludedBoundsTile, out Vector2 min, out Vector2 max))
            return;

        min -= _boundsMarginMin;
        max += _boundsMarginMax;

        _mapCenter   = (min + max) * 0.5f;
        _mapHalfSize = (max - min) * 0.5f;
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

        // 뷰포트 절반 크기 — 세로는 orthographicSize, 가로는 종횡비 반영
        float halfHeight = _currentOrthoSize;
        float halfWidth  = halfHeight * _minimapCamera.aspect;

        Vector3 camPos = _minimapCamera.transform.position;
        camPos.x = ResolveAxis(desiredPos.x, _mapCenter.x, _mapHalfSize.x, halfWidth);
        camPos.y = ResolveAxis(desiredPos.y, _mapCenter.y, _mapHalfSize.y, halfHeight);
        camPos.z = CAMERA_DEPTH_Z;
        _minimapCamera.transform.position = camPos;
    }

    // 카메라 절반 범위(halfView)가 맵 절반 크기 안에 들어오면 목표 위치를 경계 안으로 클램프.
    // 절반 범위가 맵보다 커지면(많이 축소됨) 이 축은 클램프가 불가능하므로 맵 중앙으로 고정한다.
    private float ResolveAxis(float desiredPos, float mapCenterAxis, float mapHalfAxis, float halfView)
    {
        float availableHalfRange = mapHalfAxis - halfView;
        if (availableHalfRange < 0f)
            return mapCenterAxis;

        return Mathf.Clamp(desiredPos, mapCenterAxis - availableHalfRange, mapCenterAxis + availableHalfRange);
    }

    /// <summary>
    /// 안내가 도는 동안 미니맵 조작을 막는 관문. 배선되지 않으면 null로 남아 늘 허용된다.
    /// </summary>
    public IHudControlBlockQuery BlockQuery { get; set; }

    public void ZoomIn()
    {
        if (IsBlocked)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        SetZoom(_currentOrthoSize - _zoomStep);
    }

    public void ZoomOut()
    {
        if (IsBlocked)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        SetZoom(_currentOrthoSize + _zoomStep);
    }

    // 막혔으면 클릭음도 내지 않는다 - 소리가 나면 눌린 줄 알고 다시 누르게 된다.
    private bool IsBlocked => BlockQuery != null && !BlockQuery.CanUseHudControl();

    private void SetZoom(float size) => _currentOrthoSize = Mathf.Clamp(size, _minOrthoSize, _maxOrthoSize);
}
