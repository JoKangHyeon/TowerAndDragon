using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
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

    [SerializeField] private InputActionReference _babyDragonInventoryToggleAction;

    [Serializable]
    private struct ExclusiveModeShortcut
    {
        [Tooltip("이 단축키에 대응하는 InputAction (예: BuildModeShortCut).")]
        public InputActionReference Action;
        [Tooltip("IExclusiveMode를 구현한 대상 - _exclusiveModeBehaviours에 넣은 것과 같은 오브젝트를 지정할 것(안 그러면 다른 배타 모드를 열 때 이 창이 안 닫힘).")]
        public MonoBehaviour TargetBehaviour;
    }

    [Header("배타 모드 단축키")]
    [Tooltip("단축키 → 배타 모드 열기/닫기 토글 목록. 예: 새끼용 인벤토리(Tab), 건설모드(B), 인구배치(V), 점령(C).")]
    [SerializeField] private ExclusiveModeShortcut[] _exclusiveModeShortcuts;

    // 배타 모드가 열렸을 때 알린다. 버튼·단축키 어느 경로로 열어도 OpenExclusive 하나를 지나므로
    // 여기 한 곳에 붙이면 창이 늘어나도 자동으로 따라온다(DragonEggInventorySystem.GrantEgg와 같은 패턴).
    // IExclusiveMode 구현체는 모두 MonoBehaviour라 구독자가 구체 타입으로 판별할 수 있게 그대로 넘긴다.
    public UnityEvent<MonoBehaviour> ExclusiveModeOpened = new();

    // 배타 모드가 닫혔을 때 알린다. 여는 것과 달리 닫는 데는 단일 통로가 없다 -
    // ESC는 각 창이 자체 처리하고, 버튼도 창의 토글 메서드를 직접 부르며,
    // 다른 모드가 열릴 때도 닫힌다. 그래서 호출 지점을 찾아 붙이는 대신 IsOpen 전이를 여기서 관측한다.
    public UnityEvent<MonoBehaviour> ExclusiveModeClosed = new();

    // 튜토리얼이 배선한다 - 배선되지 않은 씬에서는 null로 남아 모든 창이 그대로 열린다(기존 동작 유지).
    // PopulationManager.CapacityModifierQuery와 같은 주입 방식.
    public IExclusiveModeOpenQuery OpenQuery { get; set; }

    /// <summary>
    /// 지금 열려 있는 배타 모드. 배타이므로 많아야 하나다. 아무것도 안 열려 있으면 null.
    /// 안내가 "창을 닫으세요" 단계에 들어설 때 이미 닫혀 있는지 확인하는 데 쓴다 -
    /// 이벤트만 기다리면 안내보다 먼저 닫은 플레이어는 영영 다음으로 넘어가지 못한다.
    /// </summary>
    public MonoBehaviour CurrentOpenExclusiveMode
    {
        get
        {
            if (_exclusiveModes == null)
            {
                return null;
            }

            foreach (IExclusiveMode mode in _exclusiveModes)
            {
                if (mode.IsOpen)
                {
                    return mode as MonoBehaviour;
                }
            }

            return null;
        }
    }

    private IExclusiveMode[] _exclusiveModes;
    private (InputActionReference action, IExclusiveMode mode)[] _cachedShortcuts;

    // 직전 프레임의 열림 상태. 닫힘 전이를 잡는 데만 쓴다(_exclusiveModes와 같은 인덱스).
    private bool[] _wasExclusiveModeOpen;

    private void Awake()
    {
        // 게임오버 창은 시작 시 항상 꺼진 상태로 보장한다(씬 체크 상태와 무관).
        Hide(_gameOverWindow);
        Hide(_victoryWindow);

        CacheExclusiveModes();
        CacheExclusiveModeShortcuts();
    }

    // 배타 모드 창들은 열려있는 동안 자기 오브젝트를 스스로 비활성화하는 경우가 있어
    // (예: 새끼용 인벤토리, 빌드모드) 창 자신의 Update()로는 다시 열 수 없다.
    // 항상 켜져있는 UIManager가 단축키를 폴링해 직접 열어준다.
    private void Update()
    {
        foreach ((InputActionReference action, IExclusiveMode mode) in _cachedShortcuts)
        {
            if (!action.action.WasPerformedThisFrame())
            {
                continue;
            }

            if (mode.IsOpen)
            {
                // 안내가 이 창 안을 가리키는 중이면 단축키로 닫지 못하게 막는다.
                if (OpenQuery != null && !OpenQuery.CanClose(mode as MonoBehaviour))
                {
                    continue;
                }

                mode.Close();
            }
            else
            {
                OpenExclusive(mode);
            }
        }

        DetectClosedExclusiveModes();
    }

    // 열림→닫힘으로 바뀐 모드를 알린다. 어느 경로로 닫혔든(ESC·버튼·다른 모드 열기) 여기를 지난다.
    private void DetectClosedExclusiveModes()
    {
        for (int i = 0; i < _exclusiveModes.Length; i++)
        {
            bool isOpen = _exclusiveModes[i].IsOpen;

            if (_wasExclusiveModeOpen[i] && !isOpen)
            {
                ExclusiveModeClosed.Invoke(_exclusiveModes[i] as MonoBehaviour);
            }

            _wasExclusiveModeOpen[i] = isOpen;
        }
    }

    // 인스펙터에는 MonoBehaviour로 받고(유니티가 인터페이스 필드를 직렬화하지 못하므로)
    // 여기서 IExclusiveMode로 캐스팅해 둔다. Action이나 대상이 비어있거나 구현하지 않은 항목은
    // 경고만 남기고 제외한다.
    private void CacheExclusiveModeShortcuts()
    {
        if (_exclusiveModeShortcuts == null)
        {
            _cachedShortcuts = Array.Empty<(InputActionReference, IExclusiveMode)>();
            return;
        }

        var shortcuts = new List<(InputActionReference, IExclusiveMode)>(_exclusiveModeShortcuts.Length);
        foreach (ExclusiveModeShortcut shortcut in _exclusiveModeShortcuts)
        {
            if (shortcut.Action == null || shortcut.TargetBehaviour == null)
            {
                Debug.LogWarning("[UIManager] 단축키 항목에 Action 또는 TargetBehaviour가 비어있어 제외됩니다.");
                continue;
            }

            if (shortcut.TargetBehaviour is IExclusiveMode mode)
            {
                shortcuts.Add((shortcut.Action, mode));
            }
            else
            {
                Debug.LogWarning($"[UIManager] {shortcut.TargetBehaviour.name}은 IExclusiveMode를 구현하지 않아 단축키 대상에서 제외됩니다.");
            }
        }

        _cachedShortcuts = shortcuts.ToArray();
    }

    // 인스펙터에는 MonoBehaviour로 받고(유니티가 인터페이스 필드를 직렬화하지 못하므로)
    // 여기서 IExclusiveMode로 캐스팅해 둔다. 구현하지 않은 항목은 경고만 남기고 제외한다.
    private void CacheExclusiveModes()
    {
        if (_exclusiveModeBehaviours == null)
        {
            _exclusiveModes = Array.Empty<IExclusiveMode>();
            _wasExclusiveModeOpen = Array.Empty<bool>();
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
        _wasExclusiveModeOpen = new bool[_exclusiveModes.Length];
    }

    // target을 제외하고 열려 있는 배타 모드를 모두 닫는다.
    // 진입에 인자가 필요해 Open()으로 표현할 수 없는 모드(스킬 타겟팅 등)가 직접 호출한다.
    public void CloseAllExcept(IExclusiveMode target)
    {
        // 열리지 않는 것으로 끝난다 - 이미 열린 창을 닫지도 않는다. 안내 중에 아직 설명하지 않은 창이
        // 열리는 것만 막는 용도라, 거절이 다른 창을 닫는 부작용을 내면 안 된다.
        if (OpenQuery != null && !OpenQuery.CanOpen(target as MonoBehaviour))
        {
            return;
        }

        foreach (IExclusiveMode mode in _exclusiveModes)
        {
            if (!ReferenceEquals(mode, target) && mode.IsOpen)
                mode.Close();
        }
    }

    // target을 제외한 나머지 배타 모드가 열려 있으면 닫고, target을 연다.
    public void OpenExclusive(IExclusiveMode target)
    {
        CloseAllExcept(target);
        target.Open();

        // 열린 뒤에 알린다 - 구독자가 IsOpen을 읽을 수 있어야 한다.
        ExclusiveModeOpened.Invoke(target as MonoBehaviour);
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.AddListener(HandleGameOver);
            _gameManager.VictoryOccurred.AddListener(HandleVictory);
        }

        // 이 단축키는 다른 컴포넌트와 공유하지 않는 UIManager 전용 액션이라 여기서 직접 켠다
        // (공유 액션이면 GlobalInputBootstrap이 켜야 한다).
        if (_babyDragonInventoryToggleAction != null)
        {
            _babyDragonInventoryToggleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.RemoveListener(HandleGameOver);
            _gameManager.VictoryOccurred.RemoveListener(HandleVictory);
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
