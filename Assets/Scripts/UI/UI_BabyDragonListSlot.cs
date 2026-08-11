using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 새끼용 리스트(Panel_BabyDragon/Scroll View_BabyDragonList)의 슬롯 하나.
// 아이콘·이름을 표시하고, Icon_Focus 버튼으로 배치 시작 / 배치된 개체로 카메라 이동을 요청한다.
// 마우스를 올리면 우측 Panel_DragonInfo에 이 용의 속성 설명을 띄우도록 알린다.
// 배치·카메라·설명 표시 모두 이 뷰가 직접 하지 않는다 - 콜백으로 UI_DragonWindow에 넘긴다
// (UI_DragonInventorySlot.SetupDragon의 onClickPlace와 같은 방식).
public class UI_BabyDragonListSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 새끼용에는 고유 이름이 없다(RunData.BabyDragon.DragonName은 어디에서도 쓰이지 않는 死필드) -
    // 표시 이름은 로컬라이즈된 속성명이며, 기존 인벤토리 슬롯과 같은 키("{0}")를 재사용한다.
    private const string NAME_LOC_KEY = "baby_dragon_slot_dragon_label";

    [Tooltip("Icon_BabyDragon/Icon 의 Image.")]
    [SerializeField] private Image _icon;

    [Tooltip("Text _dragonName.")]
    [SerializeField] private TMP_Text _nameText;

    [Header("Icon_Focus - 미배치/배치 상태에 따라 그림과 동작이 바뀐다")]
    [Tooltip("Icon_Focus 의 Image.")]
    [SerializeField] private Image _focusIcon;

    [Tooltip("Icon_Focus 의 Button.")]
    [SerializeField] private Button _focusButton;

    [Tooltip("미배치 상태 아이콘 - 누르면 그리드 배치를 시작한다.")]
    [SerializeField] private Sprite _placeSprite;

    [Tooltip("배치 상태 아이콘 - 누르면 카메라를 그 개체로 옮긴다.")]
    [SerializeField] private Sprite _focusSprite;

    // 이 슬롯이 현재 표시 중인 개체. 호버 콜백에 실어 보낸다.
    private BabyDragon _dragon;

    // (개체, 올라감/벗어남) - 슬롯은 풀에서 재사용되므로 Setup마다 새로 받는다.
    private Action<BabyDragon, bool> _onHoverChanged;

    /// <summary>튜토리얼이 위치 표시/신발 버튼만 정확히 강조할 때 사용한다.</summary>
    public bool TryGetFocusButtonRect(out RectTransform buttonRect)
    {
        buttonRect = _focusButton == null ? null : _focusButton.transform as RectTransform;
        return buttonRect != null;
    }

    public void Setup(
        BabyDragon dragon,
        BabyDragonData data,
        Action<BabyDragon> onFocusClicked,
        Action<BabyDragon, bool> onHoverChanged)
    {
        _dragon = dragon;
        _onHoverChanged = onHoverChanged;

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

        if (_focusIcon != null)
        {
            // 스프라이트가 지정되지 않은 상태에서는 프리팹에 authoring된 그림을 그대로 둔다.
            Sprite stateSprite = dragon.IsInTower ? _focusSprite : _placeSprite;
            if (stateSprite != null)
            {
                _focusIcon.sprite = stateSprite;
            }
        }

        if (_focusButton != null)
        {
            // 슬롯은 풀에서 재사용되므로 이전에 붙은 다른 개체의 리스너를 반드시 지운다.
            _focusButton.onClick.RemoveAllListeners();
            _focusButton.onClick.AddListener(() => onFocusClicked(dragon));
        }
    }

    // 루트에 raycastTarget이 켜진 Image가 슬롯 전체를 덮고 있어 아이콘 사이 여백에서도 잡힌다.
    // 자식(아이콘·이름·Icon_Focus) 위에서도 EventSystem이 부모까지 enter/exit를 올려보낸다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _onHoverChanged?.Invoke(_dragon, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onHoverChanged?.Invoke(_dragon, false);
    }
}
