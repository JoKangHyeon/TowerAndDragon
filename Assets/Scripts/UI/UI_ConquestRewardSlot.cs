using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 보상 패널의 슬롯 하나. 일반 자원(식량/나무/돌/광물)은 해금되는 종류(아이콘 + 이름)만 표시하고,
// 인구는 실제로 지급되는 수량(아이콘 + 수량)을 같은 프리팹으로 표시한다.
// 라벨 텍스트는 선택 사항이다 - 아이콘만 나열하는 Slot_ChunkIcon처럼 텍스트를 비워 둔 프리팹도 같은 슬롯을 쓴다.
public class UI_ConquestRewardSlot : MonoBehaviour
{
    [SerializeField]
    private Image _iconImage;

    [Tooltip("자원 이름/수량 라벨. 아이콘만 표시하는 프리팹에서는 비워 둔다.")]
    [SerializeField]
    private TMP_Text _resourceNameText;

    // iconColor는 슬라임처럼 공용 스프라이트를 쓰는 자원을 구분하기 위한 틴트다.
    public void Setup(Sprite icon, Color iconColor, string label)
    {
        if (_iconImage != null)
        {
            // 이 슬롯은 ComponentPool로 재사용되므로 icon이 null일 때 대입을 건너뛰면 직전 항목의
            // 아이콘이 그대로 남아 잘못된 내용을 예고한다(스프라이트 없는 몬스터 프리팹 등).
            // 대입은 항상 하고, 표시할 스프라이트가 없으면 빈 사각형이 남지 않도록 그리지 않는다.
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
            _iconImage.enabled = icon != null;
        }

        if (_resourceNameText != null)
        {
            _resourceNameText.text = label;
        }
    }
}
