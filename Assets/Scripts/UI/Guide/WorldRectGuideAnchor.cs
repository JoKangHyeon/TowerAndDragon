using UnityEngine;

/// <summary>
/// 월드 스페이스 캔버스에 있는 UI(성 위 속성 아이콘 등)가 화면에서 차지하는 자리에 자기 사각형을 맞춘다.
/// 안내는 이 사각형을 앵커로 가리켜 그 자리에 딤 구멍을 뚫는다.
///
/// 왜 필요한가: <see cref="UI_GuideOverlay"/>의 구멍은 <b>스크린 캔버스의 RectTransform</b>으로만 계산된다
/// (<c>ResolveLocalRect</c>가 오버레이 모드에서 카메라를 null로 넘긴다). 월드 캔버스의 rect를 그대로
/// 가리키면 월드 좌표를 화면 좌표로 착각해 구멍이 엉뚱한 곳에 뚫린다.
/// 맵 위 <b>건물</b>은 오버레이가 직접 다루지만(<c>DrawWorld</c> - Renderer의 바운드를 투영한다)
/// 월드 캔버스의 UI에는 Renderer가 없어 그 경로를 쓸 수 없다.
///
/// <see cref="MonsterPathGuideAnchor"/>와 같은 방식이다 - 그쪽이 LineRenderer 여럿을 감싸는 반면
/// 이쪽은 RectTransform 하나를 따라간다.
/// </summary>
[RequireComponent(typeof(GuideAnchor))]
public sealed class WorldRectGuideAnchor : MonoBehaviour
{
    private const float HALF = 0.5f;
    private const int RECT_CORNER_COUNT = 4;
    private const float DEFAULT_SCREEN_PADDING = 12f;

    private static readonly Vector2 CENTER_PIVOT = new Vector2(HALF, HALF);

    [Tooltip("따라갈 월드 스페이스 UI. 이 오브젝트가 화면에서 차지하는 사각형에 자기 자신을 맞춘다.")]
    [SerializeField] private RectTransform _worldTarget;

    [Tooltip("대상을 비추는 카메라. 비우면 Camera.main을 쓴다.")]
    [WiringOptional]
    [SerializeField] private Camera _worldCamera;

    [Tooltip("이 앵커를 가리키는 안내가 떠 있을 때만 자리를 다시 계산한다. 비우면 매 프레임 계산한다 - " +
             "이 앵커를 쓰는 단계는 한둘뿐이라, 비워 두면 튜토리얼 내내 쓰이지 않는 계산이 돈다.")]
    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("구한 영역을 이만큼(화면 픽셀) 넓힌다 - 아이콘이 구멍 가장자리에 딱 붙으면 잘린 것처럼 보인다.")]
    [Min(0f)]
    [SerializeField] private float _screenPadding = DEFAULT_SCREEN_PADDING;

    private readonly Vector3[] _cornerBuffer = new Vector3[RECT_CORNER_COUNT];
    private RectTransform _rect;
    private RectTransform _parentRect;
    private Canvas _canvas;

    private Camera WorldCamera => _worldCamera != null ? _worldCamera : Camera.main;

    // Overlay 모드에서는 카메라를 넘기면 좌표가 어긋나므로 null이어야 한다(UI_GuideOverlay와 같은 규칙).
    private Camera UiCamera =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    // 오버레이를 배선하지 않았으면 판단할 근거가 없으므로 매 프레임 계산한다 -
    // 성능을 위해 안내가 어긋나는 쪽을 택하지는 않는다(MonsterPathGuideAnchor와 같은 규칙).
    private bool IsTargetedByGuide =>
        _overlay == null || ReferenceEquals(_overlay.CurrentTarget, _rect);

    private void Awake()
    {
        _rect = transform as RectTransform;
        _parentRect = transform.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();

        if (_rect == null || _parentRect == null)
        {
            Debug.LogWarning(
                "[WorldRectGuideAnchor] RectTransform 부모 밑의 UI 오브젝트여야 합니다 - 영역을 계산할 수 없습니다.", this);
            enabled = false;
            return;
        }

        if (_worldTarget == null)
        {
            Debug.LogWarning("[WorldRectGuideAnchor] 따라갈 대상(_worldTarget)이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 앵커를 부모 피벗에 맞춰 두면 anchoredPosition과 부모의 로컬 좌표가 같은 계가 된다.
        _rect.anchorMin = _parentRect.pivot;
        _rect.anchorMax = _parentRect.pivot;
        _rect.pivot = CENTER_PIVOT;
    }

    // 카메라가 움직이면 같은 대상이라도 화면에서의 자리가 달라지므로 매 프레임 다시 맞춘다.
    // 안내가 구멍을 다시 그리는 것도 LateUpdate라, 여기서 갱신해야 같은 프레임에 반영된다.
    private void LateUpdate()
    {
        if (!IsTargetedByGuide || !TryResolveScreenRect(out Rect screenRect))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, screenRect.min, UiCamera, out Vector2 localMin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, screenRect.max, UiCamera, out Vector2 localMax))
        {
            return;
        }

        _rect.anchoredPosition = (localMin + localMax) * HALF;
        _rect.sizeDelta = localMax - localMin;
    }

    /// <summary>
    /// 대상이 화면에서 차지하는 사각형. 대상이 꺼져 있으면 false를 돌려주고, 그때는 마지막으로 구한
    /// 자리를 그대로 둔다 - 0 크기로 접으면 구멍이 사라져 화면이 통째로 덮인다.
    /// </summary>
    private bool TryResolveScreenRect(out Rect screenRect)
    {
        screenRect = Rect.zero;

        Camera worldCamera = WorldCamera;
        if (worldCamera == null || _worldTarget == null || !_worldTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        _worldTarget.GetWorldCorners(_cornerBuffer);

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector3 corner in _cornerBuffer)
        {
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(corner);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        if (min.x > max.x || min.y > max.y)
        {
            return false;
        }

        var padding = new Vector2(_screenPadding, _screenPadding);
        min -= padding;
        max += padding;
        screenRect = new Rect(min, max - min);
        return true;
    }
}
