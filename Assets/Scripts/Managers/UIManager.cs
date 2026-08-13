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

    [Tooltip("안내 딤이 떠 있는 동안 단축키를 막는 데 쓴다. 비우면 딤과 무관하게 늘 허용한다.")]
    [SerializeField] private UI_GuideOverlay _guideOverlay;

    [Header("Esc - 기본 창")]
    [Tooltip("아무 창도 열려 있지 않을 때 Esc로 여는 창(보통 UI_ConfigWindow). IExclusiveMode 구현체여야 한다.")]
    [SerializeField] private MonoBehaviour _escapeWindowBehaviour;

    [Tooltip("창을 닫는 키 - 보통 Esc. 각 창이 자기를 닫으려고 구독하는 것과 같은 액션을 넣는다.")]
    [SerializeField] private InputActionReference _escapeAction;

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

    // 튜토리얼이 등록한다 - 아무도 등록하지 않은 씬에서는 비어 있어 모든 창이 그대로 열린다(기존 동작 유지).
    //
    // 슬롯 하나가 아니라 목록인 이유: 챕터 안내와 팁 체인이 동시에 돌 수 있는데, 슬롯 하나를 서로
    // 덮어쓰면 나중에 온 쪽이 앞의 관문을 지우고 물러날 때 통째로 풀어버린다
    // (CycleManager._dayEndBlockers를 목록으로 둔 것과 같은 이유다).
    private readonly List<IExclusiveModeOpenQuery> _openQueries = new();

    /// <summary>하나라도 거절하면 열리지 않는다. 같은 대상을 두 번 넣어도 한 번만 등록된다.</summary>
    public void AddOpenQuery(IExclusiveModeOpenQuery query)
    {
        if (query != null && !_openQueries.Contains(query))
        {
            _openQueries.Add(query);
        }
    }

    /// <summary>등록을 뗀다. 자기가 넣은 것만 빼므로 남의 관문은 건드리지 않는다.</summary>
    public void RemoveOpenQuery(IExclusiveModeOpenQuery query)
    {
        _openQueries.Remove(query);
    }

    private bool CanOpenByQueries(MonoBehaviour target)
    {
        foreach (IExclusiveModeOpenQuery query in _openQueries)
        {
            if (query != null && !query.CanOpen(target))
            {
                return false;
            }
        }

        return true;
    }

    // 단축키 관문. _openQueries와 목록을 따로 두는 이유는 거는 조건이 다르기 때문이다 -
    // 창 열기 관문은 1일차 강제 안내만 걸지만(플레이어가 스스로 연 팁 체인까지 걸면 안내를 켠 대가로
    // 게임이 잠긴다), 단축키는 그 팁 체인이 도는 동안에도 막혀야 한다. 딤이 마우스를 막고 있는데
    // 키보드만 통과해 건설·점령·워커 창을 여닫을 수 있었다.
    private readonly List<IShortcutBlockQuery> _shortcutQueries = new();

    /// <summary>단축키 관문을 건다. 같은 대상을 두 번 넣어도 한 번만 등록된다.</summary>
    public void AddShortcutQuery(IShortcutBlockQuery query)
    {
        if (query != null && !_shortcutQueries.Contains(query))
        {
            _shortcutQueries.Add(query);
        }
    }

    /// <summary>등록을 뗀다. 자기가 넣은 것만 빼므로 남의 관문은 건드리지 않는다.</summary>
    public void RemoveShortcutQuery(IShortcutBlockQuery query)
    {
        _shortcutQueries.Remove(query);
    }

    /// <summary>
    /// 지금 이 단축키를 받아도 되는지. <b>새 단축키를 추가하면 폴링 지점에서 이것부터 물을 것</b> -
    /// 그러지 않으면 키보드만 안내의 딤을 통과해 마우스로는 막아둔 조작이 키로는 된다.
    /// <paramref name="mode"/>가 null이면 배타 모드와 무관한 단축키다(일시정지 등).
    ///
    /// 기본 차단은 딤 여부로 정하고(마우스와 같은 기준), 그 위에 관문들이 두 방향으로 덧붙인다 -
    /// 딤이 없어도 계속 막아야 하는 안내가 있고, 막힌 중에도 지금 단계가 시킨 키는 통과해야 한다.
    /// </summary>
    public bool CanUseShortcut(MonoBehaviour mode)
    {
        if (!IsShortcutBlocked())
        {
            return true;
        }

        foreach (IShortcutBlockQuery query in _shortcutQueries)
        {
            if (query != null && query.AllowsShortcut(mode))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsShortcutBlocked()
    {
        if (_guideOverlay != null && _guideOverlay.IsBlockingInput)
        {
            return true;
        }

        foreach (IShortcutBlockQuery query in _shortcutQueries)
        {
            if (query != null && query.BlocksShortcuts())
            {
                return true;
            }
        }

        return false;
    }

    private bool CanCloseByQueries(MonoBehaviour target)
    {
        foreach (IExclusiveModeOpenQuery query in _openQueries)
        {
            if (query != null && !query.CanClose(target))
            {
                return false;
            }
        }

        return true;
    }

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

    private IExclusiveMode _escapeWindow;

    // Esc가 눌린 프레임에 그 Esc를 이미 어떤 창이 가져갔는지 판단할 때 보는 목록.
    // _exclusiveModes 배열만으로는 부족하다 - 연구·용·저장 슬롯 창처럼 배열에 등록되지 않은 창도
    // 자기 Esc 구독으로 스스로 닫히기 때문이다. 그래서 캔버스(이 컴포넌트의 자식) 안의
    // IExclusiveMode를 전부 자동으로 모으고, 캔버스 밖의 모드는 배열에서 가져와 합친다.
    private IExclusiveMode[] _escapeBlockingWindows;

    // 직전 프레임 끝에 창이 하나라도 열려 있었는지. Esc 처리에만 쓴다 - 아래 TryOpenEscapeWindow 참고.
    private bool _wasAnyWindowOpen;

    /// <summary>
    /// 게임 속도(일시정지)의 소유자. 창이 열려 있는 동안 시간을 멈추는 데 쓴다.
    /// GameManager를 통해 얻으므로 창마다 GameSpeedManager를 따로 배선하지 않아도 된다.
    /// </summary>
    public GameSpeedManager GameSpeed => _gameManager != null ? _gameManager.GameSpeedManager : null;

    private void Awake()
    {
        // 게임오버 창은 시작 시 항상 꺼진 상태로 보장한다(씬 체크 상태와 무관).
        Hide(_gameOverWindow);
        Hide(_victoryWindow);

        CacheExclusiveModes();
        CacheExclusiveModeShortcuts();
        CacheEscapeWindow();
        CacheEscapeBlockingWindows();
    }

    private void CacheEscapeBlockingWindows()
    {
        var windows = new List<IExclusiveMode>(GetComponentsInChildren<IExclusiveMode>(true));

        // 캔버스 밖에 있는 모드(점령·인구배치·스킬 타겟팅 등)는 자동 수집에 걸리지 않는다.
        foreach (IExclusiveMode mode in _exclusiveModes)
        {
            if (!windows.Contains(mode))
            {
                windows.Add(mode);
            }
        }

        _escapeBlockingWindows = windows.ToArray();
    }

    // 인스펙터에는 MonoBehaviour로 받고(유니티가 인터페이스 필드를 직렬화하지 못하므로) 여기서 캐스팅한다.
    private void CacheEscapeWindow()
    {
        if (_escapeWindowBehaviour == null)
        {
            return;
        }

        _escapeWindow = _escapeWindowBehaviour as IExclusiveMode;

        if (_escapeWindow == null)
        {
            Debug.LogWarning($"[UIManager] {_escapeWindowBehaviour.name}은 IExclusiveMode를 구현하지 않아 Esc 대상에서 제외됩니다.");
        }
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

            // 튜토리얼의 버튼 유도를 단축키로 건너뛰지 못하게 한다. 실제 HUD 버튼은
            // OpenExclusive를 직접 호출하므로 이 관문과 무관하게 현재 안내대로 작동한다.
            if (!CanUseShortcut(mode as MonoBehaviour))
            {
                continue;
            }

            if (mode.IsOpen)
            {
                // 안내가 이 창 안을 가리키는 중이면 단축키로 닫지 못하게 막는다.
                if (!CanCloseExclusive(mode))
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
        TryOpenEscapeWindow();
    }

    /// <summary>
    /// 아무 창도 열려 있지 않을 때만 Esc가 기본 창(설정)을 연다. 창이 열려 있으면 그 창이 Esc를
    /// 자기 몫으로 쓴다(각 창이 같은 액션을 직접 구독해 스스로 닫힌다).
    /// 직전 프레임 상태(_wasAnyWindowOpen)까지 보는 이유: InputSystem의 performed 콜백은 Update보다
    /// 먼저 돌아서, Esc로 창을 닫은 바로 그 프레임에 여기 오면 이미 "아무것도 안 열림"으로 보인다.
    /// 그 한 프레임을 걸러내지 않으면 Esc로 창을 닫자마자 설정 창이 대신 열린다.
    /// </summary>
    private void TryOpenEscapeWindow()
    {
        if (_escapeWindow == null || _escapeAction == null)
        {
            return;
        }

        if (_escapeAction.action.WasPerformedThisFrame()
            && !IsAnyWindowOpen
            && !_wasAnyWindowOpen
            && CanUseShortcut(_escapeWindowBehaviour))
        {
            OpenExclusive(_escapeWindow);
        }

        _wasAnyWindowOpen = IsAnyWindowOpen;
    }

    private bool IsAnyWindowOpen
    {
        get
        {
            foreach (IExclusiveMode window in _escapeBlockingWindows)
            {
                if (window.IsOpen)
                {
                    return true;
                }
            }

            return false;
        }
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
        if (!CanOpenByQueries(target as MonoBehaviour))
        {
            return;
        }

        // 목록이 아직 만들어지지 않았다(다른 오브젝트의 OnEnable이 Awake보다 먼저 부른 경우).
        // 그 시점에는 열려 있는 창도 없으므로 닫을 것이 없다 - CurrentOpenExclusiveMode와 같은 가드다.
        if (_exclusiveModes == null)
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
        // 거절당하면 열지 않는다. CloseAllExcept 안에도 같은 판정이 있지만 그것만으로는
        // "남을 닫지 않는다"까지만 지켜지고 정작 target은 열려버린다 - 여기서 한 번 더 막아야
        // 안내가 아직 설명하지 않은 창이 실제로 안 열린다.
        if (!CanOpen(target))
        {
            return;
        }

        CloseAllExcept(target);
        target.Open();

        // 열린 뒤에 알린다 - 구독자가 IsOpen을 읽을 수 있어야 한다.
        ExclusiveModeOpened.Invoke(target as MonoBehaviour);
    }

    private bool CanOpen(IExclusiveMode target)
    {
        return CanOpenByQueries(target as MonoBehaviour);
    }

    /// <summary>
    /// ESC처럼 각 모드가 직접 받는 닫기 입력도 UIManager의 튜토리얼 관문을 공유하게 한다.
    /// 질의가 없는 일반 씬에서는 기존처럼 항상 허용한다.
    /// </summary>
    public bool CanCloseExclusive(IExclusiveMode target)
    {
        return target == null || CanCloseByQueries(target as MonoBehaviour);
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.AddListener(HandleGameOver);
            _gameManager.VictoryOccurred.AddListener(HandleVictory);
        }

        // 이 단축키는 다른 컴포넌트와 공유하지 않는 UIManager 전용 액션이라 여기서 직접 켠다
        // (공유 액션이면 GlobalInputBootstrap이 켜야 한다 - _escapeAction이 그 경우다).
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
