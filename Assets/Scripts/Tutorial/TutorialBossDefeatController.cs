using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼 마지막 밤. 성 보호를 완전히 풀고 보스 웨이브를 띄운 뒤, 결과는 실제 전투에 맡긴다.
///
/// <b>여기서 성을 죽이지 않는다.</b> 하는 일은 1~2일차를 지켜 오던 잠금
/// (<see cref="TutorialCastleGuard"/>)을 이 밤에 한해 푸는 것과 보스 웨이브를 시작하는 것뿐이다.
///
/// 예전에는 보스가 성 앞에 닿기를 기다렸다가 시간이 지나면 성을 강제로 부쉈다. 그 장치는 없앴다 -
/// <b>보스 웨이브 데이터 자체가 플레이어가 이길 수 없게 설계돼 있어</b> 연출을 위해 결과를 조작할
/// 이유가 없다. 강제 패배는 "막아 봤지만 뚫렸다"를 "갑자기 성이 사라졌다"로 만들 위험만 남긴다.
///
/// 그래서 잡몹이 보스보다 먼저 성을 무너뜨리는 것도 막지 않는다. 그것도 이 밤의 정직한 결과다.
///
/// 패배는 기존 파이프라인을 그대로 탄다(Castle 파괴 → GameManager.GameOver → GameOverOccurred).
/// 게임오버 창은 튜토리얼 씬의 UIManager._gameOverWindow를 비워 두어 뜨지 않게 한다 -
/// 그 창의 "재시작" 버튼이 튜토리얼 씬을 잘못 리로드하기 때문이다.
/// </summary>
public sealed class TutorialBossDefeatController : MonoBehaviour
{
    [SerializeField] private TutorialDayLoopController _dayLoopController;
    [SerializeField] private WaveManager _waveManager;

    [Tooltip("마지막 밤에 띄울 보스 웨이브. 플레이어가 이길 수 없는 구성이어야 한다 - " +
             "이길 수 있으면 밤이 정상 종료되어 튜토리얼에 없는 4일차로 넘어간다.")]
    [SerializeField] private WaveDefinitionSO _bossWave;

    [Tooltip("1~2일차에 성을 지켜 주는 잠금. 이 밤에 푼다.")]
    [SerializeField] private TutorialCastleGuard _castleGuard;

    // 마지막 밤은 한 번뿐이지만, 두 번 불려도 웨이브를 두 번 띄우지 않는다.
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

        if (_waveManager != null)
        {
            _waveManager.AllMonstersDefeated.RemoveListener(HandleAllMonstersDefeated);
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

        // 이 밤부터 성은 평범하게 죽는다 - 치명타 거절도, 위태로울 때의 웨이브 정리도 멈춘다.
        if (_castleGuard != null)
        {
            _castleGuard.AllowDefeat();
        }
        else
        {
            Debug.LogWarning(
                "[TutorialBossDefeatController] TutorialCastleGuard 참조가 없습니다 - " +
                "1~2일차 성 보호가 걸려 있었다면 마지막 밤에도 풀리지 않습니다.", this);
        }

        if (_waveManager == null || _bossWave == null)
        {
            Debug.LogError("[TutorialBossDefeatController] 보스 웨이브를 시작할 수 없습니다.", this);
            return;
        }

        // 이 밤에만 듣는다 - 1~2일차 밤은 전멸로 정상 종료돼야 한다.
        _waveManager.AllMonstersDefeated.AddListener(HandleAllMonstersDefeated);
        _waveManager.StartWaveAsync(_bossWave).Forget();
    }

    // 이길 수 없어야 할 웨이브를 플레이어가 정리했다. 결과를 뒤집어 지게 만들지는 않는다 -
    // 그건 실제 전투가 아니다. 다만 이대로면 튜토리얼에 없는 4일차로 넘어가므로,
    // 전제가 깨졌다는 것은 남긴다(보스 웨이브 데이터를 손볼 때 조용히 깨지는 지점이다).
    private void HandleAllMonstersDefeated()
    {
        Debug.LogError(
            "[TutorialBossDefeatController] 마지막 밤 보스 웨이브를 플레이어가 막아냈습니다 - " +
            "이 웨이브는 이길 수 없어야 합니다. 이대로면 튜토리얼에 없는 4일차로 넘어갑니다.", this);
    }
}
