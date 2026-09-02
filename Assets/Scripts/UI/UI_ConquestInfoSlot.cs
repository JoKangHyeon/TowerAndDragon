using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 패널의 정보 슬롯 하나(아이콘 + 이름 + 효과값). 소요 기간처럼 항상 존재하는 값은
// 씬에 고정 배치해 값만 갱신하고, 몬스터 강화 효과처럼 적용 여부가 갈리는 값은
// 필요한 만큼만 생성해 사용한다. 둘 다 같은 프리팹(ConquestInfoImage)을 쓴다.
public class UI_ConquestInfoSlot : MonoBehaviour
{
    [Tooltip("비우면 프리팹에 배치된 아이콘을 그대로 쓴다. 아이콘이 고정인 행(가동 상태 행)이 그렇다.")]
    [WiringOptional]
    [SerializeField]
    private Image _iconImage;

    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _valueText;

    // iconColor는 슬라임처럼 공용 스프라이트를 쓰는 자원을 구분하기 위한 틴트다.
    public void Setup(Sprite icon, Color iconColor, string label, string value)
    {
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
        }

        if (_nameText != null)
        {
            _nameText.text = label;
        }

        if (_valueText != null)
        {
            _valueText.text = value;
        }
    }
}
