using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 엔딩 컷씬 진행. 성이 무너진 순간(GameOverOccurred)을 받아 컷을 순서대로 재생하고,
/// 다 끝나면 EndingCompleted를 발행한다. 본게임으로 넘어가는 일은 여기서 하지 않는다 -
/// "연출이 끝났다"와 "씬을 옮긴다"를 한 컴포넌트가 같이 쥐면 연출만 다시 보고 싶을 때 손댈 곳이 없다.
///
/// 모든 대기는 unscaled여야 한다 - 이 시점엔 GameSpeedManager가 Time.timeScale을 0으로 굳혀 놨다.
/// </summary>
public sealed class TutorialEndingSequencer : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private UI_TutorialEndingPanel _panel;
    [SerializeField] private TutorialEndingCutsceneSO _cutscene;

    [SerializeField] private UnityEvent _endingCompleted = new();

    public UnityEvent EndingCompleted => _endingCompleted;

    private bool _hasStarted;
    private bool _isNextRequested;

    private void OnEnable()
    {
        // GameOverOccurred는 프로퍼티가 아니라 직렬화 필드라 씬 설정에 따라 비어 있을 수 있다.
        //
        // 이 구독이 빠지면 성이 무너져도 엔딩이 시작되지 않는다. 튜토리얼 씬은 게임오버 창을 의도적으로
        // 비워 두고 GameSpeedManager가 게임오버에서 timeScale을 0으로 고정하므로, 그 상태는
        // "컷씬이 안 나온다"가 아니라 화면이 멈춘 채 아무 버튼도 없는 완전 정지다.
        if (WiringGuard.Require(_gameManager, nameof(_gameManager), this) &&
            WiringGuard.RequireRef(_gameManager.GameOverOccurred, nameof(_gameManager.GameOverOccurred), this))
        {
            _gameManager.GameOverOccurred.AddListener(HandleGameOver);
        }

        if (_panel != null)
        {
            _panel.NextClicked += HandleNextClicked;
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null && _gameManager.GameOverOccurred != null)
        {
            _gameManager.GameOverOccurred.RemoveListener(HandleGameOver);
        }

        if (_panel != null)
        {
            _panel.NextClicked -= HandleNextClicked;
        }
    }

    private void HandleNextClicked() => _isNextRequested = true;

    private void HandleGameOver()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        PlayAsync().Forget();
    }

    private async UniTaskVoid PlayAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        if (_panel == null || _cutscene == null || _cutscene.Beats.Count == 0)
        {
            Debug.LogWarning("[TutorialEndingSequencer] 재생할 컷씬이 없어 곧바로 종료를 알립니다.", this);
            _endingCompleted.Invoke();
            return;
        }

        // 페이드 인은 첫 컷을 그린 뒤에 시작한다. 순서를 뒤집으면 프리팹에 저장된 상태 -
        // 스프라이트가 비어 있는 일러스트 자리와 빈 문구 - 가 페이드 내내 보이고,
        // 그 다음에야 내용이 채워져 "이미지가 없는 패널이 잠깐 떴다 바뀐다"로 읽힌다.
        bool hasFadedIn = false;

        foreach (TutorialEndingBeatSO beat in _cutscene.Beats)
        {
            if (beat == null)
            {
                continue;
            }

            _isNextRequested = false;
            _panel.ShowBeat(beat);

            if (!hasFadedIn)
            {
                hasFadedIn = true;
                await _panel.ShowAsync(token);
            }

            // 확인 대기는 눌릴 버튼이 있을 때만 성립한다. 패널에 Next 버튼이 배선되지 않았는데
            // 그냥 기다리면 아무도 깨울 수 없는 대기가 되고, 게임오버 창이 없는 이 씬에서는 그대로 정지다.
            // 그럴 때는 시간으로 넘겨 컷씬이 끝까지는 흐르게 한다.
            bool canWaitForConfirm = beat.WaitForConfirm && _panel.HasNextButton;

            if (beat.WaitForConfirm && !canWaitForConfirm)
            {
                Debug.LogError(
                    "[TutorialEndingSequencer] 확인 대기 컷인데 패널에 Next 버튼이 없어 시간으로 넘깁니다 - " +
                    "UI_TutorialEndingPanel의 _nextButton 배선을 확인하세요.", this);
            }

            if (canWaitForConfirm)
            {
                await UniTask.WaitUntil(() => _isNextRequested, cancellationToken: token);
            }
            else
            {
                await UniTask.WaitForSeconds(beat.HoldSeconds, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        // 패널을 걷기 전에 알린다. 받는 쪽(TutorialToGameHandoff)이 로딩 화면으로 덮은 뒤에 걷혀야
        // 튜토리얼 맵이 다시 드러나지 않는다 - 순서를 뒤집으면 패널 fade-out 내내 맵이 보인다.
        // 씬을 옮기는 일은 여전히 여기서 하지 않는다. 알리는 시점만 컷씬 내용이 끝난 순간으로 옮긴 것이다.
        _endingCompleted.Invoke();
        await _panel.HideAsync(token);
    }
}
