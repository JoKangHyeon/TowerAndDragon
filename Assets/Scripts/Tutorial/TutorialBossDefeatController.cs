using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼 마지막 밤. 보스가 실제로 성까지 걸어와 성을 때려 부수게 둔다.
///
/// <b>성을 죽이는 것은 이 컴포넌트가 아니라 보스다.</b> 여기서 하는 일은 성을 지켜 오던 잠금
/// (<see cref="TutorialCastleGuard"/>)을 <b>보스가 성 앞에 닿은 순간에</b> 풀어 주는 것뿐이다.
/// 그래야 플레이어가 보는 것이 "갑자기 성이 사라졌다"가 아니라 "막아 봤지만 뚫렸다"가 된다.
///
/// 웨이브가 시작하자마자 풀면 안 된다 - 보스는 느리게 걸어오는데 앞서 온 잡몹이 먼저 닿아
/// 성을 부숴 버린다. 정작 싸워야 할 상대를 만나지도 못하고 끝난다.
///
/// 플레이어가 보스를 잡아 버리면 웨이브 전멸로 밤이 정상 종료되어 4일차로 넘어가는데,
/// 튜토리얼에는 4일차가 없다. 그래서 이 밤에는 전멸 신호를 패배로 받는다 - 이겼더라도 진다.
/// 데이터로 보스를 불사로 만들지 않는 이유는, 그러면 "이기지 못하게 막았다"가 밸런스 수치에 숨어
/// 보스 데이터를 손볼 때마다 이 보장이 조용히 깨지기 때문이다.
///
/// 남은 즉사 처리는 안전장치다 - 보스가 길에 막혀 성이 끝내 무너지지 않으면 엔딩까지 가지 못한다.
/// 그 경우에만 경고를 남기고 강제로 끝낸다.
///
/// 패배는 기존 파이프라인을 그대로 탄다(Castle 파괴 → GameManager.GameOver → GameOverOccurred).
/// 게임오버 창은 튜토리얼 씬의 UIManager._gameOverWindow를 비워 두어 뜨지 않게 한다 -
/// 그 창의 "재시작" 버튼이 튜토리얼 씬을 잘못 리로드하기 때문이다.
/// </summary>
public sealed class TutorialBossDefeatController : MonoBehaviour
{
    private const float DEFAULT_MAX_WAIT = 90f;
    private const float DEFAULT_FINISH_GRACE = 6f;
    private const float DEFAULT_BOSS_ARRIVAL_DISTANCE = 2f;

    [SerializeField] private TutorialDayLoopController _dayLoopController;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private Castle _castle;

    [Tooltip("마지막 밤에 띄울 보스 웨이브.")]
    [SerializeField] private WaveDefinitionSO _bossWave;

    [Tooltip("1~2일차에 성을 지켜 주는 잠금. 마지막 패배 직전에 이 잠금을 푼다.")]
    [SerializeField] private TutorialCastleGuard _castleGuard;

    [Tooltip("보스가 성에 닿기를 기다리는 시간(초). 이 시간이 지나면 길에 막힌 것으로 보고 강제로 끝낸다 - " +
             "엔딩까지 가지 못하는 것을 막는 안전장치다. 보스가 느릴수록 넉넉해야 한다.")]
    [Min(0f)]
    [SerializeField] private float _maxWaitSeconds = DEFAULT_MAX_WAIT;

    [Tooltip("보스가 성에 닿은 뒤 제 손으로 무너뜨리기를 기다리는 시간(초). 이 시간이 지나면 대신 끝낸다.")]
    [Min(0f)]
    [SerializeField] private float _finishGraceSeconds = DEFAULT_FINISH_GRACE;

    [Tooltip("보스가 '성에 닿았다'고 볼 거리. 보스의 공격 사거리보다 조금 넉넉하게 둔다.")]
    [Min(0f)]
    [SerializeField] private float _bossArrivalDistance = DEFAULT_BOSS_ARRIVAL_DISTANCE;

    // 마지막 밤은 한 번뿐이지만, 두 번 불려도 성을 두 번 부수지 않는다.
    private bool _hasBegun;

    // 플레이어가 보스를 잡아 웨이브가 비었다. 이 밤에는 승리가 아니라 패배로 이어져야 한다.
    private bool _hasClearedWave;

    // 이번 웨이브에서 가장 단단한 개체. 성에 닿는 순간이 곧 마지막 전투의 시작이다.
    private BaseMonster _boss;

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

        if (_waveManager != null)
        {
            _waveManager.AllMonstersDefeated.RemoveListener(HandleAllMonstersDefeated);
            _waveManager.MonsterSpawned.RemoveListener(HandleMonsterSpawned);
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

        // 이 밤에만 듣는다 - 1~2일차 밤은 전멸로 정상 종료돼야 한다.
        if (_waveManager != null)
        {
            _waveManager.AllMonstersDefeated.AddListener(HandleAllMonstersDefeated);
            _waveManager.MonsterSpawned.AddListener(HandleMonsterSpawned);
        }

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

        // 성은 계속 지키되 구제만 멈춘다. 잡몹이 보스보다 먼저 닿아 성을 위태롭게 만드는데,
        // 그때 구제가 웨이브를 치워 버리면 보스가 도착하기도 전에 밤이 끝난다.
        if (_castleGuard != null)
        {
            _castleGuard.SuspendRescue();
        }
        else
        {
            Debug.LogWarning(
                "[TutorialBossDefeatController] TutorialCastleGuard 참조가 없습니다 - " +
                "1~2일차 성 보호가 걸려 있었다면 마지막 밤에도 풀리지 않습니다.", this);
        }

        if (_waveManager != null && _bossWave != null)
        {
            _waveManager.StartWaveAsync(_bossWave).Forget();
        }
        else
        {
            Debug.LogError("[TutorialBossDefeatController] 보스 웨이브를 시작할 수 없습니다.", this);
        }

        // 보스가 성 앞에 닿을 때까지 기다린다. 그전까지 성은 잠금이 지켜 주므로,
        // 앞서 온 잡몹이 아무리 때려도 무너지지 않는다 - 마지막 한 방은 보스의 몫이다.
        // 배속(GameSpeedManager)을 건 플레이어에게는 대기도 함께 빨라져야 웨이브 진행과 어긋나지 않으므로
        // 스케일된 시간을 쓴다 - 일시정지 중에 패배가 진행되지 않는 것도 의도한 동작이다.
        int reachedIndex = await UniTask.WhenAny(
            UniTask.WaitUntil(IsBossAtCastle, cancellationToken: token),
            UniTask.WaitUntil(() => _hasClearedWave, cancellationToken: token),
            UniTask.WaitForSeconds(_maxWaitSeconds, cancellationToken: token));

        const int BOSS_ARRIVED_INDEX = 0;
        const int WAVE_CLEARED_INDEX = 1;

        // 여기서 잠금을 완전히 푼다. 이 순간부터 성은 다음 한 방에 무너진다.
        _castleGuard?.AllowDefeat();

        if (reachedIndex == BOSS_ARRIVED_INDEX)
        {
            // 보스가 마무리하게 둔다. 그 사이 아무 일도 없으면 아래 강제 처리로 내려간다.
            await UniTask.WhenAny(
                UniTask.WaitUntil(() => _castle == null || _castle.IsDead, cancellationToken: token),
                UniTask.WaitForSeconds(_finishGraceSeconds, cancellationToken: token));
        }
        else if (reachedIndex == WAVE_CLEARED_INDEX)
        {
            Debug.Log("[TutorialBossDefeatController] 보스를 잡았지만 마지막 밤은 패배로 끝납니다.", this);
        }
        else
        {
            Debug.LogWarning(
                $"[TutorialBossDefeatController] {_maxWaitSeconds}초 안에 보스가 성에 닿지 않아 " +
                "강제로 패배 처리합니다.", this);
        }

        if (_castle == null || _castle.IsDead)
        {
            // 보스가 제 손으로 무너뜨렸다 - 기존 패배 파이프라인이 이미 돌고 있다.
            return;
        }

        _waveManager?.CancelWave();
        _castle.TakeDamage(new DamageInfo(_castle.MaxHealth));
    }

    // 보스가 성을 때릴 수 있는 거리까지 왔는지. "누가 보스인가"는 웨이브 데이터에 따로 표시가 없어
    // 이번 웨이브에서 가장 단단한 개체로 본다 - 보스를 바꿔도 배선을 다시 할 필요가 없다.
    private bool IsBossAtCastle()
    {
        if (_boss == null || _castle == null)
        {
            return false;
        }

        return Vector3.Distance(_boss.transform.position, _castle.transform.position) <= _bossArrivalDistance;
    }

    private void HandleMonsterSpawned(BaseMonster monster)
    {
        if (monster == null || monster.Data == null)
        {
            return;
        }

        if (_boss == null || monster.Data.MaxHealth > _boss.Data.MaxHealth)
        {
            _boss = monster;
        }
    }

    // 플레이어가 보스를 잡았다. 이대로 두면 밤이 정상 종료되어 4일차로 넘어가므로 패배로 받는다.
    private void HandleAllMonstersDefeated()
    {
        _hasClearedWave = true;
    }
}
