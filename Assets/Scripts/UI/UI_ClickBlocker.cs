using UnityEngine;
using UnityEngine.EventSystems;

// 클릭을 흡수만 하고 아무 동작도 하지 않는다. blocker 루트에 "바깥 클릭 닫기" 버튼을 둔
// 모달 창에서, 창 패널 자체에 붙여 안쪽 빈 공간 클릭이 블로커까지 버블링되어
// 창이 닫히는 것을 막는 용도.
public class UI_ClickBlocker : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
    }
}
