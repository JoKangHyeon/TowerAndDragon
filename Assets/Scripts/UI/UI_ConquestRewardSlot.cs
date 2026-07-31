using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 보상 패널의 슬롯 하나. 일반 자원(식량/나무/돌/광물)은 해금되는 종류(아이콘 + 이름)만 표시하고,
// 인구는 실제로 지급되는 수량(아이콘 + 수량)을 같은 프리팹으로 표시한다.
public class UI_ConquestRewardSlot : MonoBehaviour
{
    [SerializeField]
    private Image _iconImage;

    // iconColor는 슬라임처럼 공용 스프라이트를 쓰는 자원을 구분하기 위한 틴트다.
    public void Setup(Sprite icon, Color iconColor, string label)
    {
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
        }
    }
}
