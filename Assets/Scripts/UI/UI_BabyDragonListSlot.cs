using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 새끼용 리스트(Panel_BabyDragon/Scroll View_BabyDragonList)의 슬롯 하나 - 아이콘과 이름만 표시한다.
// 표시 전용이라 클릭 동작이 없다(그리드 배치는 기존 BabyDragon_window가 계속 담당한다).
// 목록 구성·데이터 조회는 UI_DragonWindow가 하고 이 뷰는 Setup으로 값만 채운다
// (UI_DragonInventorySlot / UI_ConquestRewardSlot과 동일한 값-주입 패턴).
public class UI_BabyDragonListSlot : MonoBehaviour
{
    // 새끼용에는 고유 이름이 없다(RunData.BabyDragon.DragonName은 어디에서도 쓰이지 않는 死필드) -
    // 표시 이름은 로컬라이즈된 속성명이며, 기존 인벤토리 슬롯과 같은 키("{0}")를 재사용한다.
    private const string NAME_LOC_KEY = "baby_dragon_slot_dragon_label";

    [Tooltip("Icon_BabyDragon/Icon 의 Image.")]
    [SerializeField] private Image _icon;

    [Tooltip("Text _dragonName.")]
    [SerializeField] private TMP_Text _nameText;

    public void Setup(BabyDragon dragon, BabyDragonData data)
    {
        if (_icon != null && data.Sprite != null)
        {
            // 속성별로 이미 다른 스프라이트라 틴트가 필요 없다(알 아이콘과 다른 점).
            _icon.sprite = data.Sprite;
            _icon.color = Color.white;
        }

        if (_nameText != null)
        {
            _nameText.text = string.Format(
                StringTable.GetString(NAME_LOC_KEY),
                StringTable.GetString(DragonLocKeys.AttributeLocKey(dragon.DragonType)));
        }
    }
}
