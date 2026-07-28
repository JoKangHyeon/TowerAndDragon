using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새끼용 인벤토리 슬롯. 알/용 두 모드를 전환한다.
/// 알 모드: 공용 알 아이콘(속성 색 틴트) + 진행도 텍스트.
/// 용 모드: 속성별 스프라이트 + 속성 이름 + 배치 버튼(IsInTower이면 비활성).
/// </summary>
public class UI_DragonInventorySlot : MonoBehaviour
{
    private const string EGG_PROGRESS_LOC_KEY = "baby_dragon_egg_progress";

    [Header("알 모드")]
    [SerializeField] private GameObject _eggRoot;
    [SerializeField] private Image _eggIcon;
    [SerializeField] private TMP_Text _eggProgressText;
    [SerializeField] private TMP_Text _eggAttributeText;

    [Header("용 모드")]
    [SerializeField] private GameObject _dragonRoot;
    [SerializeField] private Image _dragonIcon;
    [SerializeField] private TMP_Text _dragonAttributeText;
    [SerializeField] private Button _dragonButton;
    [Tooltip("배치 중(IsInTower)일 때 슬롯 위에 표시할 오버레이 (선택적).")]
    [SerializeField] private GameObject _inTowerOverlay;

    public void SetupEgg(DragonEgg egg, BabyDragonData data, Sprite eggSprite, Color attributeColor)
    {
        _eggRoot.SetActive(true);
        _dragonRoot.SetActive(false);

        if (_eggIcon != null)
        {
            _eggIcon.sprite = eggSprite;
            _eggIcon.color = attributeColor;
        }

        if (_eggProgressText != null)
        {
            _eggProgressText.text = string.Format(
                StringTable.GetString(EGG_PROGRESS_LOC_KEY),
                egg.FedDayCount,
                data.DaysToHatch);
        }

        if (_eggAttributeText != null)
        {
            _eggAttributeText.text = StringTable.GetString(DragonLocKeys.AttributeLocKey(egg.DragonType));
            _eggAttributeText.color = attributeColor;
        }
    }

    public void SetupDragon(BabyDragon dragon, BabyDragonData data, Color attributeColor, Action<BabyDragon> onClickPlace)
    {
        _eggRoot.SetActive(false);
        _dragonRoot.SetActive(true);

        if (_dragonIcon != null)
        {
            _dragonIcon.sprite = data.Sprite;
            _dragonIcon.color = Color.white;
        }

        if (_dragonAttributeText != null)
        {
            _dragonAttributeText.text = StringTable.GetString(DragonLocKeys.AttributeLocKey(dragon.DragonType));
            _dragonAttributeText.color = attributeColor;
        }

        if (_inTowerOverlay != null)
        {
            _inTowerOverlay.SetActive(dragon.IsInTower);
        }

        if (_dragonButton != null)
        {
            _dragonButton.onClick.RemoveAllListeners();
            _dragonButton.interactable = !dragon.IsInTower;
            if (!dragon.IsInTower)
            {
                _dragonButton.onClick.AddListener(() => onClickPlace(dragon));
            }
        }
    }
}
