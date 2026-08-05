using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 패널의 자원 비용 슬롯 하나(아이콘 + 보유/필요 텍스트). 표시만 담당하며 판단은 호출자가 한다.
public class UI_ResourceCostSlot : MonoBehaviour
{
    [SerializeField]
    private Image _iconImage;

    [SerializeField]
    private TMP_Text _countText;

    // iconColor는 아이콘에 씌울 틴트다. 지금은 모든 자원이 고유 아이콘을 가지므로
    // 호출부가 Color.white(틴트 없음)를 넘긴다 - 슬라임도 속성별 전용 스프라이트가 생겼다.
    public void Setup(Sprite icon, Color iconColor, string countText, Color textColor)
    {
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
        }

        if (_countText != null)
        {
            _countText.text = countText;
            _countText.color = textColor;
        }
    }
}
