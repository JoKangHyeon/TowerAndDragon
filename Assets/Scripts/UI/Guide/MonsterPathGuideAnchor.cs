using UnityEngine;

/// <summary>
/// 오늘 밤 몬스터가 지나갈 경로가 화면에서 차지하는 영역에 자기 사각형을 맞춘다.
/// 안내는 이 사각형을 <see cref="GuideAnchorId.MonsterPathArea"/>로 가리켜 그 자리에 딤 구멍을 뚫는다 -
/// 구멍은 딤 패널 4장 사이의 빈 칸이므로 그 안에는 경로선이 그대로 비친다.
///
/// 경로선은 월드 <see cref="LineRenderer"/>라 안내에 직접 넘길 수 없고(딤 구멍은 RectTransform으로만 계산된다),
/// 모양대로 뚫을 수도 없다. 그 일대를 감싸는 사각형 하나로 좁히는 것이 이 컴포넌트의 일이다.
///
/// 씬에 손으로 놓지 않고 매 프레임 다시 구하는 이유: 화면에 고정하면 카메라를 움직이는 순간 구멍이
/// 경로에서 어긋나고, 포탈이 늘어나는 날에는 엉뚱한 곳을 가리킨다.
///
/// 오늘 켜지는 경로가 무엇인지는 <see cref="PortalPathVisibility"/>가 이미 정해 두었다 -
/// 그쪽이 오늘 안 쓰는 라인의 LineRenderer를 꺼 두므로, 켜져 있는 것만 모으면 그것이 오늘의 경로다.
/// </summary>
[RequireComponent(typeof(GuideAnchor))]
public sealed class MonsterPathGuideAnchor : MonoBehaviour
{
    private const float HALF = 0.5f;
    private const float DEFAULT_SCREEN_PADDING = 24f;

    private static readonly Vector2 CENTER_PIVOT = new Vector2(HALF, HALF);

    // 월드 바운드의 여덟 꼭짓점을 만들 부호. 비트 연산 대신 표로 두어 리터럴을 없앤다.
    private static readonly Vector3[] BOUNDS_CORNER_SIGNS =
    {
        new Vector3(-1f, -1f, -1f),
        new Vector3(-1f, -1f, 1f),
        new Vector3(-1f, 1f, -1f),
        new Vector3(-1f, 1f, 1f),
        new Vector3(1f, -1f, -1f),
        new Vector3(1f, -1f, 1f),
        new Vector3(1f, 1f, -1f),
        new Vector3(1f, 1f, 1f),
    };

    [Tooltip("경로 라인들이 들어 있는 루트. PortalPathVisibility의 _pathRoot와 같은 오브젝트를 넣는다.")]
    [SerializeField] private Transform _pathRoot;

    [Tooltip("경로를 비추는 카메라. 비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _worldCamera;

    [Tooltip("이 앵커를 가리키는 안내가 떠 있을 때만 자리를 다시 계산한다. 비우면 매 프레임 계산한다 - " +
             "안내가 이 앵커를 쓰는 단계는 하나뿐이라, 비워 두면 튜토리얼 내내 쓰이지 않는 계산이 돈다.")]
    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("구한 영역을 이만큼(화면 픽셀) 넓힌다 - 선이 구멍 가장자리에 딱 붙으면 잘린 것처럼 보인다.")]
    [Min(0f)]
    [SerializeField] private float _screenPadding = DEFAULT_SCREEN_PADDING;

    private LineRenderer[] _pathLines;
    private RectTransform _rect;
    private RectTransform _parentRect;
    private Canvas _canvas;

    private Camera WorldCamera => _worldCamera != null ? _worldCamera : Camera.main;

    // Overlay 모드에서는 카메라를 넘기면 좌표가 어긋나므로 null이어야 한다(UI_GuideOverlay와 같은 규칙).
    private Camera UiCamera =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    /// <summary>
    /// 지금 안내가 이 앵커를 가리키고 있는지. 오버레이를 배선하지 않았으면 판단할 근거가 없으므로
    /// 예전처럼 매 프레임 계산한다 - 성능을 위해 안내가 어긋나는 쪽을 택하지는 않는다.
    /// </summary>
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
                "[MonsterPathGuideAnchor] RectTransform 부모 밑의 UI 오브젝트여야 합니다 - 영역을 계산할 수 없습니다.", this);
            enabled = false;
            return;
        }

        // 앵커를 부모 피벗에 맞춰 두면 anchoredPosition과 부모의 로컬 좌표가 같은 계가 된다
        // (UI_GuideOverlay가 구멍 테두리를 다루는 방식과 같다).
        _rect.anchorMin = _parentRect.pivot;
        _rect.anchorMax = _parentRect.pivot;
        _rect.pivot = CENTER_PIVOT;

        CachePathLines();
    }

    private void CachePathLines()
    {
        if (_pathRoot == null)
        {
            Debug.LogWarning("[MonsterPathGuideAnchor] 경로 루트(_pathRoot)가 비어 있어 경로 영역을 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        // 꺼져 있는 것까지 모은다 - 오늘 쓰는 경로는 날마다 바뀌고, 그 판정은 매 프레임 enabled로 한다.
        _pathLines = _pathRoot.GetComponentsInChildren<LineRenderer>(true);
    }

    // 카메라가 움직이면 같은 경로라도 화면에서의 자리가 달라지므로 매 프레임 다시 맞춘다.
    // 안내가 구멍을 다시 그리는 것도 LateUpdate라, 여기서 갱신해야 같은 프레임에 반영된다.
    //
    // 다만 이 앵커를 가리키는 안내가 떠 있을 때만 계산한다. 경로선 전부의 월드 바운드를 화면으로
    // 투영하는 일이라 공짜가 아닌데, 이 앵커를 쓰는 단계는 1일차의 경로 안내 하나뿐이다.
    // 게이트가 없으면 튜토리얼이 도는 내내 아무도 보지 않는 사각형을 매 프레임 다시 맞추게 된다.
    private void LateUpdate()
    {
        if (!IsTargetedByGuide)
        {
            return;
        }

        if (!TryResolveScreenRect(out Rect screenRect))
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
    /// 오늘 켜져 있는 경로선 전부를 감싸는 화면 사각형. 켜진 경로가 하나도 없으면 false를 돌려주고,
    /// 그때는 마지막으로 구한 자리를 그대로 둔다 - 0 크기로 접으면 구멍이 사라져 화면이 통째로 덮인다.
    /// </summary>
    private bool TryResolveScreenRect(out Rect screenRect)
    {
        screenRect = Rect.zero;

        Camera worldCamera = WorldCamera;
        if (worldCamera == null || _pathLines == null)
        {
            return false;
        }

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        bool hasVisibleLine = false;

        foreach (LineRenderer line in _pathLines)
        {
            // PortalPathVisibility가 오늘 쓰지 않는 경로의 LineRenderer를 꺼 둔다.
            if (line == null || !line.enabled || !line.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasVisibleLine = true;
            AccumulateBounds(line.bounds, worldCamera, ref min, ref max);
        }

        if (!hasVisibleLine || min.x > max.x || min.y > max.y)
        {
            return false;
        }

        var padding = new Vector2(_screenPadding, _screenPadding);
        min -= padding;
        max += padding;
        screenRect = new Rect(min, max - min);
        return true;
    }

    // 곡선을 점마다 투영하지 않고 월드 바운드의 여덟 꼭짓점만 본다 - 경로를 덮는 사각형을 구하는 것이
    // 목적이라 이것으로 충분하고, 점 수에 관계없이 비용이 일정하다.
    private static void AccumulateBounds(Bounds bounds, Camera worldCamera, ref Vector2 min, ref Vector2 max)
    {
        foreach (Vector3 sign in BOUNDS_CORNER_SIGNS)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, sign);
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(corner);

            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }
    }
}
