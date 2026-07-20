using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 숫자 0을 누르면 진행 중인 모든 원정을 즉시 완료 처리한다.
/// 며칠 걸리는 점령 대기를 기다리지 않고 완료 후 흐름(주둔지 배치, 보상 등)을 바로 확인할 때 쓴다.
/// 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화하면 된다.
/// </summary>
public class ConquestExpeditionTester : MonoBehaviour
{
    [SerializeField] private ConquestManager _conquestManager;

    private void Update()
    {
        if (_conquestManager == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            _conquestManager.DebugForceCompleteAllExpeditions();
        }
    }
}
