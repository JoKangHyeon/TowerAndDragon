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

    public void Setup(Sprite icon, string countText, Color textColor)
    {
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
        }

        if (_countText != null)
        {
            _countText.text = countText;
            _countText.color = textColor;
        }
    }
}
