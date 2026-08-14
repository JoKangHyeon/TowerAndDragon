using UnityEngine;

/// <summary>
/// 시계 초침처럼 자기 자신을 계속 회전시킨다. TE_TimeClock 프리팹의 Hand에 붙는다.
/// 원래는 엔딩 컷씬의 연출 프리팹(TutorialEndingBeatSO._effectPrefabs)용이었고,
/// 지금은 로딩 화면(<see cref="SceneLoadOverlay"/>)이 유일한 소비자다.
///
/// DOTween이 아니라 Update에서 직접 적분하는 이유: 씬 로딩 중에는 프레임이 거의 그려지지 않는다
/// (측정: 콜드 3.8초에 16프레임). 시간 기준으로 회전값을 정하면 그려질 때마다 위상이 크게 건너뛰어
/// 회전이 아니라 깜빡임으로 보인다. 프레임당 회전량에 상한을 두면 프레임이 부족할 때 느려지기만 하고
/// 방향은 유지된다.
///
/// Unity가 deltaTime을 Time.maximumDeltaTime(기본 0.333초)으로 클램프하므로 한 프레임의 회전은
/// 그 자체로 이미 제한된다 - 기본 속도(3초/바퀴)에서는 최대 40°다. 상한이 실제로 크게 일하는 것은
/// 가속을 켰을 때(최고 4배 = 최대 160°)이고, 기본 속도에서는 40°를 상한까지 다듬는 정도다.
///
/// timeScale을 무시한다 - 컷씬은 GameSpeedManager가 timeScale을 0으로 굳힌 화면에서 재생되고,
/// 로딩 화면도 정지 중에 뜰 수 있다.
///
/// 회전축은 이 오브젝트의 RectTransform pivot이다. 초침 스프라이트는 회전 중심(축 보석)이 이미지
/// 정중앙이 아니라 아래쪽에 있으므로, pivot을 그 지점으로 옮겨야 시계 밖으로 휘둘리지 않는다.
/// time_clock_second_hand(1254x1254)의 보석 중심은 실측 pivot (0.5, 0.331)이다.
/// 시계판(time_clock_face)은 축이 정중앙이라 pivot을 건드리지 않는다.
/// </summary>
public sealed class UI_RotatingHand : MonoBehaviour
{
    private const float FULL_TURN_DEGREES = 360f;
    private const float MIN_TURN_SECONDS = 0.01f;
    private const float DEFAULT_SECONDS_PER_TURN = 3f;
    private const float DEFAULT_MAX_DEGREES_PER_FRAME = 30f;
    private const float DEFAULT_FINAL_SPEED_MULTIPLIER = 4f;
    private const int DEFAULT_ACCELERATION_TURNS = 5;
    private const float NO_ACCELERATION_MULTIPLIER = 1f;

    [Tooltip("한 바퀴 도는 데 걸리는 시간(초).")]
    [Min(MIN_TURN_SECONDS)]
    [SerializeField] private float _secondsPerTurn = DEFAULT_SECONDS_PER_TURN;

    [Tooltip("시계 반대 방향으로 돈다. 시간이 되돌아가는 연출에 쓴다.")]
    [SerializeField] private bool _isCounterClockwise = true;

    [Tooltip("한 프레임에 돌 수 있는 최대 각도. 씬 로딩처럼 프레임이 드문 구간에서 " +
             "위상이 건너뛰는 것을 막는다. 프레임이 넉넉하면 여기에 걸리지 않는다.")]
    [Min(1f)]
    [SerializeField] private float _maxDegreesPerFrame = DEFAULT_MAX_DEGREES_PER_FRAME;

    [Tooltip("느리게 시작해 점점 빨라진다. 되감기가 가속되는 느낌을 준다. 끄면 일정한 속도로 돈다. " +
             "로딩 화면에서는 끈다 - 가속은 끝이 가까워졌다는 신호로 읽혀 언제 끝날지 모르는 대기와 어긋난다.")]
    [SerializeField] private bool _acceleratesOverTime;

    [Tooltip("가속을 켰을 때 마지막 회전이 첫 회전보다 몇 배 빠른지.")]
    [Min(1f)]
    [SerializeField] private float _finalSpeedMultiplier = DEFAULT_FINAL_SPEED_MULTIPLIER;

    [Tooltip("가속에 쓸 회전 수. 이만큼 돌고 나면 최고 속도로 계속 돈다.")]
    [Min(1)]
    [SerializeField] private int _accelerationTurns = DEFAULT_ACCELERATION_TURNS;

    [Tooltip("이 CanvasGroup의 알파가 0이면 돌지 않는다. 비워 두면 부모에서 자동으로 찾는다.")]
    [WiringOptional]
    [SerializeField] private CanvasGroup _visibilityGroup;

    // 가속 진행도의 기준. 켜져 있지 않으면 쓰이지 않는다.
    private float _accumulatedDegrees;

    // 화면 기준으로 시계 방향이 -Z다.
    private float Direction => _isCounterClockwise ? 1f : -1f;

    private float BaseDegreesPerSecond => FULL_TURN_DEGREES / Mathf.Max(_secondsPerTurn, MIN_TURN_SECONDS);

    private float SpeedMultiplier
    {
        get
        {
            if (!_acceleratesOverTime)
            {
                return NO_ACCELERATION_MULTIPLIER;
            }

            float turns = _accumulatedDegrees / FULL_TURN_DEGREES;
            float progress = Mathf.Clamp01(turns / Mathf.Max(_accelerationTurns, 1));

            return Mathf.Lerp(NO_ACCELERATION_MULTIPLIER, _finalSpeedMultiplier, progress);
        }
    }

    private bool IsVisible => _visibilityGroup == null || _visibilityGroup.alpha > 0f;

    private void Awake()
    {
        // 로딩 오버레이는 SetActive로 여닫지 않고 알파로만 감춘다. 그 알파를 보지 않으면 화면에 없는
        // 시계가 매 프레임 transform을 돌려 보이지 않는 캔버스를 계속 리빌드한다
        // (UI_FrameSteppedSpinner와 같은 이유).
        if (_visibilityGroup == null)
        {
            _visibilityGroup = GetComponentInParent<CanvasGroup>(true);
        }
    }

    private void OnEnable()
    {
        _accumulatedDegrees = 0f;
    }

    private void Update()
    {
        if (!IsVisible)
        {
            return;
        }

        float degrees = Mathf.Min(
            BaseDegreesPerSecond * SpeedMultiplier * Time.unscaledDeltaTime,
            _maxDegreesPerFrame);

        _accumulatedDegrees += degrees;
        transform.Rotate(0f, 0f, degrees * Direction);
    }
}
