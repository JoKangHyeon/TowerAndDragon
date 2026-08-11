using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// 2D 아이소메트릭 타워 디펜스 카메라 컨트롤러
/// 지원 기능: (모두 Input Action 시스템 사용하는걸로 변경)
///   - WASD / 방향키 이동
///   - 마우스 엣지 스크롤 (토글 가능)
///   - 미들 마우스 드래그 이동
///   - 마우스 휠 줌 (Orthographic Size)
///   - 카메라 위치 북마크 (Ctrl+1~5 저장, 1~5 복귀)
///   - MinimapController 연동 (MoveTo 공개 메서드)
///   - 맵 경계 클램프 + 부드러운 이동(SmoothDamp)
///
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private const float SCROLL_DEADZONE     = 0.01f;
    private const float SCROLL_TO_ZOOM_SCALE = 0.01f;
    private const int   BOOKMARK_COUNT      = 5;

    // ─────────────────────────────────────────────
    // Inspector 설정
    // ─────────────────────────────────────────────

    [Header("Input Actions")]
    [SerializeField] private InputActionReference _cameraMoveAction;

    [Header("이동 속도")]
    [SerializeField] private float _wasdSpeed      = 12f;
    [SerializeField] private float _edgeScrollSpeed = 12f;
    [SerializeField] private float _dragSensitivity = 1f;  // 드래그 배율 (1 = 1:1)

    [Header("엣지 스크롤")]
    [SerializeField] private bool  _edgeScrollEnabled   = true;
    [SerializeField] private float _edgeScrollThreshold = 20f; // 픽셀

    [Header("줌")]
    [SerializeField] private float _zoomSpeed    = 3f;
    [SerializeField] private float _minZoom      = 3f;
    [SerializeField] private float _maxZoom      = 18f;
    [SerializeField] private float _zoomSmoothing = 8f;

    [Header("이동 스무딩")]
    [SerializeField] private float _moveSmoothTime = 0.08f;

    [Header("맵 경계 (World 좌표)")]
    [Tooltip("지정 시 시작할 때 타일맵 실제 경계로 _mapMin/_mapMax를 덮어씀. 비우면 아래 수동 값 사용")]
    [SerializeField] private Tilemap _boundsTilemap;
    [Tooltip("경계 계산에서 제외할 타일 (예: 바다). 지정 시 나머지 타일 기준으로 경계 계산")]
    [SerializeField] private TileBase _excludedBoundsTile;
    [Tooltip("계산된 경계를 바깥으로 넓히는 여유 — 서쪽(x)·남쪽(y) 방향 (World 단위)")]
    [SerializeField] private Vector2 _boundsMarginMin = Vector2.zero;
    [Tooltip("계산된 경계를 바깥으로 넓히는 여유 — 동쪽(x)·북쪽(y) 방향 (World 단위)")]
    [SerializeField] private Vector2 _boundsMarginMax = Vector2.zero;
    [SerializeField] private Vector2 _mapMin = new Vector2(-60f, -60f);
    [SerializeField] private Vector2 _mapMax = new Vector2( 60f,  60f);

    // ─────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────

    private Camera   _cam;
    private Vector3  _targetPos;          // 목표 위치 (SmoothDamp 대상)
    private float    _targetZoom;         // 목표 줌
    private Vector3  _smoothVelocity;     // SmoothDamp 내부 velocity

    // 미들 마우스 드래그
    private bool    _isDragging;
    private Vector3 _dragWorldOrigin;

    private bool _isDragEndInEdge;

    // 북마크 (0~4 = 단축키 1~5)
    private Vector3?[] _bookmarks = new Vector3?[BOOKMARK_COUNT];

    // ─────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _cam        = GetComponent<Camera>();
        _targetPos  = transform.position;
        _targetZoom = _cam.orthographicSize;
        InitMapBounds();
    }

    /// _boundsTilemap이 지정된 경우 타일맵 실제 월드 경계로 _mapMin/_mapMax를 갱신.
    /// _excludedBoundsTile(바다 등)이 지정되면 해당 타일을 제외한 영역만 경계로 삼는다.
    /// 계산 불가 시 인스펙터 수동 값을 그대로 사용한다.
    private void InitMapBounds()
    {
        if (!TilemapBoundsCalculator.TryCalculate(_boundsTilemap, _excludedBoundsTile, out Vector2 min, out Vector2 max))
            return;

        _mapMin = min - _boundsMarginMin;
        _mapMax = max + _boundsMarginMax;
    }

    private void OnEnable()
    {
        if (_cameraMoveAction != null)
            _cameraMoveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_cameraMoveAction != null)
            _cameraMoveAction.action.Disable();
    }

    private void Update()
    {
        HandleWASD();
        HandleEdgeScroll();
        HandleMouseLeftButtonDrag();
        HandleZoom();
        HandleBookmarks();

        ClampTargetPosition();
        ApplyMovement();
    }

    // ─────────────────────────────────────────────
    // 입력 처리
    // ─────────────────────────────────────────────

    /// WASD / 방향키 — 화면 기준 상하좌우 (Player/CameraMove Action)
    private void HandleWASD()
    {
        if (_cameraMoveAction == null) return;

        Vector2 moveInput = _cameraMoveAction.action.ReadValue<Vector2>();
        if (moveInput == Vector2.zero) return;

        Vector3 dir = new Vector3(moveInput.x, moveInput.y, 0f).normalized;
        // 일시정지/배속(Time.timeScale) 중에도 카메라는 항상 동일한 속도로 조작할 수 있어야 한다.
        _targetPos += dir * _wasdSpeed * Time.unscaledDeltaTime;
    }

    /// 마우스를 화면 가장자리로 가져가면 카메라 이동 (스타크래프트 방식)
    private void HandleEdgeScroll()
    {
        if (!_edgeScrollEnabled)    return;
        if (!Application.isFocused) return;
        if (_isDragging) return;
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 dir      = Vector3.zero;

        if (mousePos.x < _edgeScrollThreshold)                 dir.x -= 1f;
        if (mousePos.x > Screen.width  - _edgeScrollThreshold) dir.x += 1f;
        if (mousePos.y < _edgeScrollThreshold)                 dir.y -= 1f;
        if (mousePos.y > Screen.height - _edgeScrollThreshold) dir.y += 1f;

        if (_isDragEndInEdge)
        {
            if(dir == Vector3.zero)
            {
                _isDragEndInEdge = false;
            }
            else
            {
                return;
            }
        }
        if (dir == Vector3.zero) return;

        // 일시정지/배속 중에도 카메라는 항상 동일한 속도로 조작할 수 있어야 한다.
        _targetPos += dir.normalized * _edgeScrollSpeed * Time.unscaledDeltaTime;
    }

    /// 마우스 좌클릭 드래그로 카메라 이동
    private void HandleMouseLeftButtonDrag()
    {
        if (Mouse.current == null) return;

        // 드래그 시작 - UI 위에서 시작한 드래그는 무시한다 (UI 위에서 끝나는 것은 허용)
        if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            _dragWorldOrigin = ScreenToWorld(Mouse.current.position.ReadValue());
            _isDragging      = true;
        }

        // 드래그 중 — 월드 좌표 차이만큼 targetPos 이동
        if (_isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector3 currentWorld = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector3 delta        = (_dragWorldOrigin - currentWorld) * _dragSensitivity;

            _targetPos      += delta;
            // 드래그 기준점을 현재 위치로 갱신해야 누적 오차가 없음
            _dragWorldOrigin = ScreenToWorld(Mouse.current.position.ReadValue());
        }

        // 드래그 종료
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (mousePos.x < _edgeScrollThreshold
                || mousePos.x > Screen.width - _edgeScrollThreshold
                || mousePos.y < _edgeScrollThreshold
                || mousePos.y > Screen.height - _edgeScrollThreshold)
                _isDragEndInEdge = true;
        }
    }

    /// 마우스 휠 — Orthographic Size 줌
    private void HandleZoom()
    {
        if (Mouse.current == null) return;
        if (IsPointerOverUI()) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < SCROLL_DEADZONE) return;

        _targetZoom -= scroll * _zoomSpeed * SCROLL_TO_ZOOM_SCALE;
        _targetZoom  = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
    }


    /// 북마크 Ctrl 1 메인성 하나만 사용
    /// 일단 여러 북마크 가능하게 해놓고 후에 하나만 사용하는거로 변경 가능성 있음
    private void HandleBookmarks()
    {
        if (Keyboard.current == null) return;

        bool ctrl = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

        for (int i = 0; i < BOOKMARK_COUNT; i++)
        {
            Key key = Key.Digit1 + i;
            bool pressedThisFrame = Keyboard.current[key].wasPressedThisFrame;

            if (ctrl && pressedThisFrame)
            {
                // 저장
                _bookmarks[i] = _targetPos;
                Debug.Log($"[Camera] Bookmark {i + 1} 저장: {_targetPos}");
            }
            else if (!ctrl && pressedThisFrame && _bookmarks[i].HasValue)
            {
                // 복귀 — 저장 시점보다 줌 아웃돼 있으면 그 위치가 이미 경계를 넘을 수 있다
                _targetPos = ClampToMapBounds(_bookmarks[i].Value);
                Debug.Log($"[Camera] Bookmark {i + 1} 복귀");
            }
        }
    }

    // ─────────────────────────────────────────────
    // 위치 보정 & 적용
    // ─────────────────────────────────────────────

    /// 맵 경계 밖으로 나가지 않도록 targetPos 클램프
    private void ClampTargetPosition()
    {
        _targetPos = ClampToMapBounds(_targetPos);
    }

    /// 뷰포트(orthographicSize 기준)가 맵 경계를 넘어서지 않는 위치로 보정한다.
    /// 줌 상태에 따라 클램프 범위 자체를 안쪽으로 당겨준다. (z는 그대로 보존)
    /// 카메라를 움직이는 모든 경로(입력·MoveTo·북마크·최종 위치)가 이 함수를 거쳐야 한다.
    private Vector3 ClampToMapBounds(Vector3 position)
    {
        // _targetZoom이 아니라 _cam.orthographicSize를 쓰는 이유:
        // 클램프는 ApplyMovement의 줌 Lerp보다 먼저 호출되므로
        // 이 시점의 orthographicSize가 "이번 프레임 실제 화면에 반영된 값"과 일치한다.
        float halfHeight = _cam.orthographicSize;
        float halfWidth  = halfHeight * _cam.aspect;

        position.x = ClampAxis(position.x, _mapMin.x + halfWidth,  _mapMax.x - halfWidth);
        position.y = ClampAxis(position.y, _mapMin.y + halfHeight, _mapMax.y - halfHeight);
        return position;
    }

    /// 뷰포트가 맵보다 큰 축은 min > max가 되므로, 그 축은 맵 중앙으로 고정한다.
    private static float ClampAxis(float value, float min, float max)
    {
        if (min > max)
        {
            return (min + max) * 0.5f; // = (mapMin + mapMax) / 2 (맵 중앙)
        }
        return Mathf.Clamp(value, min, max);
    }

    /// SmoothDamp으로 부드럽게 이동 + 줌 Lerp
    /// 일시정지/배속 중에도 카메라 이동·줌 체감 속도가 바뀌지 않도록 unscaledDeltaTime을 쓴다.
    private void ApplyMovement()
    {
        // Z축은 카메라 고유 깊이 유지
        Vector3 goal = new Vector3(_targetPos.x, _targetPos.y, transform.position.z);
        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position, goal, ref _smoothVelocity, _moveSmoothTime,
            Mathf.Infinity, Time.unscaledDeltaTime);

        // 시작 위치가 경계 밖이거나 줌 아웃으로 허용 범위가 좁아지는 중에도
        // 실제 화면이 맵 밖을 비추지 않도록 최종 위치까지 클램프한다.
        transform.position = ClampToMapBounds(smoothed);

        _cam.orthographicSize = Mathf.Lerp(
            _cam.orthographicSize, _targetZoom, _zoomSmoothing * Time.unscaledDeltaTime);
    }

    // ─────────────────────────────────────────────
    // 외부 공개 API
    // ─────────────────────────────────────────────

    /// 미니맵 클릭 등 외부에서 카메라 위치를 즉시 설정할 때 사용
    /// UI 이벤트에서 호출되므로 Update의 클램프보다 늦게 실행될 수 있다.
    /// 여기서 바로 클램프하지 않으면 그 프레임의 SmoothDamp가 경계 밖으로 카메라를 끌고 간다.
    public void MoveTo(Vector2 worldPosition)
    {
        _targetPos = ClampToMapBounds(new Vector3(worldPosition.x, worldPosition.y, _targetPos.z));
    }

    /// 엣지 스크롤 토글 (설정 화면 연동)
    public void SetEdgeScrollEnabled(bool enabled)
    {
        _edgeScrollEnabled = enabled;
    }

    // ─────────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────────

    /// 스크린 좌표 → 월드 좌표 (2D Orthographic 전용)
    private Vector3 ScreenToWorld(Vector3 screenPos)
    {
        screenPos.z = 0f;
        return _cam.ScreenToWorldPoint(screenPos);
    }

    private static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
