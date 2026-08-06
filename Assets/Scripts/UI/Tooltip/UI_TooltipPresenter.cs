using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 툴팁 패널 하나를 소유해 내용을 그리고 커서를 따라다니게 한다. 도메인을 모르고 TooltipContent만 받으므로
/// 자원·건물·연구 등 어디서든 같은 표시기를 재사용한다.
///
/// 싱글턴을 두지 않는다(프로젝트 관례). 소유 창이 UI_TooltipTrigger.SetPresenter로 주입하거나,
/// 트리거 인스펙터에서 직접 연결한다.
/// </summary>
public class UI_TooltipPresenter : MonoBehaviour
{
    [Tooltip("툴팁 본체. 표시할 내용이 없을 때 통째로 비활성화한다.")]
    [SerializeField] private RectTransform _panel;

    [Tooltip("제목 텍스트. 비워두면 제목 줄을 쓰지 않는다.")]
    [SerializeField] private TMP_Text _titleText;

    [Tooltip("본문 텍스트. TMP 리치텍스트를 그대로 그린다.")]
    [SerializeField] private TMP_Text _bodyText;

    [Tooltip("커서에서 툴팁까지의 간격(캔버스 단위). 커서가 툴팁을 가리지 않게 띄운다.")]
    [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -16f);

    [Tooltip("툴팁이 캔버스 가장자리에서 유지할 최소 여백(캔버스 단위).")]
    [SerializeField] private float _edgePadding = 8f;

    private Canvas _canvas;
    private RectTransform _parentRect;

    // 현재 툴팁을 띄운 주체. 다른 요소의 Hide 호출이 남의 툴팁을 끄지 않게 한다.
    private object _owner;

    // 내용만 갱신할 때 위치를 유지하기 위해 마지막 커서 위치를 들고 있는다.
    private Vector2 _lastScreenPosition;

    private bool IsShowing => _owner != null;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();

        if (_panel != null)
        {
            _parentRect = _panel.parent as RectTransform;

            // 커서 기준으로 위치를 잡으려면 앵커가 한 점이어야 한다. 좌상단 피벗이라 커서 오른쪽 아래로 펼쳐진다.
            //
            // 앵커를 부모의 피벗에 맞추는 것이 핵심이다. ScreenPointToLocalPointInRectangle이 돌려주는 좌표는
            // 부모의 '피벗'이 원점인 로컬 좌표인데, anchoredPosition은 '앵커 지점'에서 잰 값이다.
            // 둘이 어긋나면(예: 앵커 (0,0) + 부모 피벗 (0.5,0.5)) 툴팁이 캔버스 절반만큼 밀려 화면 밖으로 나간다.
            if (_parentRect != null)
            {
                _panel.anchorMin = _parentRect.pivot;
                _panel.anchorMax = _parentRect.pivot;
            }

            _panel.pivot = new Vector2(0f, 1f);
            _panel.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        ForceHide();
    }

    /// <summary>owner가 요청한 내용을 커서 위치에 띄운다. 내용이 비어 있으면 아무것도 하지 않는다.</summary>
    public void Show(object owner, in TooltipContent content, Vector2 screenPosition)
    {
        if (owner == null || _panel == null || !content.HasContent)
        {
            return;
        }

        _owner = owner;
        ApplyContent(content);
        MoveTo(screenPosition);
    }

    /// <summary>owner가 띄운 툴팁만 닫는다. 다른 주체가 이미 새 툴팁을 띄웠다면 무시한다.</summary>
    public void Hide(object owner)
    {
        if (owner == null || !ReferenceEquals(_owner, owner))
        {
            return;
        }

        ForceHide();
    }

    /// <summary>주체와 무관하게 즉시 닫는다. 창이 닫히거나 모드가 바뀔 때 쓴다.</summary>
    public void ForceHide()
    {
        _owner = null;

        if (_panel != null)
        {
            _panel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 표시 중인 툴팁의 내용만 갱신한다(값이 실시간으로 바뀌는 경우). 위치는 마지막 커서 자리를 유지한다.
    /// </summary>
    public void Refresh(object owner, in TooltipContent content)
    {
        if (!IsShowing || !ReferenceEquals(_owner, owner))
        {
            return;
        }

        if (!content.HasContent)
        {
            ForceHide();
            return;
        }

        ApplyContent(content);
        MoveTo(_lastScreenPosition);
    }

    public void MoveTo(Vector2 screenPosition)
    {
        if (!IsShowing || _panel == null || _parentRect == null)
        {
            return;
        }

        _lastScreenPosition = screenPosition;

        // 화면 픽셀이 아니라 부모(캔버스) 로컬 단위로 변환한 뒤에 계산한다.
        // 캔버스 스케일이 1이 아니면 두 단위가 다르므로, 섞어 쓰면 창 크기에 따라 위치가 어긋난다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, screenPosition, ResolveCamera(), out Vector2 cursorLocal))
        {
            return;
        }

        _panel.anchoredPosition = ClampInsideParent(cursorLocal);
    }

    // 피벗이 좌상단이므로 패널은 (x, y-h) ~ (x+w, y)를 차지한다. 오른쪽/아래로 넘치면 커서 반대편으로
    // 뒤집고, 그래도 넘치면 부모 사각형 안으로 밀어 넣는다.
    private Vector2 ClampInsideParent(Vector2 cursorLocal)
    {
        Rect area = _parentRect.rect;
        float width = _panel.rect.width;
        float height = _panel.rect.height;

        float x = cursorLocal.x + _cursorOffset.x;
        float y = cursorLocal.y + _cursorOffset.y;

        if (x + width > area.xMax - _edgePadding)
        {
            x = cursorLocal.x - _cursorOffset.x - width;
        }

        if (y - height < area.yMin + _edgePadding)
        {
            y = cursorLocal.y - _cursorOffset.y + height;
        }

        return new Vector2(
            ClampRange(x, area.xMin + _edgePadding, area.xMax - width - _edgePadding),
            ClampRange(y, area.yMin + height + _edgePadding, area.yMax - _edgePadding));
    }

    // 툴팁이 부모보다 크면 최솟값이 최댓값을 넘어선다 - 그 경우 최솟값(좌상단 붙임)을 택한다.
    private static float ClampRange(float value, float min, float max) =>
        min > max ? min : Mathf.Clamp(value, min, max);

    private void ApplyContent(in TooltipContent content)
    {
        _panel.gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.gameObject.SetActive(!string.IsNullOrEmpty(content.Title));
            _titleText.text = content.Title;
        }

        if (_bodyText != null)
        {
            _bodyText.gameObject.SetActive(!string.IsNullOrEmpty(content.Body));
            _bodyText.text = content.Body;
        }

        // 위치를 잡기 전에 크기를 확정해야 화면 밖 판정이 맞다(ContentSizeFitter는 다음 프레임에 반영된다).
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
    }

    private Camera ResolveCamera()
    {
        if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _canvas.worldCamera;
    }
}
