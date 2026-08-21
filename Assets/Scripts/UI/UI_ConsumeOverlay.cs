using System.Collections.Generic;
using UnityEngine;

// 건물·타워를 설치할 때 빠져나간 자원을 그 자리에서 떠오르게 보여준다.
// 자원 종류가 둘 이상이면 같은 프리팹(Overlay_Consume)을 종류마다 하나씩 찍어 세로로 쌓는다.
//
// 좌표 변환은 WorldRectGuideAnchor와 같은 규칙을 따른다 - 월드 좌표를 카메라로 스크린 좌표로
// 바꾼 뒤, 부모 사각형의 로컬 좌표로 한 번 더 바꾼다. 화면 픽셀을 그대로 쓰면 캔버스 스케일이
// 1이 아닐 때 창 크기에 따라 위치가 어긋난다.
public class UI_ConsumeOverlay : MonoBehaviour
{
    private const float DEFAULT_ROW_SPACING = 52f;
    private const float DEFAULT_RISE_DISTANCE = 70f;
    private const float DEFAULT_DURATION = 0.9f;
    private const float DEFAULT_ROW_STAGGER = 0.14f;
    private const float DEFAULT_SWAY_STRENGTH = 10f;

    // 자원 수량 앞에 붙는 부호. 소모임을 드러내려고 항상 음수로 적는다.
    // 언어에 따라 달라지지 않으므로 스트링테이블 대신 상수로 둔다.
    private const string CONSUME_AMOUNT_FORMAT = "-{0}";

    [Header("Dependencies")]
    [Tooltip("찍어낸 줄을 담을 부모. 이 사각형의 로컬 좌표로 위치를 계산한다.")]
    [SerializeField] private RectTransform _container;

    [SerializeField] private UI_ConsumeOverlayRow _rowPrefab;

    // ResourceManager(씬 오브젝트) 대신 카탈로그 에셋을 직접 참조한다 - 아이콘 조회에만 쓰므로
    // 에셋으로 충분하고, 그래야 UI_Canvas 프리팹 안에서 배선이 끝나 씬마다 다시 연결할 필요가 없다.
    [Tooltip("자원 아이콘을 찾는 데 쓴다.")]
    [SerializeField] private ResourceCatalog _resourceCatalog;

    [Tooltip("대상을 비추는 카메라. 비우면 Camera.main을 쓴다.")]
    [WiringOptional]
    [SerializeField] private Camera _worldCamera;

    [Header("연출")]
    [Tooltip("자원이 여러 종류일 때 줄 사이 세로 간격. 아래로 쌓인다.")]
    [SerializeField] private float _rowSpacing = DEFAULT_ROW_SPACING;

    [Tooltip("한 줄이 떠오르는 거리.")]
    [SerializeField] private float _riseDistance = DEFAULT_RISE_DISTANCE;

    [Tooltip("떠오르며 사라지는 데 걸리는 시간.")]
    [SerializeField] private float _duration = DEFAULT_DURATION;

    [Tooltip("자원이 여러 종류일 때 다음 줄이 등장하기까지의 간격. 0이면 한꺼번에 나온다.")]
    [SerializeField] private float _rowStagger = DEFAULT_ROW_STAGGER;

    [Tooltip("떠오르는 동안 좌우로 흔들릴 진폭. 0이면 흔들지 않는다.")]
    [SerializeField] private float _swayStrength = DEFAULT_SWAY_STRENGTH;

    [Tooltip("설치 지점에서 얼마나 띄워 시작할지(스크린 픽셀). 건물 스프라이트에 겹치지 않게 올려 둔다.")]
    [SerializeField] private Vector2 _screenOffset = new Vector2(0f, 40f);

    private Canvas _canvas;

    private Camera WorldCamera => _worldCamera != null ? _worldCamera : Camera.main;

    // Overlay 모드에서는 카메라를 넘기면 좌표가 어긋나므로 null이어야 한다
    // (WorldRectGuideAnchor·UI_GuideOverlay와 같은 규칙).
    private Camera UiCamera =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// <paramref name="worldPosition"/> 위에 소모 자원을 한 종류씩 띄운다.
    /// 비용이 비어 있으면 아무것도 하지 않는다(무료 건물).
    /// </summary>
    public void Show(IReadOnlyList<ResourceAmount> cost, Vector3 worldPosition)
    {
        if (cost == null || cost.Count == 0)
        {
            return;
        }

        if (!WiringGuard.Require(_container, nameof(_container), this) ||
            !WiringGuard.Require(_rowPrefab, nameof(_rowPrefab), this))
        {
            return;
        }

        if (!TryResolveLocalPoint(worldPosition, out Vector2 origin))
        {
            return;
        }

        // 인덱스를 따로 세는 이유: 수량 0인 항목(있을 수 있다)을 건너뛰어도 줄 간격이 벌어지지 않게 한다.
        int rowIndex = 0;

        foreach (ResourceAmount amount in cost)
        {
            if (amount.Amount == 0)
            {
                continue;
            }

            SpawnRow(amount, origin, rowIndex);
            rowIndex++;
        }
    }

    private void SpawnRow(ResourceAmount amount, Vector2 origin, int rowIndex)
    {
        UI_ConsumeOverlayRow row = Instantiate(_rowPrefab, _container);
        var rowRect = (RectTransform)row.transform;

        // 앵커를 부모의 피벗에 맞춘다 - ScreenPointToLocalPointInRectangle이 돌려주는 좌표는
        // 부모의 '피벗'이 원점인데 anchoredPosition은 '앵커 지점'에서 재므로, 어긋나면 캔버스
        // 절반만큼 밀린다(UI_TooltipPresenter와 같은 이유).
        rowRect.anchorMin = _container.pivot;
        rowRect.anchorMax = _container.pivot;
        rowRect.anchoredPosition = origin + Vector2.down * (_rowSpacing * rowIndex);

        row.Play(
            IconFor(amount.Type),
            FormatAmount(amount.Amount),
            _rowStagger * rowIndex,
            _riseDistance,
            _duration,
            _swayStrength);
    }

    private bool TryResolveLocalPoint(Vector3 worldPosition, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        Camera worldCamera = WorldCamera;
        if (worldCamera == null)
        {
            return false;
        }

        Vector2 screenPoint = (Vector2)worldCamera.WorldToScreenPoint(worldPosition) + _screenOffset;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _container, screenPoint, UiCamera, out localPoint);
    }

    private Sprite IconFor(ResourceType type)
    {
        if (_resourceCatalog == null)
        {
            return null;
        }

        return _resourceCatalog.TryGet(type, out ResourceData data) ? data.Icon : null;
    }

    private static string FormatAmount(int amount) => string.Format(CONSUME_AMOUNT_FORMAT, amount);
}
