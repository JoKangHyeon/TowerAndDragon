using DG.Tweening;
using UnityEngine;

/// <summary>
/// 원형 마스크 안에서 낮/밤 아이콘을 원호를 따라 굴려 교체한다(Panel_TopCenter/Symbol_Day).
///
/// 회전 중심은 마스크 중앙 바로 아래에 잡는다. 그래서 각도가 <see cref="REST_ANGLE_DEG"/>(위쪽)일 때
/// 아이콘이 정확히 마스크 중앙에 오고, 거기서 시계방향으로 돌리면 오른쪽 아래로 빠져나가며
/// 반대편 아이콘이 왼쪽 아래에서 올라온다.
///
/// 대기 중인 아이콘을 끄지 않고 마스크 밖에 세워 두는 이유: 꺼 두면 스윕 도중에 나타날 수 없어
/// 결국 "뿅" 하고 바뀌는 것과 같아진다. 마스크(UI.Mask)가 밖을 잘라 주므로 켜 둬도 보이지 않는다.
///
/// 트윈이 각도 하나만 굴리고 두 아이콘의 위치를 그 각도에서 계산하는 이유: 아이콘마다 트윈을 따로
/// 두면 이징이 미세하게 어긋나 한쪽이 먼저 도착하는 순간이 생긴다.
/// </summary>
public sealed class UI_CycleSymbolSwapper : MonoBehaviour
{
    // 정지 상태의 각도. 회전 중심이 마스크 바로 아래에 있으므로 이 각도에서 마스크 정중앙이 된다.
    private const float REST_ANGLE_DEG = 90f;

    // 대기 아이콘이 완전히 가려지려면 마스크 반지름(25) + 아이콘 반지름(21) = 46px보다 멀어야 한다.
    // 여유를 둔 값이라 아이콘이나 마스크 크기를 조금 손봐도 새어 나오지 않는다.
    private const float DEFAULT_SWAP_DISTANCE = 58f;
    private const float DEFAULT_SWEEP_ANGLE_DEG = 90f;
    private const float DEFAULT_DURATION = 0.45f;

    private const float HALF = 0.5f;

    [Tooltip("낮에 마스크 중앙으로 들어올 아이콘(Image_Day).")]
    [SerializeField] private RectTransform _dayIcon;

    [Tooltip("밤에 마스크 중앙으로 들어올 아이콘(Image_Light).")]
    [SerializeField] private RectTransform _nightIcon;

    [Header("궤적")]
    [Tooltip("교체 중인 두 아이콘 사이 거리(px). 대기 중인 아이콘은 마스크 중앙에서 이만큼 떨어진 곳에 서 있으므로, " +
        "마스크 반지름 + 아이콘 반지름보다 커야 대기 아이콘이 완전히 가려진다.")]
    [SerializeField] private float _swapDistance = DEFAULT_SWAP_DISTANCE;

    [Tooltip("교체 한 번에 도는 각도(도). 작을수록 직선에 가깝게 스쳐 지나가고, 클수록 크게 돌아 나간다. " +
        "아이콘 사이 거리는 이 값과 무관하게 유지된다.")]
    [SerializeField] private float _sweepAngleDeg = DEFAULT_SWEEP_ANGLE_DEG;

    [Tooltip("체크하면 시계방향으로 돈다. 해제하면 반시계방향.")]
    [SerializeField] private bool _isClockwise = true;

    [Header("트윈")]
    [Tooltip("교체에 걸리는 시간(초). 게임 속도·일시정지와 무관한 UI 연출이라 unscaled로 흐른다.")]
    [SerializeField] private float _duration = DEFAULT_DURATION;

    [SerializeField] private Ease _ease = Ease.InOutCubic;

    private Tween _tween;
    private bool _isDay;

    // 첫 적용인지. 창을 열 때의 첫 호출(UI_IngameWindow.OnEnable)은 연출 없이 제자리에 놓아야 한다 -
    // 이어하기나 씬 진입마다 아이콘이 굴러 들어오면 상태 변화가 아닌데 변화처럼 보인다.
    private bool _hasApplied;

    // 시계방향은 각도가 줄어드는 쪽이다(Unity의 각도는 반시계가 +).
    private float SweepStep => _isClockwise ? -_sweepAngleDeg : _sweepAngleDeg;

    // 두 아이콘 사이 거리는 궤도원의 현(chord)이다 - 원하는 거리와 각도에서 반경을 역산한다.
    // 덕분에 각도를 바꿔 궤적의 휘어짐만 조절해도 "대기 아이콘이 가려지는 거리"는 그대로 유지된다.
    private float OrbitRadius
    {
        get
        {
            float halfSweepSin = Mathf.Sin(_sweepAngleDeg * HALF * Mathf.Deg2Rad);

            // 각도가 0이면 궤도가 성립하지 않는다(반경이 무한). 이때는 굴리지 않고 제자리에 둔다.
            return Mathf.Approximately(halfSweepSin, 0f) ? 0f : _swapDistance * HALF / halfSweepSin;
        }
    }

    /// <summary>낮이면 낮 아이콘을, 밤이면 밤 아이콘을 마스크 중앙으로 굴려 온다.</summary>
    public void Apply(bool isDay)
    {
        if (!WiringGuard.Require(_dayIcon, nameof(_dayIcon), this) ||
            !WiringGuard.Require(_nightIcon, nameof(_nightIcon), this))
        {
            return;
        }

        // 같은 상태로 다시 들어오면(창 재활성화 등) 굴릴 이유가 없다.
        if (_hasApplied && _isDay == isDay)
        {
            return;
        }

        bool shouldAnimate = _hasApplied;

        _isDay = isDay;
        _hasApplied = true;

        // 대기 아이콘은 꺼서 감추는 게 아니라 마스크 밖으로 밀어 감춘다.
        _dayIcon.gameObject.SetActive(true);
        _nightIcon.gameObject.SetActive(true);

        Play(shouldAnimate);
    }

    private void Play(bool shouldAnimate)
    {
        // 앞선 교체가 남아 있으면 끝까지 진행시켜 제자리에 놓고 시작한다. 중간에서 죽이면
        // 나가던 아이콘이 어중간한 위치에 멈춘 채로 다음 스윕의 출발점이 된다.
        CompleteRunningTween();

        RectTransform incoming = _isDay ? _dayIcon : _nightIcon;
        RectTransform outgoing = _isDay ? _nightIcon : _dayIcon;

        float radius = OrbitRadius;
        float exitAngle = REST_ANGLE_DEG + SweepStep;
        float enterAngle = REST_ANGLE_DEG - SweepStep;

        if (!shouldAnimate)
        {
            incoming.anchoredPosition = PositionAt(REST_ANGLE_DEG, radius);
            outgoing.anchoredPosition = PositionAt(exitAngle, radius);
            return;
        }

        incoming.anchoredPosition = PositionAt(enterAngle, radius);

        _tween = DOVirtual.Float(0f, 1f, _duration, progress =>
            {
                incoming.anchoredPosition = PositionAt(Mathf.Lerp(enterAngle, REST_ANGLE_DEG, progress), radius);
                outgoing.anchoredPosition = PositionAt(Mathf.Lerp(REST_ANGLE_DEG, exitAngle, progress), radius);
            })
            .SetEase(_ease)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    /// <summary>회전 중심(마스크 중앙에서 radius만큼 아래) 기준의 각도를 마스크 중앙 기준 좌표로 옮긴다.</summary>
    private static Vector2 PositionAt(float angleDeg, float radius)
    {
        float radians = angleDeg * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius - radius);
    }

    // 창이 닫히는 도중 트윈이 남으면 다음에 열 때 아이콘이 어중간한 위치에서 시작한다.
    private void OnDisable()
    {
        CompleteRunningTween();
    }

    private void CompleteRunningTween()
    {
        if (_tween != null && _tween.IsActive())
        {
            _tween.Complete();
        }

        _tween = null;
    }
}
