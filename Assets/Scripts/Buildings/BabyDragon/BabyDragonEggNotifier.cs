using UnityEngine;

/// <summary>
/// DragonEggInventorySystem의 획득/부화 이벤트를 구독해 UI_NotificationToast에 문구를 넘긴다.
/// 토스트가 새끼용 도메인을 몰라도 되게 하는 얇은 연결 계층 - GrantEgg 관문 하나에만 이벤트를 붙였으므로
/// 향후 점령·랜드마크 등 알 획득 경로가 늘어나도 이 브리지를 거쳐 알림이 자동으로 따라온다.
/// </summary>
public class BabyDragonEggNotifier : MonoBehaviour
{
    private const string EGG_GRANTED_LOC_KEY = "baby_dragon_toast_egg_granted";
    private const string EGG_HATCHED_LOC_KEY = "baby_dragon_toast_egg_hatched";

    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private UI_NotificationToast _toast;

    private void OnEnable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatched);
        }
    }

    private void OnDisable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatched);
        }
    }

    private void HandleEggGranted(DragonType type) => ShowToast(EGG_GRANTED_LOC_KEY, type);
    private void HandleEggHatched(DragonType type) => ShowToast(EGG_HATCHED_LOC_KEY, type);

    private void ShowToast(string locKey, DragonType type)
    {
        if (_toast == null)
        {
            return;
        }

        _toast.Show(locKey, StringTable.GetString(DragonLocKeys.AttributeLocKey(type)));
    }
}
