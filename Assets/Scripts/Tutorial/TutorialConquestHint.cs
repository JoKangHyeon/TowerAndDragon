using UnityEngine;

/// <summary>
/// 튜토리얼 전용 점령 안내. 아직 갈 수 없는 땅을 골랐을 때 패널이 열리지 않게 막고, 그 이유를 알려준다.
///
/// 본게임 경고창(UI_WarningWindow)에 넣지 않고 이 컴포넌트가 따로 처리하는 이유는
/// 이 동작이 튜토리얼에서만 필요하기 때문이다 - 규칙을 아는 플레이어는 패널을 열어 비용과 이유를
/// 확인하고 스스로 닫는다. 처음 하는 플레이어만 점령 버튼이 왜 안 듣는지 모른 채 갇힌다.
///
/// 이 컴포넌트가 없거나 꺼진 씬에서는 질의가 걸리지 않아 패널이 평소대로 열린다.
/// </summary>
public sealed class TutorialConquestHint : MonoBehaviour, IUnreachableChunkSelectQuery
{
    [SerializeField] private ConquestModeController _conquestModeController;
    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("아직 갈 수 없는 땅을 눌렀을 때 띄울 문구의 스트링테이블 키.")]
    [SerializeField] private string _unreachableLandLocKey;

    /// <summary>튜토리얼 중에는 갈 수 없는 땅의 패널을 열지 않는다.</summary>
    bool IUnreachableChunkSelectQuery.CanSelectUnreachableChunk() => false;

    private void OnEnable()
    {
        if (_conquestModeController == null)
        {
            return;
        }

        _conquestModeController.UnreachableSelectQuery = this;
        _conquestModeController.UnreachableChunkBlocked.AddListener(HandleUnreachableChunkBlocked);
    }

    private void OnDisable()
    {
        if (_conquestModeController == null)
        {
            return;
        }

        _conquestModeController.UnreachableChunkBlocked.RemoveListener(HandleUnreachableChunkBlocked);

        // 남이 걸어둔 것을 지우지 않도록 내가 건 경우에만 뗀다(TutorialRunner.ReleaseOpenQuery와 같은 관례).
        if (ReferenceEquals(_conquestModeController.UnreachableSelectQuery, this))
        {
            _conquestModeController.UnreachableSelectQuery = null;
        }
    }

    private void HandleUnreachableChunkBlocked()
    {
        if (_toast != null && !string.IsNullOrWhiteSpace(_unreachableLandLocKey))
        {
            _toast.Show(_unreachableLandLocKey);
        }
    }
}
