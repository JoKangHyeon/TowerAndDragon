using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>씬 내내 항상 켜져 있어야 하는 입력 액션을 씬 시작 시 한 번만 켠다.
/// 여기 등록하는 것은 두 부류다.
///  1) 여러 배타 모드·창이 공유하는 액션(Confirm, Cancel 등). 이런 액션은 각 모드 컨트롤러·창이
///     개별적으로 Enable/Disable하지 않는다 - 한쪽이 끄면 다른 쪽도 같이 죽어버리므로, 모드 전환은
///     액션을 끄고 켜는 대신 각 컨트롤러가 자신의 활성 여부를 (InputSuppressed, IsActive 등으로)
///     스스로 판단해 처리한다.
///  2) 소비자가 폴링만 하고 아무도 켜지 않는 액션(UIManager의 모드 전환 단축키 C·V·B가 그랬다).
///     "켜는 책임"이 코드 어디에도 드러나지 않아 조용히 누락되기 쉬운 부류라 여기 모아 둔다.
/// 반대로 소유자가 하나뿐이고 그 소유자와 생명주기를 같이해야 하는 액션(CameraController의
/// 이동·줌·북마크 등)은 여기가 아니라 소유자의 OnEnable/OnDisable에서 다룬다.
/// 창(Esc로 닫히는 UI)이 있는 씬에는 타이틀 씬을 포함해 이 부트스트랩이 하나씩 있어야 한다 -
/// 없으면 창들이 꺼져 있는 액션을 구독만 하게 되어 Esc가 조용히 무시된다.
/// 반대로 이 씬에서는 반드시 꺼야 하는 액션은 <see cref="_disabledActions"/>에 넣는다.</summary>
public class GlobalInputBootstrap : MonoBehaviour
{
    [Tooltip("씬 내내 항상 켜져 있어야 하는 액션들. 여러 소비자가 공유하거나(Confirm·Cancel), " +
             "소비자가 폴링만 하고 아무도 켜지 않는 액션(모드 전환 단축키, 보정키)을 모두 넣는다.")]
    [SerializeField] private InputActionReference[] _alwaysEnabledActions;

    [Tooltip("이 씬에서는 꺼둘 액션들. 액션의 켜짐 상태는 씬이 아니라 에셋에 남으므로, " +
             "다른 씬에서 켜고 돌아온 경우까지 확실히 끄려면 목록에서 빼는 것만으로는 부족하고 " +
             "여기 넣어 명시적으로 꺼야 한다. (예: 타이틀 씬의 Esc)")]
    [WiringOptional]
    [SerializeField] private InputActionReference[] _disabledActions;

    private void OnEnable()
    {
        EnableActions();

        // 끄기를 나중에 한다 - 같은 액션이 실수로 양쪽에 들어가도 "꺼짐"으로 수렴시킨다.
        DisableActions();
    }

    private void EnableActions()
    {
        if (_alwaysEnabledActions == null)
        {
            return;
        }

        foreach (InputActionReference action in _alwaysEnabledActions)
        {
            if (action != null)
            {
                action.action.Enable();
            }
        }
    }

    private void DisableActions()
    {
        if (_disabledActions == null)
        {
            return;
        }

        foreach (InputActionReference action in _disabledActions)
        {
            if (action != null)
            {
                action.action.Disable();
            }
        }
    }
}
