using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 재생 속도(일시정지/배속)의 유일한 소유자. Time.timeScale을 이 스크립트에서만 바꾼다.
/// - Speed_setting UI(일시정지/재생/배속 버튼)가 이 매니저를 호출한다.
/// - GameManager의 게임오버/승리 이벤트를 받아 시간을 완전히 멈춘다.
/// (싱글톤 아님 - SerializeField 주입 관례. GameManager와 같은 오브젝트에 형제 컴포넌트로 둔다.)
/// </summary>
public class GameSpeedManager : MonoBehaviour
{
    private const float PAUSED_SCALE = 0f;
    private const float NORMAL_SCALE = 1f;

    [Tooltip("배속 버튼을 반복해 누를 때 순환할 배율. 순서대로 적용된다(예: x2 → x3).")]
    [SerializeField] private float[] _fastScales = { 2f, 3f };

    [Tooltip("게임 종료(승리/패배) 후 시간을 완전히 멈추기까지의 지연(초). 파괴 연출을 보여주기 위한 값.")]
    [SerializeField] private float _gameEndFreezeDelay = 0.5f;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("일시정지 토글 단축키 - 보통 Space.")]
    [SerializeField] private InputActionReference _togglePauseAction;

    /// <summary>속도가 바뀔 때마다 발생. UI가 구독해 하이라이트/라벨을 갱신한다.</summary>
    public UnityEvent SpeedChanged = new();

    private bool _isPaused;
    private int _fastIndex = -1;

    // 게임 종료 시 플레이어 조작을 즉시 막는 플래그(UI interactable 판단용)와,
    // 파괴 연출이 끝난 뒤 실제로 시간을 못박는 플래그를 분리한다.
    private bool _isSpeedLocked;
    private bool _isTimeStopped;

    public bool IsPaused => _isPaused;
    public bool IsSpeedLocked => _isSpeedLocked;
    public bool IsFastForward => _fastIndex >= 0;
    public float CurrentScale => IsFastForward ? _fastScales[_fastIndex] : NORMAL_SCALE;

    // 배속 버튼이 꺼져 있을 때(x1) 라벨에 보여줄 "다음에 진입할" 배율. 배속 버튼 라벨은 항상
    // 순환 배열의 첫 배율을 정적으로 보여주는 게 아니라, 현재 진행 중이면 그 배율을, 아니면
    // 첫 배율을 보여준다(UI_SpeedSettingWindow.Render 참고).
    public float FirstFastScale => HasFastScales ? _fastScales[0] : NORMAL_SCALE;

    // 배속 배열이 인스펙터에서 비워지면(씬 구성 실수) 배속 기능 자체를 쓸 수 없다.
    private bool HasFastScales => _fastScales != null && _fastScales.Length > 0;

    private void Awake()
    {
        // 플레이모드 재진입(도메인/씬 리로드 비활성) 시 이전 세션의 timeScale이 남아있을 수 있어 초기화한다.
        Time.timeScale = NORMAL_SCALE;
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.AddListener(HandleGameEnd);
            _gameManager.VictoryOccurred.AddListener(HandleGameEnd);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleDayStart);
        }

        if (_togglePauseAction != null)
        {
            _togglePauseAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.RemoveListener(HandleGameEnd);
            _gameManager.VictoryOccurred.RemoveListener(HandleGameEnd);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleDayStart);
        }

        // timeScale은 씬 로드를 넘어 유지되므로, 재시작(SceneManager.LoadScene) 시 정지 상태가
        // 새 씬으로 새어 들어가지 않도록 여기서 복구한다.
        Time.timeScale = NORMAL_SCALE;
    }

    private void Update()
    {
        if (_togglePauseAction == null || _isSpeedLocked)
        {
            return;
        }

        // 낮에는 Speed_setting UI 자체가 숨겨져 있어 정지 상태를 눈으로 확인할 수 없으므로 밤에만 허용한다.
        if (_cycleManager != null && _cycleManager.CurrentCycle != CycleManager.CycleState.Night)
        {
            return;
        }

        if (_togglePauseAction.action.WasPerformedThisFrame())
        {
            TogglePause();
        }
    }

    /// <summary>일시정지 버튼.</summary>
    public void Pause()
    {
        if (_isSpeedLocked)
        {
            return;
        }

        _isPaused = true;
        Apply();
    }

    /// <summary>재생 버튼 - 일시정지를 풀고 배속도 x1로 되돌린다.</summary>
    public void ResumeNormal()
    {
        if (_isSpeedLocked)
        {
            return;
        }

        _isPaused = false;
        _fastIndex = -1;
        Apply();
    }

    /// <summary>배속 버튼 - 누를 때마다 다음 배율로 순환한다(x2 → x3 → x2 ...).</summary>
    public void CycleFastForward()
    {
        if (_isSpeedLocked)
        {
            return;
        }

        if (!HasFastScales)
        {
            Debug.LogError("[GameSpeedManager] FastScales가 비어 있어 배속을 사용할 수 없습니다.", this);
            return;
        }

        _isPaused = false;
        _fastIndex = (_fastIndex + 1) % _fastScales.Length;
        Apply();
    }

    /// <summary>단축키(Space) 전용 - 일시정지 상태를 토글한다.</summary>
    public void TogglePause()
    {
        if (_isSpeedLocked)
        {
            return;
        }

        if (_isPaused)
        {
            ResumeNormal();
        }
        else
        {
            Pause();
        }
    }

    // 낮이 시작되면 배속/일시정지 상태를 전부 초기화한다. Speed_setting UI가 밤에만 노출되므로,
    // 다음 밤은 항상 x1(일시정지 해제) 상태로 시작해야 한다.
    private void HandleDayStart(int day)
    {
        _isPaused = false;
        _fastIndex = -1;
        Apply();
    }

    private void HandleGameEnd()
    {
        if (_isSpeedLocked)
        {
            return;
        }

        _isSpeedLocked = true;
        _isPaused = false;
        _fastIndex = -1; // 파괴/승리 연출은 x1로 보여준다.
        Apply();

        FreezeAfterDelayAsync().Forget();
    }

    private async UniTaskVoid FreezeAfterDelayAsync()
    {
        await UniTask.Delay(
            TimeSpan.FromSeconds(_gameEndFreezeDelay),
            DelayType.UnscaledDeltaTime,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        _isTimeStopped = true;
        Apply();
    }

    private void Apply()
    {
        Time.timeScale = (_isTimeStopped || _isPaused) ? PAUSED_SCALE : CurrentScale;
        SpeedChanged?.Invoke();
    }
}
