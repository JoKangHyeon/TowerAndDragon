using UnityEngine;

/// <summary>
/// 1일차 튜토리얼이 끝난 뒤 새끼용 가이드를 시작시킨다. 시작 신호는 알 지급이다
/// (GrantEgg가 모든 획득 경로의 관문이므로 그것만 부르면 토스트·뱃지·안내가 따라온다).
///
/// **지급을 튜토리얼 종료로 미루는 이유는 순서와 경로 단순화 두 가지다.**
/// 미리 주면 새끼용 안내가 빌드 튜토리얼과 화면을 다투고, 내부 타이머(토스트가 끝난 뒤 안내를 띄우는 대기)가
/// 먼저 돌아버려 나중에 보여줄 때 알림과 어긋난다. 그리고 종료 시점 하나에 매달면
/// **완주와 건너뛰기가 같은 경로**가 되어 스킵했을 때만 타이밍이 이상해지는 일이 없다.
///
/// 씬의 DragonEggInventorySystem._grantStartingEgg는 꺼 두어야 한다 - 켜져 있으면 시작 시 알이 나와
/// 위 순서가 깨진다. 대신 알을 못 받는 일이 없도록, 튜토리얼이 없거나 꺼져 있으면 평소처럼 바로 지급한다.
/// </summary>
public sealed class BabyDragonTutorialHandOff : MonoBehaviour
{
    [SerializeField] private TutorialRunner _runner;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("지급할 알의 속성.")]
    [SerializeField] private DragonType _grantedEggType = DragonType.Life;

    private bool _hasGranted;

    private void OnEnable()
    {
        if (_runner != null)
        {
            _runner.TutorialEnded.AddListener(GrantOnce);
        }
    }

    private void OnDisable()
    {
        if (_runner != null)
        {
            _runner.TutorialEnded.RemoveListener(GrantOnce);
        }
    }

    // 튜토리얼 자체가 없거나 꺼져 있으면 종료 신호도 오지 않는다 - 그때는 평소처럼 바로 지급한다.
    private void Start()
    {
        if (_runner == null || !_runner.isActiveAndEnabled)
        {
            GrantOnce();
        }
    }

    /// <summary>
    /// 알은 안내가 아니라 게임 진행에 필요한 물건이므로 어떤 경로로든 한 번은 반드시 지급된다.
    /// </summary>
    private void GrantOnce()
    {
        if (_hasGranted || _eggInventorySystem == null)
        {
            return;
        }

        _hasGranted = true;
        _eggInventorySystem.GrantEgg(_grantedEggType);
    }
}
