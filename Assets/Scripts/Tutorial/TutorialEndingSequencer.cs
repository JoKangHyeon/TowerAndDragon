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
        if (_gameManager != null && _gameManager.GameOverOccurred != null)
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

        await _panel.ShowAsync(token);

        foreach (TutorialEndingBeatSO beat in _cutscene.Beats)
        {
            if (beat == null)
            {
                continue;
            }

            _isNextRequested = false;
            _panel.ShowBeat(beat);

            if (beat.WaitForConfirm)
            {
                await UniTask.WaitUntil(() => _isNextRequested, cancellationToken: token);
            }
            else
            {
                await UniTask.WaitForSeconds(beat.HoldSeconds, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        await _panel.HideAsync(token);
        _endingCompleted.Invoke();
    }
}
