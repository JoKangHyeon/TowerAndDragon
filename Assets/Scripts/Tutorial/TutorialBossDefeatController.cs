using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼 마지막 밤. 보스 웨이브를 띄우는 것이 전부다.
///
/// <b>성 보호는 이 밤에도 풀지 않는다</b>(팀 결정, 2026-08-14). 튜토리얼의 결말은 패배가 아니라
/// "세 밤을 지켜냈다"이므로, 보스를 정리하면 <see cref="TutorialDayLoopController"/>가 클리어를 건다.
///
/// 예전에는 이길 수 없는 웨이브를 띄우고 <c>AllowDefeat</c>로 잠금을 풀어 성이 무너지게 했다.
/// 그 구조는 통째로 걷어냈다 - 게임오버 창이 없는 이 씬에서 패배는 곧 완전 정지였고,
/// "안내를 다 따라왔는데 지는" 결말이 튜토리얼의 목적과 어긋났다.
/// </summary>
public sealed class TutorialBossDefeatController : MonoBehaviour
{
    [SerializeField] private TutorialDayLoopController _dayLoopController;
    [SerializeField] private WaveManager _waveManager;

    [Tooltip("마지막 밤에 띄울 보스 웨이브. 안내대로 따라온 플레이어가 막아낼 수 있는 구성이어야 한다 - " +
             "보스를 정리하는 것이 튜토리얼의 클리어 조건이다.")]
    [SerializeField] private WaveDefinitionSO _bossWave;

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

    }

    /// <summary>마지막 밤 진입. 인스펙터에서 직접 연결할 수도 있게 public으로 둔다.</summary>
    public void BeginFinalNight()
    {
        if (_hasBegun)
        {
            return;
        }

        _hasBegun = true;

        if (_waveManager == null || _bossWave == null)
        {
            Debug.LogError("[TutorialBossDefeatController] 보스 웨이브를 시작할 수 없습니다.", this);
            return;
        }

        // 보스를 정리하면 기존 경로대로 밤이 끝나고(WaveManager.AllMonstersDefeated → CycleManager.EndNight),
        // 그 밤이 마지막이었다는 판단과 클리어는 TutorialDayLoopController가 맡는다.
        _waveManager.StartWaveAsync(_bossWave).Forget();
    }
}
