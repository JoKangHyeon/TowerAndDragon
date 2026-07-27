using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] F 키를 누르면 진행 중인 웨이브를 즉시 완료한다.
/// 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화한다.
/// </summary>
public class WaveDebugSkipController : MonoBehaviour
{
    [SerializeField] private WaveManager _waveManager;

    private void Update()
    {
        if (_waveManager == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            _waveManager.DebugForceCompleteWave();
        }
    }
}
