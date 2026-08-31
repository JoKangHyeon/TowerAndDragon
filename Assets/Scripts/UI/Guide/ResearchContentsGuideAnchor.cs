using UnityEngine;

/// <summary>
/// 연구 창을 가리키는 딤 구멍의 크기를 상황에 맞춰 좁혔다 넓힌다.
/// <see cref="GuideAnchorId.ResearchWindowContents"/> 앵커에 붙인다.
///
/// 왜 필요한가: 노드 해금은 <b>트리에서 노드를 고르고 상세 패널의 연구 버튼을 누르는</b> 두 단계라
/// 구멍이 둘을 함께 덮어야 한다. 그런데 상세 패널은 창 오른쪽 <b>바깥</b>으로 삐져나와 있어서,
/// 처음부터 둘을 덮는 고정 사각형을 쓰면 구멍이 창 밖까지 뚫린다. 창 밖에 있는 것은 전체 화면
/// 블로커(<see cref="UI_ResearchWindow"/>의 blockerButton)뿐이라, 그 자리를 누르면
/// <b>안내를 따라가려던 클릭이 연구 창을 닫아 버린다</b>.
///
/// 그래서 상세 패널이 떠 있을 때만 그쪽으로 넓힌다 - 노드를 고르기 전에는 트리 영역만 덮으므로
/// 구멍이 창 밖으로 나가지 않는다.
///
/// <see cref="WorldRectGuideAnchor"/>·<see cref="MonsterPathGuideAnchor"/>와 같은 계열이다 -
/// 가리킬 자리를 스스로 계산해 자기 사각형에 담아 두고, 안내는 그것을 앵커로 가리킬 뿐이다.
/// 그 둘과 달리 대상이 같은 캔버스의 UI라 화면 좌표를 거치지 않고 부모의 로컬 좌표로 바로 옮긴다.
///
/// 그 둘과 또 하나 다른 점: <see cref="GuideAnchor"/>를 RequireComponent로 걸지 않는다.
/// 이 앵커는 <see cref="GuideAnchorBinder"/>가 런타임에 붙이므로, 여기서 강제하면 id가 None인
/// 앵커가 씬에 하나 더 저장돼 앵커 검증 창에 빈 항목으로 잡힌다.
///
/// <b>구멍을 넓히는 것과 짝이 되는 규칙</b>으로 <see cref="IResearchDetailsCloseQuery"/>도 함께 맡는다.
/// 구멍은 사각형 하나라 트리와 상세 패널을 함께 덮으면 <b>그 사이의 공백까지 뚫린다</b> - 안내를 따라
/// 누른 클릭이 그 공백에 떨어지면 방금 연 상세 패널이 닫혀 눌러야 할 해금 버튼이 사라진다.
/// 두 판단이 같은 조건("안내가 지금 이 앵커를 가리키는가")을 쓰므로 컴포넌트를 나누지 않는다 -
/// 나누면 같은 상태를 두 곳에서 보게 되어 서로 어긋날 수 있다.
/// </summary>
public sealed class ResearchContentsGuideAnchor : MonoBehaviour, IResearchDetailsCloseQuery
{
    private const float HALF = 0.5f;
    private const int RECT_CORNER_COUNT = 4;

    private static readonly Vector2 CENTER_PIVOT = new Vector2(HALF, HALF);

    [Tooltip("노드를 고르기 전에 덮을 영역. 연구 트리가 보이는 자리(Contents/Scroll View/Viewport)를 넣는다.")]
    [SerializeField] private RectTransform _treeArea;

    [Tooltip("떠 있는 동안 구멍을 여기까지 넓힐 상세 패널. 비면 구멍이 트리 영역에서 넓어지지 않아 " +
        "연구 버튼이 딤에 막힌다.")]
    [SerializeField] private UI_ResearchDetailsPanel _detailsPanel;

    [Tooltip("이 앵커를 가리키는 안내가 떠 있을 때만 자리를 다시 계산한다. 비우면 매 프레임 계산한다. " +
        "비면 상세 패널 닫기도 막지 않는다 - 판단할 근거가 없는데 막으면 패널을 영영 못 닫는다.")]
    [WiringOptional]
    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("빈 곳 클릭으로 상세 패널이 닫히는 것을 막을 연구 창. 비면 막지 않는다.")]
    [SerializeField] private UI_ResearchWindow _researchWindow;

    private readonly Vector3[] _cornerBuffer = new Vector3[RECT_CORNER_COUNT];
    private RectTransform _rect;
    private RectTransform _parentRect;

    // 오버레이를 배선하지 않았으면 판단할 근거가 없으므로 매 프레임 계산한다
    // (WorldRectGuideAnchor와 같은 규칙 - 성능을 위해 안내가 어긋나는 쪽을 택하지 않는다).
    private bool IsTargetedByGuide =>
        _overlay == null || ReferenceEquals(_overlay.CurrentTarget, _rect);

    private void Awake()
    {
        _rect = transform as RectTransform;
        _parentRect = transform.parent as RectTransform;

        if (_rect == null || _parentRect == null)
        {
            Debug.LogWarning(
                "[ResearchContentsGuideAnchor] RectTransform 부모 밑의 UI 오브젝트여야 합니다 - 영역을 계산할 수 없습니다.",
                this);
            enabled = false;
            return;
        }

        if (!WiringGuard.Require(_treeArea, nameof(_treeArea), this))
        {
            enabled = false;
            return;
        }

        WiringGuard.Optional(_detailsPanel, nameof(_detailsPanel), this);
        WiringGuard.Optional(_researchWindow, nameof(_researchWindow), this);

        // 앵커를 부모 피벗에 맞춰 두면 anchoredPosition과 부모의 로컬 좌표가 같은 계가 된다
        // (WorldRectGuideAnchor와 같은 준비).
        _rect.anchorMin = _parentRect.pivot;
        _rect.anchorMax = _parentRect.pivot;
        _rect.pivot = CENTER_PIVOT;
    }

    // 안내가 구멍을 다시 그리는 것이 LateUpdate라, 여기서 갱신해야 같은 프레임에 반영된다.
    private void LateUpdate()
    {
        if (!IsTargetedByGuide || !TryGetLocalRect(_treeArea, out Rect area))
        {
            return;
        }

        // 상세 패널이 떠 있을 때만 넓힌다. 닫혀 있는데 넓히면 그 자리는 창 밖 블로커라
        // 안내를 따라 누른 클릭이 연구 창을 닫는다.
        if (IsDetailsPanelShown && TryGetLocalRect(DetailsPanelRect, out Rect detailsRect))
        {
            area = Encapsulate(area, detailsRect);
        }

        _rect.anchoredPosition = area.center;
        _rect.sizeDelta = area.size;
    }

    /// <summary>
    /// 안내가 이 앵커를 가리키는 동안에는 빈 곳 클릭으로 상세 패널을 닫지 않는다 -
    /// 그 클릭이 떨어지는 공백은 안내가 뚫어 준 구멍의 일부라, 플레이어에게는 "누르라고 뚫어 둔 자리"로 읽힌다.
    ///
    /// 오버레이를 배선하지 않았으면 <b>막지 않는다</b>. 이 경우 <see cref="IsTargetedByGuide"/>가 항상
    /// 참이라, 그대로 쓰면 안내가 없는데도 패널을 영영 닫을 수 없게 된다.
    /// </summary>
    bool IResearchDetailsCloseQuery.CanCloseResearchDetails() =>
        _overlay == null || !IsTargetedByGuide;

    private void OnEnable()
    {
        if (_researchWindow != null)
        {
            _researchWindow.DetailsCloseQuery = this;
        }
    }

    private void OnDisable()
    {
        // 남이 걸어둔 것을 지우지 않도록 내가 건 경우에만 뗀다(TutorialConquestHint와 같은 관례).
        if (_researchWindow != null && ReferenceEquals(_researchWindow.DetailsCloseQuery, this))
        {
            _researchWindow.DetailsCloseQuery = null;
        }
    }

    private bool IsDetailsPanelShown => _detailsPanel != null && _detailsPanel.IsShown;

    private RectTransform DetailsPanelRect =>
        _detailsPanel == null ? null : _detailsPanel.transform as RectTransform;

    /// <summary>
    /// 대상이 부모의 로컬 좌표에서 차지하는 사각형. 대상이 없거나 꺼져 있으면 false다 -
    /// 그때 0 크기로 접으면 구멍이 사라져 화면이 통째로 덮인다.
    /// </summary>
    private bool TryGetLocalRect(RectTransform target, out Rect localRect)
    {
        localRect = Rect.zero;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        target.GetWorldCorners(_cornerBuffer);

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector3 corner in _cornerBuffer)
        {
            Vector2 local = _parentRect.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        if (min.x > max.x || min.y > max.y)
        {
            return false;
        }

        localRect = new Rect(min, max - min);
        return true;
    }

    private static Rect Encapsulate(Rect a, Rect b)
    {
        Vector2 min = Vector2.Min(a.min, b.min);
        Vector2 max = Vector2.Max(a.max, b.max);

        return new Rect(min, max - min);
    }
}
