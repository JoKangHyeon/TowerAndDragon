using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 숫자 1을 누르면 알을 하나 지급하고, 숫자 2를 누르면 하루를 넘긴다.
/// Dragon_window의 Panel_BabyDragon(새끼용 리스트 / 알 리스트)을 확인할 때
/// 알 획득 → 부화 대기 → 부화까지를 실제로 기다리지 않고 바로 돌려보기 위한 도구다.
/// 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화하면 된다.
/// </summary>
public class DragonEggInventoryTester : MonoBehaviour
{
    [Tooltip("알 지급 경로. 모든 알 획득 경로가 GrantEgg 하나만 호출한다.")]
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("하루 넘기기용. CycleManager가 붙은 오브젝트(보통 GameManager).")]
    [SerializeField] private CycleManager _cycleManager;

    // 누를 때마다 5속성을 순환한다 - 같은 속성만 쌓이면 리스트의 속성별 아이콘·이름·틴트를 한 번에 볼 수 없다.
    private static readonly DragonType[] GRANT_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    private int _nextGrantIndex;

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            GrantNextEgg();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            AdvanceOneDay();
        }
    }

    private void GrantNextEgg()
    {
        if (_eggInventorySystem == null)
        {
            return;
        }

        DragonType dragonType = GRANT_ORDER[_nextGrantIndex];
        _nextGrantIndex = (_nextGrantIndex + 1) % GRANT_ORDER.Length;

        _eggInventorySystem.GrantEgg(dragonType);
        Debug.Log($"[DragonEggInventoryTester] 알 지급: {dragonType}");
    }

    // 어느 상태에서 눌러도 OnDayStart가 정확히 한 번 발화되도록, 낮이면 밤을 건너뛰고 다음 낮까지 보낸다.
    // CycleManager.DebugCycle()은 한 '단계'만 넘기므로 낮에서는 두 번 눌러야 해서 여기서는 쓰지 않는다.
    // 밤을 건너뛰므로 그 밤에 스폰된 몬스터가 남을 수 있다 - 필요하면 F키(WaveDebugSkipController)로 웨이브를 정리한다.
    private void AdvanceOneDay()
    {
        if (_cycleManager == null)
        {
            return;
        }

        if (_cycleManager.CurrentCycle == CycleManager.CycleState.Day)
        {
            _cycleManager.EndDay();
        }

        _cycleManager.EndNight();
        Debug.Log($"[DragonEggInventoryTester] 하루 넘김 - 현재 {_cycleManager.CurrentDayNumber}일차");
    }
}
