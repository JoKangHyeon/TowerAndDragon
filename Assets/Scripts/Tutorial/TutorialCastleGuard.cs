using UnityEngine;

/// <summary>
/// 튜토리얼이 끝나기 전에 성이 무너지는 것을 막는다.
///
/// 1~2일차 밤에 성이 파괴되면 게임오버가 나고 엔딩 컷씬이 재생돼, 안내를 절반도 보지 못한 채
/// 본게임으로 넘어가 버린다. 타워를 몬스터가 오는 길목에 짓도록 유도하더라도 플레이어가
/// 다르게 지으면 그만이므로 결과에 기대지 않는다.
///
/// 다만 피해를 통째로 막으면 안 된다 - 몬스터는 성을 계속 때리는데 성은 줄지 않고,
/// 사거리 안에 타워가 없으면 몬스터가 죽지 않아 밤이 영영 끝나지 않는다(밤을 끝내는 경로는
/// WaveManager의 전멸 이벤트뿐이다). 그래서 두 가지를 나눠 처리한다.
///   1. 성이 위태로워지면 남은 웨이브를 정리해 그 밤을 끝낸다 - 교착을 푸는 쪽.
///   2. 그래도 들어온 치명타는 거절한다 - 한 방에 무너지는 경우까지 막는 마지막 방어선.
///
/// 마지막 밤만 예외다 - TutorialBossDefeatController가 AllowDefeat로 잠금을 풀고, 그때부터는
/// 보호도 구제도 없이 실제 전투로 결판이 난다.
/// </summary>
public sealed class TutorialCastleGuard : MonoBehaviour, ICastleDamageBlockQuery
{
    private const float DEFAULT_RESCUE_HEALTH_RATIO = 0.25f;

    [SerializeField] private Castle _castle;

    [Tooltip("남은 웨이브를 정리해 밤을 끝낼 성 체력 비율. 이 아래로 떨어지면 몬스터가 물러난다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _rescueHealthRatio = DEFAULT_RESCUE_HEALTH_RATIO;

    [Tooltip("구제할 때 남은 몬스터를 정리한다. 비우면 밤이 끝나지 않아 교착에 빠질 수 있다.")]
    [SerializeField] private WaveManager _waveManager;

    private bool _isDefeatAllowed;

    /// <summary>
    /// 마지막 밤에 호출한다. 치명타 거절과 구제를 한꺼번에 풀어 성이 평범하게 죽게 만든다.
    /// 한 번 풀면 되돌리지 않는다.
    /// </summary>
    public void AllowDefeat()
    {
        _isDefeatAllowed = true;
    }

    bool ICastleDamageBlockQuery.CanTakeDamage(float amount)
    {
        if (_isDefeatAllowed || _castle == null)
        {
            return true;
        }

        // 이 한 방에 무너진다면 거절한다. 평범한 피해는 그대로 받아 긴장이 살아 있어야 한다.
        return amount < _castle.CurrentHealth;
    }

    private void OnEnable()
    {
        if (_castle == null)
        {
            Debug.LogError("[TutorialCastleGuard] Castle 참조가 없습니다.", this);
            return;
        }

        // 구독을 먼저 걸고 차단은 그 뒤에 건다 - 순서가 반대면 구독이 실패했을 때
        // 치명타 거절만 살아남아 성은 죽지 않고 구제도 오지 않는 교착이 된다.
        _castle.HealthChanged.AddListener(HandleHealthChanged);
        _castle.DamageBlockQuery = this;
    }

    private void OnDisable()
    {
        if (_castle == null)
        {
            return;
        }

        _castle.HealthChanged.RemoveListener(HandleHealthChanged);

        // 남이 걸어둔 것을 지우지 않도록 내가 건 경우에만 뗀다(TutorialRunner.ReleaseOpenQuery와 같은 관례).
        if (ReferenceEquals(_castle.DamageBlockQuery, this))
        {
            _castle.DamageBlockQuery = null;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (_isDefeatAllowed || max <= 0f)
        {
            return;
        }

        // _waveManager가 비면 구제가 통째로 사라지는데, 치명타 거절은 그대로 살아 있다 -
        // 성은 죽지 않고 몬스터도 정리되지 않아 밤이 영영 끝나지 않는다. 조용히 넘기면 안 되는 조합이다.
        if (!WiringGuard.Require(_waveManager, nameof(_waveManager), this) || !_waveManager.IsRunning)
        {
            return;
        }

        if (current / max > _rescueHealthRatio)
        {
            return;
        }

        // 남은 몬스터를 정리해 밤을 끝낸다. 성은 살아남고 다음 날 안내가 이어진다.
        Debug.Log("[TutorialCastleGuard] 성이 위태로워 남은 웨이브를 정리하고 밤을 끝냅니다.", this);
        _waveManager.ForceCompleteWave();
    }
}
