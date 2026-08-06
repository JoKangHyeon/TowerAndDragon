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
/// 마지막 밤의 연출된 패배만 예외다 - TutorialBossDefeatController가 AllowDefeat로 잠금을 푼다.
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

    /// <summary>마지막 밤의 연출된 패배 직전에 호출한다. 한 번 풀면 되돌리지 않는다.</summary>
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

        _castle.DamageBlockQuery = this;
        _castle.HealthChanged.AddListener(HandleHealthChanged);
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
        if (_isDefeatAllowed || _waveManager == null || !_waveManager.IsRunning || max <= 0f)
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
