using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스를 올리고 있는 동안에만 지정한 오브젝트를 켜 두는 범용 컴포넌트.
/// 이 오브젝트(또는 자식) 중 하나가 레이캐스트 대상이어야 한다.
///
/// 켜지는 오브젝트의 내용은 이 컴포넌트가 건드리지 않는다 - 고정 문구는 LocalizedText가,
/// 값에 따라 바뀌는 표시는 소유 창이 채우고, 여기서는 "언제 보이는가"만 맡는다.
/// 마우스를 따라다니며 문구까지 그려야 하면 UI_TooltipTrigger를 쓴다.
/// </summary>
public class UI_HoverReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("마우스를 올렸을 때 켜지는 오브젝트. 보통 이 오브젝트의 자식으로 둔다.")]
    [SerializeField] private GameObject _target;

    [Tooltip("이 오브젝트의 버튼이 비활성(interactable=false)이면 마우스를 올려도 켜지 않는다. " +
        "누를 수 없는 버튼의 안내까지 뜨면 지금 할 수 있는 일로 오해하게 된다. " +
        "같은 오브젝트에 버튼이 없으면 이 설정은 무시된다.")]
    [SerializeField] private bool _hideWhileButtonDisabled = true;

    // 버튼 위에 붙는 것이 정상 사용이라 인스펙터로 잇지 않고 직접 찾는다(없어도 된다).
    private Button _button;
    private bool _isHovered;

    // 인스펙터에서는 켠 채로 두고 배치하는 편이 편하므로, 시작 시 꺼진 상태를 코드가 보장한다.
    private void Awake()
    {
        _button = GetComponent<Button>();
        SetTargetActive(false);
    }

    // 호버 중에 이 오브젝트가 꺼지면 OnPointerExit가 오지 않아 대상만 화면에 남는다.
    private void OnDisable()
    {
        _isHovered = false;
        SetTargetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        Sync();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        SetTargetActive(false);
    }

    // 마우스를 올린 채로 버튼이 비활성으로 바뀌는 경우가 있다(철거 직후 선택이 풀리는 등) -
    // 그때 안내만 남지 않도록 호버 중에는 계속 다시 맞춘다. interactable을 매 프레임 쓰는 쪽
    // (UI_BuildModeWindow.Update)보다 뒤에 읽어야 하므로 LateUpdate에서 본다
    // (UI_ButtonInteractableFade와 같은 이유).
    private void LateUpdate()
    {
        if (_isHovered && IsGated)
        {
            Sync();
        }
    }

    private bool IsGated => _hideWhileButtonDisabled && _button != null;

    private void Sync() => SetTargetActive(_isHovered && (!IsGated || _button.IsInteractable()));

    private void SetTargetActive(bool isActive)
    {
        if (_target != null && _target.activeSelf != isActive)
        {
            _target.SetActive(isActive);
        }
    }
}
