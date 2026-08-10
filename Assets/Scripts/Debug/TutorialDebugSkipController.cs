using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 튜토리얼을 챕터·날짜 단위로 건너뛴다. 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화한다.
///
/// F1 - 지금 챕터 하나만 건너뛴다.
/// F2 - 다음 날 챕터가 열릴 때까지 건너뛰고 밤까지 넘긴다(= 하루 점프).
/// F3 - 남은 챕터를 전부 건너뛴다.
///
/// 챕터를 끄고 켜는 것은 TutorialScenarioController가 TutorialEnded를 듣고 하므로 여기서는
/// 러너의 Skip()만 부른다 - 직접 SetActive를 만지면 지급 컴포넌트가 붙은 챕터 이음매를 건너뛰게 된다.
///
/// 밤 넘기기가 한 프레임 뒤인 이유: EndDay는 튜토리얼이 건 DayEndBlockQuery에 막히는데,
/// 그 잠금은 러너의 OnDisable에서 풀린다. Skip() 직후에는 아직 러너가 켜져 있어 막힌다.
/// </summary>
public sealed class TutorialDebugSkipController : MonoBehaviour
{
    // 한 번의 하루 점프가 지나칠 수 있는 챕터 수의 상한. 무한 루프 방지용이다.
    private const int MAX_CHAPTERS_PER_DAY = 32;

    [SerializeField] private TutorialScenarioController _scenarioController;

    [Tooltip("하루 점프(F2)에서 밤으로 넘길 때 씁니다. 비우면 챕터만 건너뛰고 날짜는 그대로입니다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("하루 점프에서 밤 웨이브를 즉시 끝낼 때 씁니다. 비우면 밤 웨이브를 직접 처리해야 합니다.")]
    [SerializeField] private WaveManager _waveManager;

    private bool _isJumping;

    private void Update()
    {
        if (_scenarioController == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            SkipCurrentChapter();
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            JumpOneDayAsync().Forget();
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            SkipAllChaptersAsync().Forget();
        }
    }

    private bool SkipCurrentChapter()
    {
        TutorialRunner runner = _scenarioController.DebugCurrentRunner;

        if (runner == null || !runner.gameObject.activeInHierarchy)
        {
            Debug.Log("[TutorialDebugSkipController] 지금 돌고 있는 챕터가 없습니다.", this);
            return false;
        }

        Debug.Log($"[TutorialDebugSkipController] 챕터 건너뛰기: {runner.name}", this);
        runner.Skip();
        return true;
    }

    /// <summary>
    /// 다음 챕터가 "다음 날"에 열리는 것일 때까지 지금 날의 챕터를 모두 건너뛴 뒤 밤을 넘긴다.
    /// 챕터 전환은 TutorialEnded -> HandleChapterEnded로 한 프레임 안에 끝나지 않으므로 매번 기다린다.
    /// </summary>
    private async UniTaskVoid JumpOneDayAsync()
    {
        if (_isJumping)
        {
            return;
        }

        _isJumping = true;

        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            int startDay = _scenarioController.DebugCurrentChapterStartDay;

            for (int i = 0; i < MAX_CHAPTERS_PER_DAY; i++)
            {
                // 대기 중인 챕터가 생겼다면 이미 "다음 날에 열릴 것"만 남은 것이다.
                if (_scenarioController.DebugHasPendingChapter)
                {
                    break;
                }

                int nextStartDay = _scenarioController.DebugNextChapterStartDay;

                // 다음 챕터가 다음 날 것이면 여기서 멈추고 밤으로 넘긴다.
                if (nextStartDay > startDay)
                {
                    break;
                }

                if (!SkipCurrentChapter())
                {
                    break;
                }

                await UniTask.Yield(token);
            }

            // 마지막 챕터까지 건너뛴 경우에도 한 번 더 넘겨야 그 날이 끝난다.
            if (!SkipCurrentChapter())
            {
                await UniTask.Yield(token);
            }

            await AdvanceNightAsync(token);
        }
        finally
        {
            _isJumping = false;
        }
    }

    private async UniTaskVoid SkipAllChaptersAsync()
    {
        if (_isJumping)
        {
            return;
        }

        _isJumping = true;

        try
        {
            var token = this.GetCancellationTokenOnDestroy();

            for (int i = 0; i < MAX_CHAPTERS_PER_DAY; i++)
            {
                if (!SkipCurrentChapter())
                {
                    break;
                }

                await UniTask.Yield(token);
            }

            Debug.Log("[TutorialDebugSkipController] 남은 챕터를 모두 건너뛰었습니다.", this);
        }
        finally
        {
            _isJumping = false;
        }
    }

    /// <summary>
    /// 밤으로 넘기고, 웨이브가 있으면 즉시 끝내 다음 낮까지 보낸다.
    /// 러너가 꺼지며 DayEndBlockQuery가 풀리기를 한 프레임 기다린 뒤에 EndDay를 부른다.
    /// </summary>
    private async UniTask AdvanceNightAsync(System.Threading.CancellationToken token)
    {
        if (_cycleManager == null)
        {
            Debug.Log("[TutorialDebugSkipController] CycleManager가 없어 날짜는 그대로 둡니다.", this);
            return;
        }

        await UniTask.Yield(token);

        if (_cycleManager.CurrentCycle != CycleManager.CycleState.Day)
        {
            return;
        }

        _cycleManager.ForceEndDay();

        if (_cycleManager.CurrentCycle != CycleManager.CycleState.Night)
        {
            Debug.LogWarning(
                "[TutorialDebugSkipController] 밤으로 넘어가지 못했습니다 - 아직 무언가가 낮을 잡고 있습니다.",
                this);

            return;
        }

        if (_waveManager == null)
        {
            Debug.Log("[TutorialDebugSkipController] WaveManager가 없어 밤은 직접 끝내야 합니다.", this);
            return;
        }

        // 스폰이 시작될 틈을 준 뒤에 끝낸다 - 스폰 전에 부르면 끝낼 웨이브가 없어 밤이 남는다.
        await UniTask.Yield(token);
        _waveManager.DebugForceCompleteWave();
    }
}
