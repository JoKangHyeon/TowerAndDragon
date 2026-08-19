using UnityEngine;

/// <summary>
/// 1일차 타워 준비가 끝난 뒤 새끼용 가이드를 시작시킨다. 시작 신호는 알 지급이다
/// (GrantEgg가 모든 획득 경로의 관문이므로 그것만 부르면 토스트·뱃지·안내가 따라온다).
///
/// 실제 지급 시점은 지정한 타워 준비 단계의 완료 직후다. 그 다음 단계에서 1일차 러너가 표시권을
/// 새끼용 가이드에 넘기므로, 알 확인을 마친 뒤에야 밤 준비 안내가 이어진다.
/// 건너뛰기로 해당 단계를 지나지 않은 경우에는 튜토리얼 종료 신호가 안전망으로 한 번 지급한다.
///
/// 씬의 DragonEggInventorySystem._grantStartingEgg는 꺼 두어야 한다 - 켜져 있으면 시작 시 알이 나와
/// 위 순서가 깨진다. 대신 알을 못 받는 일이 없도록, 튜토리얼이 없거나 꺼져 있으면 평소처럼 바로 지급한다.
/// </summary>
public sealed class BabyDragonTutorialHandOff : MonoBehaviour
{
    [SerializeField] private TutorialRunner _runner;
    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;

    [Tooltip("이 단계를 완료하면 알을 지급한다. 1일차에서는 타워 3기 완전 배치 단계다.")]
    [SerializeField] private TutorialStepSO _grantAfterStep;

    [Tooltip("지급할 알의 속성.")]
    [SerializeField] private DragonType _grantedEggType = DragonType.Life;

    [Tooltip("이미 며칠 자란 상태로 줄지. 부화까지의 날수(BabyDragonData.DaysToHatch)에서 이만큼 앞당겨진다 - " +
             "튜토리얼처럼 다음 날 아침 부화를 보여줘야 할 때 쓴다.")]
    [Min(0)]
    [SerializeField] private int _initialFedDayCount;

    private bool _hasGranted;

    private void OnEnable()
    {
        if (_runner != null)
        {
            _runner.TutorialStepCompleted.AddListener(HandleTutorialStepCompleted);
            _runner.TutorialEnded.AddListener(GrantOnce);
        }
    }

    private void OnDisable()
    {
        if (_runner != null)
        {
            _runner.TutorialStepCompleted.RemoveListener(HandleTutorialStepCompleted);
            _runner.TutorialEnded.RemoveListener(GrantOnce);
        }
    }

    // 튜토리얼 자체가 없거나 꺼져 있으면 종료 신호도 오지 않는다 - 그때는 평소처럼 바로 지급한다.
    //
    // isActiveAndEnabled로 보면 안 된다. 튜토리얼이 여러 챕터로 나뉘면 뒤 챕터는 제 차례가 올 때까지
    // 오브젝트가 꺼진 채 기다리는데, 그것을 "튜토리얼이 없다"로 읽어 시작하자마자 알을 줘 버린다
    // (실제로 1일차를 두 챕터로 나눈 순간 그렇게 됐다). 기다리는 중인지 정말 꺼진 것인지는
    // 컴포넌트의 enabled로 가른다 - 차례를 기다리는 챕터는 컴포넌트가 켜져 있다.
    private void Start()
    {
        if (_runner == null || !_runner.enabled)
        {
            GrantOnce();
        }
    }

    /// <summary>
    /// 알은 안내가 아니라 게임 진행에 필요한 물건이므로 어떤 경로로든 한 번은 반드시 지급된다.
    /// </summary>
    private void HandleTutorialStepCompleted(TutorialStepSO completedStep)
    {
        if (completedStep == _grantAfterStep)
        {
            GrantOnce();
        }
    }

    private void GrantOnce()
    {
        if (_hasGranted || _eggInventorySystem == null)
        {
            return;
        }

        _hasGranted = true;
        _eggInventorySystem.GrantEgg(_grantedEggType, _initialFedDayCount);
    }
}
