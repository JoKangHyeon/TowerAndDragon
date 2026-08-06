using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스를 올리면 툴팁을 띄우는 범용 트리거. 이 오브젝트(또는 자식) 중 하나가 레이캐스트 대상이어야 한다.
///
/// 두 가지 방식으로 쓴다.
/// 1) 고정 문구: 인스펙터에 제목/본문 스트링테이블 키만 넣으면 코드 없이 동작한다.
/// 2) 동적 문구: 소유 스크립트가 SetContent로 밀어 넣는다(값이 바뀔 때마다 다시 호출).
/// 동적 내용이 설정돼 있으면 그쪽이 우선한다.
///
/// 표시기(UI_TooltipPresenter)는 인스펙터에서 직접 연결하거나 소유 창이 SetPresenter로 주입한다.
/// </summary>
public class UI_TooltipTrigger : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    [Tooltip("툴팁을 그릴 표시기. 비워두면 소유 스크립트가 SetPresenter로 주입해야 한다.")]
    [SerializeField] private UI_TooltipPresenter _presenter;

    [Tooltip("고정 제목의 스트링테이블 키. 동적 내용을 쓰면 비워둔다.")]
    [SerializeField] private string _titleLocKey;

    [Tooltip("고정 본문의 스트링테이블 키. 동적 내용을 쓰면 비워둔다.")]
    [SerializeField] private string _bodyLocKey;

    private TooltipContent _dynamicContent;
    private bool _hasDynamicContent;
    private bool _isHovered;

    /// <summary>소유 창이 표시기를 주입한다(프리팹마다 참조를 손으로 걸지 않아도 되게).</summary>
    public void SetPresenter(UI_TooltipPresenter presenter)
    {
        _presenter = presenter;
    }

    /// <summary>동적 내용을 설정한다. 표시 중이면 즉시 반영한다.</summary>
    public void SetContent(in TooltipContent content)
    {
        _dynamicContent = content;
        _hasDynamicContent = content.HasContent;

        if (_isHovered)
        {
            RefreshWhileHovered();
        }
    }

    /// <summary>동적 내용을 지운다. 인스펙터 키가 있으면 그쪽으로 되돌아간다.</summary>
    public void ClearContent()
    {
        _dynamicContent = default;
        _hasDynamicContent = false;

        if (_isHovered)
        {
            RefreshWhileHovered();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;

        TooltipContent content = ResolveContent();

        if (_presenter == null || !content.HasContent)
        {
            return;
        }

        _presenter.Show(this, content, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_isHovered && _presenter != null)
        {
            _presenter.MoveTo(eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _presenter?.Hide(this);
    }

    // 창이 닫히거나 이 행이 꺼질 때 툴팁이 화면에 남지 않게 한다(OnPointerExit가 오지 않는다).
    private void OnDisable()
    {
        _isHovered = false;
        _presenter?.Hide(this);
    }

    private void RefreshWhileHovered()
    {
        if (!WiringGuard.Require(_presenter, nameof(_presenter), this))
        {
            return;
        }

        _presenter.Refresh(this, ResolveContent());
    }

    private TooltipContent ResolveContent()
    {
        if (_hasDynamicContent)
        {
            return _dynamicContent;
        }

        return new TooltipContent(Localize(_titleLocKey), Localize(_bodyLocKey));
    }

    private static string Localize(string locKey) =>
        string.IsNullOrEmpty(locKey) ? string.Empty : StringTable.GetString(locKey);
}
