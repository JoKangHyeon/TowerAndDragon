using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private GameObject _victoryWindow;

    [Tooltip("한 번에 하나만 열려야 하는 UI 모드 목록(IExclusiveMode 구현체). 예: UI_BuildModeWindow, ConquestModeController.")]
    [SerializeField] private MonoBehaviour[] _exclusiveModeBehaviours;

    [Header("새끼용 인벤토리 단축키")]
    [Tooltip("새끼용 인벤토리 창 열기/닫기 토글 - 보통 Tab.")]
    [SerializeField] private InputActionReference _babyDragonInventoryToggleAction;
    [Tooltip("IExclusiveMode를 구현한 UI_DragonInventoryWindow - _exclusiveModeBehaviours에 넣은 것과 같은 오브젝트를 지정할 것(안 그러면 빌드모드/점령 창을 열 때 이 창이 안 닫힘).")]
    [SerializeField] private MonoBehaviour _babyDragonInventoryWindow;

    private IExclusiveMode[] _exclusiveModes;
    private IExclusiveMode _babyDragonInventoryMode;

    private void Awake()
    {
        // 게임오버 창은 시작 시 항상 꺼진 상태로 보장한다(씬 체크 상태와 무관).
        Hide(_gameOverWindow);
        Hide(_victoryWindow);

        CacheExclusiveModes();
        _babyDragonInventoryMode = _babyDragonInventoryWindow as IExclusiveMode;
    }

    // 새끼용 인벤토리 창은 열려있는 동안 자기 오브젝트(_panel)를 스스로 비활성화하므로(BuildMode와 같은 패턴)
    // 창 자신의 Update()로는 다시 열 수 없다 - 항상 켜져있는 UIManager가 단축키를 폴링해 직접 열어준다.
    private void Update()
    {
        if (_babyDragonInventoryToggleAction == null ||
            _babyDragonInventoryMode == null ||
            !_babyDragonInventoryToggleAction.action.WasPerformedThisFrame())
        {
            return;
        }

        if (_babyDragonInventoryMode.IsOpen)
        {
            _babyDragonInventoryMode.Close();
        }
        else
        {
            OpenExclusive(_babyDragonInventoryMode);
        }
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
            _gameManager.VictoryOccurred.AddListener(HandleVictory);
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

    private void HandleVictory()
    {
        ShowVictoryAfterDelay().Forget();
    }

    private async UniTaskVoid ShowGameOverAfterDelay()
    {
        // GameSpeedManager가 게임오버 직후 Time.timeScale을 0으로 못박으므로,
        // 기본 DelayType.DeltaTime을 쓰면 이 대기가 영원히 끝나지 않는다.
        await UniTask.Delay(
            TimeSpan.FromSeconds(GAME_OVER_SHOW_DELAY),
            DelayType.UnscaledDeltaTime,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        Show(_gameOverWindow);
    }

    private async UniTaskVoid ShowVictoryAfterDelay()
    {
        // 위와 동일한 이유로 unscaled 대기를 쓴다.
        await UniTask.Delay(
            TimeSpan.FromSeconds(GAME_OVER_SHOW_DELAY),
            DelayType.UnscaledDeltaTime,
            cancellationToken: this.GetCancellationTokenOnDestroy()
        );

        Show(_victoryWindow);
    }
}
