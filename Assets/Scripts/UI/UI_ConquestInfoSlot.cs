using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 패널의 정보 슬롯 하나(아이콘 + 이름 + 효과값). 소요 기간처럼 항상 존재하는 값은
// 씬에 고정 배치해 값만 갱신하고, 몬스터 강화 효과처럼 적용 여부가 갈리는 값은
// 필요한 만큼만 생성해 사용한다. 둘 다 같은 프리팹(ConquestInfoImage)을 쓴다.
public class UI_ConquestInfoSlot : MonoBehaviour
{
    [SerializeField]
    private Image _iconImage;

    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _valueText;

    public void Setup(Sprite icon, string label, string value)
    {
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
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
