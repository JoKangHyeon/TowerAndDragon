using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼 마지막 밤. 보스가 실제로 성까지 걸어와 성을 때리게 두고, 그 전투 도중에 성을 무너뜨린다.
///
/// 시간만 재서 성을 즉사시키면 보스가 화면에 들어오기도 전에 게임이 끝나 "왜 졌는지" 알 수 없다.
/// 그래서 기다리는 기준은 시간이 아니라 <b>성이 실제로 데미지를 받기 시작한 시점</b>이다 -
/// 그 순간이 곧 보스가 도착해 전투가 시작된 순간이고, 화면에도 보인다.
///
/// 패배는 기존 파이프라인을 그대로 탄다(Castle.TakeDamage → Health.Died → GameManager.GameOver →
/// GameOverOccurred). 게임오버 창은 튜토리얼 씬의 UIManager._gameOverWindow를 비워 두어 뜨지 않게 한다 -
/// 그 창의 "재시작" 버튼이 튜토리얼 씬을 잘못 리로드하기 때문이다.
/// </summary>
public sealed class TutorialBossDefeatController : MonoBehaviour
{
    private const float DEFAULT_FINISH_DELAY = 5f;
    private const float DEFAULT_MAX_WAIT = 45f;

    [SerializeField] private TutorialDayLoopController _dayLoopController;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private Castle _castle;

    [Tooltip("마지막 밤에 띄울 보스 웨이브.")]
    [SerializeField] private WaveDefinitionSO _bossWave;

    [Tooltip("1~2일차에 성을 지켜 주는 잠금. 마지막 패배 직전에 이 잠금을 푼다.")]
    [SerializeField] private TutorialCastleGuard _castleGuard;

    [Tooltip("보스가 성을 때리기 시작한 뒤 성이 무너지기까지의 시간(초). 이 동안이 '지는 전투'다.")]
    [Min(0f)]
    [SerializeField] private float _finishDelaySeconds = DEFAULT_FINISH_DELAY;

    [Tooltip("전투가 시작되지 않아도 이 시간(초)이 지나면 패배시킨다. " +
             "보스가 도중에 막히거나 먼저 쓰러졌을 때 엔딩까지 못 가는 것을 막는 안전장치다.")]
    [Min(0f)]
    [SerializeField] private float _maxWaitSeconds = DEFAULT_MAX_WAIT;

    // 마지막 밤은 한 번뿐이지만, 두 번 불려도 성을 두 번 부수지 않는다.
    private bool _hasBegun;

    private void OnEnable()
    {
        if (_dayLoopController == null)
        {
            Debug.LogError("[TutorialBossDefeatController] TutorialDayLoopController 참조가 없습니다.", this);
            return;
        }

        _dayLoopController.FinalNightStarted.AddListener(BeginFinalNight);
    }

    private void OnDisable()
    {
        if (_dayLoopController != null)
        {
            _dayLoopController.FinalNightStarted.RemoveListener(BeginFinalNight);
        }
    }

    /// <summary>마지막 밤 진입. 인스펙터에서 직접 연결할 수도 있게 public으로 둔다.</summary>
    public void BeginFinalNight()
    {
        if (_hasBegun)
        {
            return;
        }

        _hasBegun = true;
        RunAsync().Forget();
    }

    private async UniTaskVoid RunAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        if (_castle == null)
        {
            Debug.LogError("[TutorialBossDefeatController] Castle 참조가 없어 패배 처리를 할 수 없습니다.", this);
            return;
        }

        if (_waveManager != null && _bossWave != null)
        {
            _waveManager.StartWaveAsync(_bossWave).Forget();
        }
        else
        {
            Debug.LogError("[TutorialBossDefeatController] 보스 웨이브를 시작할 수 없습니다.", this);
        }

        // 배속(GameSpeedManager)을 건 플레이어에게는 대기도 함께 빨라져야 웨이브 진행과 어긋나지 않는다.
        // 그래서 스케일된 시간을 쓴다 - 일시정지 중에 패배가 진행되지 않는 것도 의도한 동작이다.
        float startHealth = _castle.CurrentHealth;

        int finishedIndex = await UniTask.WhenAny(
            UniTask.WaitUntil(() => _castle == null || _castle.CurrentHealth < startHealth, cancellationToken: token),
            UniTask.WaitForSeconds(_maxWaitSeconds, cancellationToken: token));

        const int BATTLE_STARTED_INDEX = 0;

        if (finishedIndex == BATTLE_STARTED_INDEX)
        {
            // 보스가 성을 때리는 그림을 잠시 보여준 뒤에 끝낸다.
            await UniTask.WaitForSeconds(_finishDelaySeconds, cancellationToken: token);
        }
        else
        {
            Debug.LogWarning(
                "[TutorialBossDefeatController] 보스가 성에 닿지 않아 대기 시간으로 패배 처리합니다.", this);
        }

        // 남은 스폰을 멈춘다. 이미 나온 보스는 그대로 두어 화면에 남는다.
        _waveManager?.CancelWave();

        if (_castle == null || _castle.IsDead)
        {
            return;
        }

        // 여기까지 성을 지켜 온 잠금을 푼다 - 이 밤의 패배만은 연출대로 일어나야 한다.
        if (_castleGuard != null)
        {
            _castleGuard.AllowDefeat();
        }

        _castle.TakeDamage(new DamageInfo(_castle.MaxHealth));
    }
}
