using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// UI 창을 켜고 끄는 관리자. 창 표시/숨김(Show/Hide)을 담당하고,
/// GameManager의 게임오버 이벤트를 받아 게임오버 창을 띄운다.
/// (싱글톤 아님 — SerializeField 주입 관례. 항상 활성인 오브젝트에 둔다.)
/// </summary>
public class UIManager : MonoBehaviour
{
    // 파괴 연출을 볼 수 있도록 게임오버 창 표시를 지연시키는 시간(초).
    private const float GAME_OVER_SHOW_DELAY = 0.5f;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private GameObject _gameOverWindow;

    private void Awake()
    {
        // 게임오버 창은 시작 시 항상 꺼진 상태로 보장한다(씬 체크 상태와 무관).
        Hide(_gameOverWindow);
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred += HandleGameOver;
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred -= HandleGameOver;
        }
    }

    public void Show(GameObject window)
    {
        if (window != null)
        {
            window.SetActive(true);
        }
    }

    public void Hide(GameObject window)
    {
        if (window != null)
        {
            window.SetActive(false);
        }
    }

    private void HandleGameOver()
    {
        ShowGameOverAfterDelay().Forget();
    }

    private async UniTaskVoid ShowGameOverAfterDelay()
    {
        await UniTask.Delay(
            TimeSpan.FromSeconds(GAME_OVER_SHOW_DELAY),
            cancellationToken: this.GetCancellationTokenOnDestroy());

        Show(_gameOverWindow);
    }
}
