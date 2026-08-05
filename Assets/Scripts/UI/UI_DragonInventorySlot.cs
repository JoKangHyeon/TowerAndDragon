using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새끼용 인벤토리 슬롯. 알/용 두 모드를 전환한다. 슬롯 자체가 배치 버튼 역할을 한다(UI_BuildingSlot과 동일한 방식).
/// 알 모드: 공용 알 아이콘(속성 색 틴트) + 부화 진행도 텍스트. 클릭 동작 없음.
/// 용 모드: 속성별 스프라이트(틴트 없음) + 먹이 슬라임 아이콘(공용 스프라이트, 속성 색 틴트) + 양. 클릭하면 배치 시작.
/// </summary>
[RequireComponent(typeof(Button))]
public class UI_DragonInventorySlot : MonoBehaviour
{
    private const string EGG_PROGRESS_LOC_KEY = "baby_dragon_egg_progress";
    private const string FEED_INFO_LOC_KEY = "baby_dragon_feed_info";
    private const string EGG_LABEL_LOC_KEY = "baby_dragon_slot_egg_label";
    private const string DRAGON_LABEL_LOC_KEY = "baby_dragon_slot_dragon_label";

    [Header("공통")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _attributeText;

    [Header("알 전용")]
    [SerializeField] private TMP_Text _hatchProgressText;

    [Header("용 전용")]
    [SerializeField] private Image _feedIcon;
    [SerializeField] private TMP_Text _feedAmountText;
    [Tooltip("먹이 슬라임 아이콘 조회용.")]
    [SerializeField] private ResourceCatalog _resourceCatalog;

    public void SetupEgg(DragonEgg egg, BabyDragonData data, Sprite eggSprite, Color attributeColor)
    {
        _icon.sprite = eggSprite;
        _icon.color = attributeColor; // 공용 알 스프라이트라 틴트로 속성을 구분한다.
        ApplyAttributeText(egg.DragonType, isDragonMode: false);
        SetModeVisibility(isDragonMode: false);

        _hatchProgressText.text = string.Format(
            StringTable.GetString(EGG_PROGRESS_LOC_KEY),
            egg.FedDayCount,
            data.DaysToHatch);

        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.interactable = false; // 알은 클릭 동작이 없다(부화 대기).
    }

    // 속성 색 파라미터는 없다 - 용 스프라이트와 먹이 슬라임 아이콘 모두 속성별 전용 스프라이트를
    // 쓰게 되어 틴트가 필요 없어졌다(알 모드는 여전히 공용 스프라이트라 SetupEgg만 색을 받는다).
    public void SetupDragon(
        BabyDragon dragon,
        BabyDragonData data,
        int previewDailyFeed,
        bool isPlaceable,
        Action<BabyDragon> onClickPlace)
    {
        _icon.sprite = data.Sprite;
        _icon.color = Color.white; // 속성별로 이미 다른 스프라이트라 틴트가 필요 없다.
        ApplyAttributeText(dragon.DragonType, isDragonMode: true);
        SetModeVisibility(isDragonMode: true);

        if (DragonSlimeTable.TryGetFeedSlime(dragon.DragonType, out ResourceType slimeType) &&
            _resourceCatalog != null &&
            _resourceCatalog.TryGet(slimeType, out ResourceData resourceData))
        {
            // 슬라임은 속성별 전용 스프라이트를 갖고 있으므로 틴트하지 않는다
            // (예전에는 공용 흰 스프라이트 하나를 속성 색으로 구분했다).
            _feedIcon.sprite = resourceData.Icon;
            _feedIcon.color = Color.white;
        }

        _feedAmountText.text = string.Format(StringTable.GetString(FEED_INFO_LOC_KEY), previewDailyFeed);

        Button button = GetComponent<Button>();
        // 밤에는 배치를 시작할 수 없다(BuildingPlacementController.IsDayForBuildActions와 같은 판정).
        button.interactable = isPlaceable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClickPlace(dragon));
    }

    private void ApplyAttributeText(DragonType dragonType, bool isDragonMode)
    {
        string attributeName = StringTable.GetString(DragonLocKeys.AttributeLocKey(dragonType));
        string labelLocKey = isDragonMode ? DRAGON_LABEL_LOC_KEY : EGG_LABEL_LOC_KEY;
        _attributeText.text = string.Format(StringTable.GetString(labelLocKey), attributeName);
    }

    private void SetModeVisibility(bool isDragonMode)
    {
        _hatchProgressText.gameObject.SetActive(!isDragonMode);
        _feedIcon.gameObject.SetActive(isDragonMode);
        _feedAmountText.gameObject.SetActive(isDragonMode);
    }
}
