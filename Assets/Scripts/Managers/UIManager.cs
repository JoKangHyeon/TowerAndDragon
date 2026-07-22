using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// UI 창을 켜고 끄는 관리자. 창 표시/숨김(Show/Hide)을 담당하고,
/// GameManager의 게임오버 이벤트를 받아 게임오버 창을 띄운다.
/// 배타적 모드(건설/점령 등)는 OpenExclusive로 "누가 닫힐지"만 조정한다 —
/// 각 모드의 열림/닫힘 연출·상태는 자기 자신(IExclusiveMode 구현)이 책임진다.
/// (싱글톤 아님 — SerializeField 주입 관례. 항상 활성인 오브젝트에 둔다.)
/// </summary>
public class UIManager : MonoBehaviour
{
    // 파괴 연출을 볼 수 있도록 게임오버 창 표시를 지연시키는 시간(초).
    private const float GAME_OVER_SHOW_DELAY = 0.5f;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private GameObject _gameOverWindow;

    [Tooltip("한 번에 하나만 열려야 하는 UI 모드 목록(IExclusiveMode 구현체). 예: UI_BuildModeWindow, ConquestModeController.")]
    [SerializeField] private MonoBehaviour[] _exclusiveModeBehaviours;

    private IExclusiveMode[] _exclusiveModes;

    private void Awake()
    {
        // 게임오버 창은 시작 시 항상 꺼진 상태로 보장한다(씬 체크 상태와 무관).
        Hide(_gameOverWindow);

        CacheExclusiveModes();
    }

    // 인스펙터에는 MonoBehaviour로 받고(유니티가 인터페이스 필드를 직렬화하지 못하므로)
    // 여기서 IExclusiveMode로 캐스팅해 둔다. 구현하지 않은 항목은 경고만 남기고 제외한다.
    private void CacheExclusiveModes()
    {
        if (_exclusiveModeBehaviours == null)
        {
            _exclusiveModes = Array.Empty<IExclusiveMode>();
            return;
        }

        var modes = new List<IExclusiveMode>(_exclusiveModeBehaviours.Length);
        foreach (MonoBehaviour behaviour in _exclusiveModeBehaviours)
        {
            if (behaviour is IExclusiveMode mode)
            {
                modes.Add(mode);
            }
            else if (behaviour != null)
            {
                Debug.LogWarning($"[UIManager] {behaviour.name}은 IExclusiveMode를 구현하지 않아 배타 조정 대상에서 제외됩니다.");
            }
        }

        _exclusiveModes = modes.ToArray();
    }

    // target을 제외한 나머지 배타 모드가 열려 있으면 닫고, target을 연다.
    public void OpenExclusive(IExclusiveMode target)
    {
        foreach (IExclusiveMode mode in _exclusiveModes)
        {
            if (!ReferenceEquals(mode, target) && mode.IsOpen)
                mode.Close();
        }

        target.Open();
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.AddListener(HandleGameOver);
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.RemoveListener(HandleGameOver);
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
