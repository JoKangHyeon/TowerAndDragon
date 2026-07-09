using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D 아이소메트릭 타워 디펜스 카메라 컨트롤러
/// 지원 기능:
///   - WASD / 방향키 이동
///   - 마우스 엣지 스크롤 (토글 가능)
///   - 미들 마우스 드래그 이동
///   - 마우스 휠 줌 (Orthographic Size)
///   - 카메라 위치 북마크 (Ctrl+1~5 저장, 1~5 복귀)
///   - MinimapController 연동 (MoveTo 공개 메서드)
///   - 맵 경계 클램프 + 부드러운 이동(SmoothDamp)
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController2 : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Inspector 설정
    // ─────────────────────────────────────────────

    [Header("이동 속도")]
    [SerializeField] private float wasdSpeed      = 12f;
    [SerializeField] private float edgeScrollSpeed = 12f;
    [SerializeField] private float dragSensitivity = 1f;  // 드래그 배율 (1 = 1:1)

    [Header("엣지 스크롤")]
    [SerializeField] private bool  edgeScrollEnabled   = true;
    [SerializeField] private float edgeScrollThreshold = 20f; // 픽셀

    [Header("줌")]
    [SerializeField] private float zoomSpeed    = 3f;
    [SerializeField] private float minZoom      = 3f;
    [SerializeField] private float maxZoom      = 18f;
    [SerializeField] private float zoomSmoothing = 8f;

    [Header("이동 스무딩")]
    [SerializeField] private float moveSmoothTime = 0.08f;

    [Header("맵 경계 (World 좌표)")]
    [SerializeField] private Vector2 mapMin = new Vector2(-60f, -60f);
    [SerializeField] private Vector2 mapMax = new Vector2( 60f,  60f);

    // ─────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────

    private Camera   cam;
    private Vector3  targetPos;          // 목표 위치 (SmoothDamp 대상)
    private float    targetZoom;         // 목표 줌
    private Vector3  smoothVelocity;     // SmoothDamp 내부 velocity

    // 미들 마우스 드래그
    private bool    isDragging;
    private Vector3 dragWorldOrigin;

    // 북마크 (0~4 = 단축키 1~5)
    private Vector3?[] bookmarks = new Vector3?[5];

    // ─────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────

    private void Awake()
    {
        cam        = GetComponent<Camera>();
        targetPos  = transform.position;
        targetZoom = cam.orthographicSize;
    }

    private void Update()
    {
        HandleWASD();
        HandleEdgeScroll();
        HandleMiddleMouseDrag();
        HandleZoom();
        HandleBookmarks();

        ClampTargetPosition();
        ApplyMovement();
    }

    // ─────────────────────────────────────────────
    // 입력 처리
    // ─────────────────────────────────────────────

    /// WASD / 방향키 — 화면 기준 상하좌우
    private void HandleWASD()
    {
        if (Keyboard.current == null) return;

        float h = 0f, v = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  h -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  v -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    v += 1f;

        if (h == 0f && v == 0f) return;

        Vector3 dir = new Vector3(h, v, 0f).normalized;
        targetPos += dir * wasdSpeed * Time.deltaTime;
    }

    /// 마우스를 화면 가장자리로 가져가면 카메라 이동 (스타크래프트 방식)
    private void HandleEdgeScroll()
    {
        if (!edgeScrollEnabled)    return;
        if (!Application.isFocused) return;
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 dir      = Vector3.zero;

        if (mousePos.x < edgeScrollThreshold)                 dir.x -= 1f;
        if (mousePos.x > Screen.width  - edgeScrollThreshold) dir.x += 1f;
        if (mousePos.y < edgeScrollThreshold)                 dir.y -= 1f;
        if (mousePos.y > Screen.height - edgeScrollThreshold) dir.y += 1f;

        if (dir == Vector3.zero) return;

        targetPos += dir.normalized * edgeScrollSpeed * Time.deltaTime;
    }

    /// 미들 마우스 버튼 클릭 후 드래그로 카메라 이동
    private void HandleMiddleMouseDrag()
    {
        if (Mouse.current == null) return;

        // 드래그 시작
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragWorldOrigin = ScreenToWorld(Mouse.current.position.ReadValue());
            isDragging      = true;
        }

        // 드래그 중 — 월드 좌표 차이만큼 targetPos 이동
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector3 currentWorld = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector3 delta        = (dragWorldOrigin - currentWorld) * dragSensitivity;

            targetPos      += delta;
            // 드래그 기준점을 현재 위치로 갱신해야 누적 오차가 없음
            dragWorldOrigin = ScreenToWorld(Mouse.current.position.ReadValue());
        }

        // 드래그 종료
        if (Mouse.current.leftButton.wasReleasedThisFrame)
            isDragging = false;
    }

    /// 마우스 휠 — Orthographic Size 줌
    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        targetZoom -= scroll * zoomSpeed * 0.01f;
        targetZoom  = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    /// Ctrl+1~5: 현재 위치 저장 / 1~5: 저장된 위치로 이동
    private void HandleBookmarks()
    {
        if (Keyboard.current == null) return;

        bool ctrl = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

        for (int i = 0; i < 5; i++)
        {
            Key key = Key.Digit1 + i;
            bool pressedThisFrame = Keyboard.current[key].wasPressedThisFrame;

            if (ctrl && pressedThisFrame)
            {
                // 저장
                bookmarks[i] = targetPos;
                Debug.Log($"[Camera] Bookmark {i + 1} 저장: {targetPos}");
            }
            else if (!ctrl && pressedThisFrame && bookmarks[i].HasValue)
            {
                // 복귀
                targetPos = bookmarks[i].Value;
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
        targetPos.x = Mathf.Clamp(targetPos.x, mapMin.x, mapMax.x);
        targetPos.y = Mathf.Clamp(targetPos.y, mapMin.y, mapMax.y);
    }

    /// SmoothDamp으로 부드럽게 이동 + 줌 Lerp
    private void ApplyMovement()
    {
        // Z축은 카메라 고유 깊이 유지
        Vector3 goal = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(
            transform.position, goal, ref smoothVelocity, moveSmoothTime);

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize, targetZoom, zoomSmoothing * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    // 외부 공개 API
    // ─────────────────────────────────────────────

    /// 미니맵 클릭 등 외부에서 카메라 위치를 즉시 설정할 때 사용
    public void MoveTo(Vector2 worldPosition)
    {
        targetPos = new Vector3(worldPosition.x, worldPosition.y, targetPos.z);
    }

    /// 엣지 스크롤 토글 (설정 화면 연동)
    public void SetEdgeScrollEnabled(bool enabled)
    {
        edgeScrollEnabled = enabled;
    }

    // ─────────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────────

    /// 스크린 좌표 → 월드 좌표 (2D Orthographic 전용)
    private Vector3 ScreenToWorld(Vector3 screenPos)
    {
        screenPos.z = 0f;
        return cam.ScreenToWorldPoint(screenPos);
    }
}
