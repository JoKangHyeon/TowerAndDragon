using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>여러 배타 모드(건설, 점령 등)가 공유하는 입력 액션을 게임 시작 시 한 번만 켠다.
/// 공유 액션은 각 모드 컨트롤러가 개별적으로 Enable/Disable하지 않는다 - 한쪽이 끄면 다른 쪽도
/// 같이 죽어버리므로, 모드 전환은 액션을 끄고 켜는 대신 각 컨트롤러가 자신의 활성 여부를
/// (InputSuppressed, IsActive 등으로) 스스로 판단해 처리한다.</summary>
public class GlobalInputBootstrap : MonoBehaviour
{
    [Tooltip("건설 배치 확정 / 점령 청크 선택처럼 여러 배타 모드가 공유하는 확인 액션.")]
    [SerializeField] private InputActionReference _confirmAction;

    private void OnEnable()
    {
        if (_confirmAction != null)
            _confirmAction.action.Enable();
    }
}
