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

    [Tooltip("커서에서 툴팁까지의 화면 픽셀 간격. 커서가 툴팁을 가리지 않게 띄운다.")]
    [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -16f);

    [Tooltip("툴팁이 화면 가장자리에서 유지할 최소 여백(픽셀).")]
    [SerializeField] private float _screenPadding = 8f;

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
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.zero;
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
        Vector2 clamped = ClampToScreen(screenPosition + _cursorOffset, screenPosition);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, clamped, ResolveCamera(), out Vector2 localPoint))
        {
            _panel.anchoredPosition = localPoint;
        }
    }

    // 피벗이 좌상단이므로 패널은 (x, y-h) ~ (x+w, y)를 차지한다. 오른쪽으로 넘치면 커서 왼쪽으로 뒤집고,
    // 그래도 넘치면 화면 안으로 밀어 넣는다.
    private Vector2 ClampToScreen(Vector2 desired, Vector2 cursorPosition)
    {
        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        float width = _panel.rect.width * scale;
        float height = _panel.rect.height * scale;

        float x = desired.x;
        float y = desired.y;

        if (x + width > Screen.width - _screenPadding)
        {
            x = cursorPosition.x - _cursorOffset.x - width;
        }

        if (y - height < _screenPadding)
        {
            y = cursorPosition.y - _cursorOffset.y + height;
        }

        x = Mathf.Clamp(x, _screenPadding, Mathf.Max(_screenPadding, Screen.width - width - _screenPadding));
        y = Mathf.Clamp(y, Mathf.Min(height + _screenPadding, Screen.height), Screen.height - _screenPadding);

        return new Vector2(x, y);
    }

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
