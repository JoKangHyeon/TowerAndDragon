using UnityEngine;

/// <summary>
/// 지정한 일차 아침에 연구 점수가 모자라면 채워 준다.
///
/// 연구소 정원은 2명이고 인구 1명당 하루 5점이라, 안내를 따라 1명만 배치하면 다음 날 5점뿐이다.
/// 1티어 노드가 10점을 요구하므로 그대로면 "연구 하나 완료" 목표를 아무리 해도 달성할 수 없다.
/// 인구를 정원까지 채우게 시키는 방법도 있지만, 그때 남은 인구가 모자라면 같은 자리에서 또 막힌다 -
/// 배치 결과와 무관하게 성립하도록 부족분만 메운다.
///
/// **발화 시점이 챕터가 아니라 날짜인 이유:** 예전에는 연구 챕터 오브젝트에 붙어 OnEnable에서 지급했다.
/// 그 챕터가 사라지면 지급도 함께 사라져 목표가 달성 불가능해진다. 그렇다고 상시 오브젝트에서
/// OnEnable로 지급하면 연구를 배우기도 전인 1일차에 점수가 생긴다. 그래서 날짜를 직접 기다린다.
/// </summary>
public sealed class TutorialResearchPointGrant : MonoBehaviour
{
    private const string RESEARCH_POINTS_GRANTED_LOC_KEY = "tutorial_day2_research_points_granted";
    private const int FIRST_DAY_NUMBER = 1;
    private const int DEFAULT_GRANT_DAY_NUMBER = 2;

    [SerializeField] private ResearchManager _researchManager;

    [Tooltip("지급할 날을 기다리는 데 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("이 일차 아침에 지급한다. 연구를 안내하는 날보다 앞서면 안 된다.")]
    [Min(FIRST_DAY_NUMBER)]
    [SerializeField] private int _grantOnDayNumber = DEFAULT_GRANT_DAY_NUMBER;

    [Tooltip("그날 최소한 갖고 있어야 할 연구 점수. 1티어 노드 비용 이상으로 둔다.")]
    [Min(0)]
    [SerializeField] private int _minimumResearchPoints;

    private bool _hasGranted;

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }
    }

    // 배선이 빠지면 조용히 지급되지 않고, 그러면 연구 목표가 영영 달성 불가능해진다 - 시작할 때 알린다.
    private void Start()
    {
        if (_cycleManager == null)
        {
            Debug.LogError("[TutorialResearchPointGrant] CycleManager 참조가 없어 연구 점수를 지급하지 못합니다.", this);
        }

        if (_researchManager == null)
        {
            Debug.LogError("[TutorialResearchPointGrant] ResearchManager 참조가 없습니다.", this);
        }
    }

    private void HandleDayStart(int dayNumber)
    {
        // 그날 아침을 놓쳤어도(뒤늦게 켜졌다면) 이후 아침에 한 번은 지급되도록 >= 로 본다.
        if (_hasGranted || dayNumber < _grantOnDayNumber || _researchManager == null)
        {
            return;
        }

        _hasGranted = true;

        int missing = _minimumResearchPoints - _researchManager.ResearchPoints;
        if (missing <= 0)
        {
            return;
        }

        // 조용히 채운다. 예전에는 토스트로 알렸지만 그 시점이 하필 아침이라, 그때 뜨는 안내 말풍선과
        // 화면에서 겹쳐 둘 다 읽히지 않았다. 포인트가 얼마인지는 연구소를 지었을 때 뜨는
        // 안내 체인이 짚어 주므로 여기서 따로 말할 이유가 없다.
        _researchManager.AddResearchPoints(missing);
        Debug.Log($"[TutorialResearchPointGrant] 연구 포인트 {missing} 지급 (일차 {_grantOnDayNumber})", this);
    }
}
