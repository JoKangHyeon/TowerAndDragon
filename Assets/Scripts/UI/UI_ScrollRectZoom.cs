using UnityEngine;
using UnityEngine.EventSystems;

// 스크롤뷰 내용물을 마우스 휠로 확대·축소한다(스킬트리처럼 넓은 내용을 담는 스크롤뷰용).
//
// ScrollRect는 휠을 '스크롤'로 쓰기 때문에 같은 오브젝트에 이 컴포넌트를 붙이면 둘 다 반응한다
// (EventSystem은 한 오브젝트의 같은 인터페이스 구현을 모두 호출한다).
// 그래서 이 컴포넌트는 ScrollRect보다 아래 계층(보통 Viewport)에 붙여야 한다 -
// EventSystem이 포인터가 가리킨 오브젝트에서 부모로 올라가며 '첫' IScrollHandler를 찾으므로,
// Viewport에 있는 이쪽이 부모의 ScrollRect보다 먼저 잡혀 휠을 가로챈다. 드래그 팬은 그대로 동작한다.
//
// 붙이는 오브젝트에 raycastTarget인 Graphic이 있어야 빈 공간에서도 휠이 먹는다
// (없으면 노드 위에 커서가 있을 때만 이벤트가 올라온다).
public class UI_ScrollRectZoom : MonoBehaviour, IScrollHandler
{
    [Tooltip("확대·축소할 대상. 보통 ScrollRect의 Content.")]
    [SerializeField] private RectTransform _target;

    [Tooltip("휠 한 칸당 배율 변화량.")]
    [SerializeField] private float _zoomStep = 0.1f;

    [SerializeField] private float _minZoom = 0.5f;
    [SerializeField] private float _maxZoom = 2f;

    public void OnScroll(PointerEventData eventData)
    {
        if (!WiringGuard.Require(_target, nameof(_target), this))
        {
            return;
        }

        // scrollDelta의 크기는 입력 모듈·플랫폼마다 다르므로(±1, ±120 등) 부호만 쓴다 -
        // 어디서나 '휠 한 칸 = _zoomStep 한 번'으로 일정하게 만든다.
        float direction = Mathf.Sign(eventData.scrollDelta.y);
        if (Mathf.Approximately(direction, 0f))
        {
            return;
        }

        float zoom = Mathf.Clamp(_target.localScale.x + direction * _zoomStep, _minZoom, _maxZoom);

        // z는 1로 유지한다 - UI RectTransform에서 z 스케일은 렌더링에 쓰이지 않는다.
        _target.localScale = new Vector3(zoom, zoom, 1f);
    }
}
