using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] F 키를 누르면 진행 중인 웨이브를 즉시 완료한다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public class WaveDebugSkipController : MonoBehaviour
{
#if UNITY_EDITOR
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
#endif
}
