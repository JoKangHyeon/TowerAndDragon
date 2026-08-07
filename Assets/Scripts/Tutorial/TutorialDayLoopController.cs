using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 튜토리얼 전용 낮/밤 루프. 본게임의 주기 규칙(WaveCycleRules·WaveCycleScheduleSO)을 참조하지 않고
/// 자체 일수 상한만 센다 - 튜토리얼이 본게임 밸런스 데이터에 묶이면 한쪽을 고칠 때마다 다른 쪽이 흔들린다.
///
/// 씬에서는 DailyWaveController를 꺼 두고 이 컴포넌트가 밤 웨이브를 대신 시작한다.
/// 밤을 끝내는 것은 기존 경로 그대로다 - WaveManager.AllMonstersDefeated가 CycleManager.EndNight를 부른다.
/// 그래서 밤에는 반드시 웨이브가 하나 돌아야 한다(웨이브가 없으면 밤이 끝나지 않는다).
/// </summary>
public sealed class TutorialDayLoopController : MonoBehaviour
{
    private const int TUTORIAL_TOTAL_DAYS = 3;

    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private WaveManager _waveManager;

    [Tooltip("마지막 날을 뺀 밤에 실행할 웨이브. 순서대로 1일차, 2일차 밤에 대응한다.")]
    [SerializeField] private List<WaveDefinitionSO> _nightWaves = new();

    [Tooltip("마지막 날 밤에 발행된다. 보스 등장과 강제 패배 연출이 여기에 붙는다.")]
    [SerializeField] private UnityEvent _finalNightStarted = new();

    public UnityEvent FinalNightStarted => _finalNightStarted;

    private void OnEnable()
    {
        if (_cycleManager == null)
        {
            Debug.LogError("[TutorialDayLoopController] CycleManager 참조가 없습니다.", this);
            return;
        }

        _cycleManager.OnNightStart.AddListener(HandleNightStart);
        _cycleManager.OnDayReady.AddListener(HandleDayReady);
    }

    private void OnDisable()
    {
        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        _cycleManager.OnDayReady.RemoveListener(HandleDayReady);
    }

    // 마지막 날 밤에서 엔딩으로 빠지므로 그 다음 날은 정상 흐름에 존재하지 않는다.
    // 도달했다면 엔딩 배선이 끊긴 것이므로, 조용히 4일차를 진행하지 말고 알린다.
    //
    // 낮 시작 4단계 중 OnDayReady에 붙는 이유: 관측만 하는 진단이라 정산 이후 단계가 맞고,
    // OnDayStart는 인라인 초기화가 없어 이 필드가 없는 씬 인스턴스에서 null이 될 수 있다.
    private void HandleDayReady(int dayNumber)
    {
        if (dayNumber > TUTORIAL_TOTAL_DAYS)
        {
            Debug.LogError(
                $"[TutorialDayLoopController] 튜토리얼이 {TUTORIAL_TOTAL_DAYS}일을 넘겨 {dayNumber}일차로 진입했습니다.",
                this);
        }
    }

    private void HandleNightStart(int dayNumber)
    {
        // 마지막 날이거나(정상) 그보다 뒤라면(비정상) 엔딩으로 보낸다 - 어느 쪽이든 웨이브를 또 돌리지 않는다.
        if (dayNumber >= TUTORIAL_TOTAL_DAYS)
        {
            _finalNightStarted.Invoke();
            return;
        }

        StartNightWave(dayNumber);
    }

    private void StartNightWave(int dayNumber)
    {
        int waveIndex = dayNumber - 1;

        WaveDefinitionSO nightWave = waveIndex >= 0 && waveIndex < _nightWaves.Count
            ? _nightWaves[waveIndex]
            : null;

        if (nightWave == null || _waveManager == null)
        {
            Debug.LogError($"[TutorialDayLoopController] {dayNumber}일차 밤 웨이브를 시작할 수 없습니다.", this);
            EndNightNextFrameAsync().Forget();
            return;
        }

        _waveManager.StartWaveAsync(nightWave).Forget();
    }

    /// <summary>
    /// 웨이브를 못 돌린 밤이 영영 끝나지 않는 것을 막는 비상구. 한 프레임 미루는 이유는,
    /// OnNightStart를 발행하는 도중에 EndNight를 부르면 StartNight가 아직 끝나지 않은 상태에서
    /// 낮으로 되돌아가 CurrentCycle이 실제 진행과 어긋나기 때문이다.
    /// </summary>
    private async UniTaskVoid EndNightNextFrameAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        _cycleManager.EndNight();
    }
}
