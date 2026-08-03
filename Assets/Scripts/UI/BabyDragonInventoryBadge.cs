using UnityEngine;

/// <summary>
/// 새끼용 인벤토리 HUD 버튼의 미확인 알림 표시(작은 동그라미). 알/용 탭별로 "미확인이 있다/없다"만
/// 따로 추적하다가, 해당 탭을 실제로 봤을 때(UI_DragonInventoryWindow.OnTabDisplayed)만 그 몫을 지운다.
/// 이 표시 상태는 튜토리얼 진행도와 구분되는 UI 전용 상태라 RunData에는 넣지 않는다.
/// </summary>
public class BabyDragonInventoryBadge : MonoBehaviour
{
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private UI_DragonInventoryWindow _inventoryWindow;

    [Tooltip("새끼용 인벤토리 버튼 구석에 표시되는 작은 동그라미.")]
    [SerializeField] private GameObject _badgeDot;

    private bool _hasUnreadEgg;
    private bool _hasUnreadDragon;

    private void OnEnable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatched);
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.AddListener(HandleTabDisplayed);
        }

        // 이벤트는 변경 시에만 발생하므로, 활성화 시점의 현재 값을 한 번 직접 채워준다.
        Render();
    }

    private void OnDisable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatched);
        }

        if (_inventoryWindow != null)
        {
            _inventoryWindow.OnTabDisplayed.RemoveListener(HandleTabDisplayed);
        }
    }

    // 알 지급은 알 탭에 쌓이므로 알 탭 몫을 켠다.
    private void HandleEggGranted(DragonType _)
    {
        _hasUnreadEgg = true;
        Render();
    }

    // 부화하면 알 탭에서 용 탭으로 옮겨가므로 용 탭 몫을 켠다.
    private void HandleEggHatched(DragonType _)
    {
        _hasUnreadDragon = true;
        Render();
    }

    private void HandleTabDisplayed(bool isDragonTab)
    {
        if (isDragonTab)
        {
            _hasUnreadDragon = false;
        }
        else
        {
            _hasUnreadEgg = false;
        }

        Render();
    }

    private void Render()
    {
        if (_badgeDot != null)
        {
            _badgeDot.SetActive(_hasUnreadEgg || _hasUnreadDragon);
        }
    }
}
