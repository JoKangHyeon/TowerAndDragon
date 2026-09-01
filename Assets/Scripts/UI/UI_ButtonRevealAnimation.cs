using DG.Tweening;
using UnityEngine;

/// <summary>
/// 버튼이 제자리에서 살짝 커지며 나타나는 등장 연출. 움직일 버튼 자신에게 붙인다.
/// 자리를 옮기지 않으므로 레이아웃이 잡아 준 위치를 그대로 쓰고, 배율과 알파만 건드린다.
///
/// 알파를 Image가 아니라 CanvasGroup으로 조절하는 이유: 이 버튼들은 ColorTint 트랜지션이라
/// Button이 상태가 바뀔 때마다 targetGraphic의 색을 직접 덮어쓴다. Image에 페이드를 걸면 둘이 싸운다.
/// 아직 보이지 않는 동안 눌리지 않도록 blocksRaycasts를 꺼 두고, 다 나타나면 되돌린다.
///
/// 승리 창의 타임라인은 세 컴포넌트가 나눠 맡는다. 셋 다 창이 열리는 같은 프레임에 OnEnable로
/// 시작하므로, 각자의 시작 지연값만으로 순서가 맞는다.
///   0.00s ~ 0.30s  글자가 내리꽂힌다      (UI_HeaderSlamAnimation)
///   0.30s ~ 1.00s  날개가 펼쳐진다        (UI_WingUnfoldAnimation)
///   0.95s ~        버튼이 제자리에서 나타난다 (이 컴포넌트)
/// 앞 박자의 길이를 바꾸면 뒤 박자의 시작 지연도 같이 밀어야 한다.
///
/// 시간을 unscaled로 도는 이유: 이 창은 GameSpeedManager가 Time.timeScale을 0으로 못박은 뒤에 열린다.
/// SetUpdate(true)를 빼면 트윈이 한 프레임도 진행하지 않는다.
/// </summary>
public sealed class UI_ButtonRevealAnimation : MonoBehaviour
{
    // 날개가 다 펼쳐지기 직전에 시작한다. 완전히 끝난 뒤에 시작하면 화면이 한 박자 비어 보인다.
    private const float DEFAULT_START_DELAY = 0.95f;
    private const float DEFAULT_REVEAL_DURATION = 0.4f;
    private const float DEFAULT_FADE_DURATION = 0.3f;

    // 자리를 옮기지 않으니 배율 변화가 등장의 전부다. 이동으로 벌던 눈에 띔을 여기서 벌어야 한다.
    private const float DEFAULT_START_SCALE = 0.8f;
    private const float DEFAULT_REVEAL_OVERSHOOT = 1.7f;
    private const float DEFAULT_PULSE_SCALE = 0.03f;
    private const float DEFAULT_PULSE_DURATION = 1.4f;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;
    private const int INFINITE_LOOPS = -1;

    [Header("나타나기")]
    [Tooltip("창이 열린 뒤 버튼이 나타나기까지 기다리는 시간(초). 앞 박자(날개)가 끝나갈 무렵으로 맞춘다.")]
    [Min(0f)]
    [SerializeField] private float _startDelay = DEFAULT_START_DELAY;

    [Tooltip("제 크기를 찾는 데 걸리는 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _revealDuration = DEFAULT_REVEAL_DURATION;

    [Tooltip("나타나는 시간(초). 커지는 시간보다 짧아야 커지는 동안 이미 보인다.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    [Tooltip("등장을 시작하는 배율. 1보다 작으면 작았다가 제 크기를 찾고, 1보다 크면 줄어들며 내려앉는다.")]
    [Min(0f)]
    [SerializeField] private float _startScale = DEFAULT_START_SCALE;

    [Tooltip("살짝 튕기며 자리를 잡으려면 OutBack 계열을 쓴다.")]
    [SerializeField] private Ease _revealEase = Ease.OutBack;

    [Tooltip("목표를 지나쳤다 돌아오는 정도. Back/Elastic 계열 이즈에서만 의미가 있다.")]
    [Min(0f)]
    [SerializeField] private float _revealOvershoot = DEFAULT_REVEAL_OVERSHOOT;

    [Header("맥박")]
    [Tooltip("등장이 끝난 뒤 아주 약하게 커졌다 작아질지. 눌러야 할 곳을 눈에 띄게 한다.")]
    [SerializeField] private bool _playIdlePulse = true;

    [Tooltip("맥박이 커지는 비율. 0.03이면 3%다.")]
    [Min(0f)]
    [SerializeField] private float _pulseScale = DEFAULT_PULSE_SCALE;

    [Tooltip("가장 커질 때까지 걸리는 시간(초). 왕복이므로 한 번 뛰는 데는 두 배가 걸린다. " +
             "글자의 _breathDuration, 날개의 _flutterDuration과 맞추면 화면 전체가 같은 박자로 움직인다.")]
    [Min(0f)]
    [SerializeField] private float _pulseDuration = DEFAULT_PULSE_DURATION;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Vector3 _homeScale;

    private Sequence _revealSequence;
    private Tween _pulseTween;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // 프리팹에 CanvasGroup을 따로 붙여 두지 않아도 되도록 여기서 확보한다.
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _homeScale = _rect.localScale;
    }

    private void OnEnable()
    {
        PlayReveal();
    }

    private void OnDisable()
    {
        // 맥박은 무한 반복이라 창을 닫아도 살아남는다. 여기서 끊지 않으면 다시 열 때 두 겹으로 돈다.
        KillTweens();
    }

    /// <summary>등장 전 상태로 되돌린 뒤 처음부터 다시 재생한다. 창을 다시 열 때마다 같은 그림에서 시작한다.</summary>
    public void PlayReveal()
    {
        KillTweens();
        ApplyStartState();

        _revealSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        _revealSequence.Insert(_startDelay, _rect
            .DOScale(_homeScale, _revealDuration)
            .SetEase(_revealEase, _revealOvershoot));

        _revealSequence.Insert(_startDelay, _canvasGroup.DOFade(VISIBLE_ALPHA, _fadeDuration));

        _revealSequence.OnComplete(HandleRevealed);
    }

    /// <summary>등장 직전 상태. 제자리에 작게, 투명하게, 눌리지 않는 채로 대기한다.</summary>
    private void ApplyStartState()
    {
        _rect.localScale = _homeScale * _startScale;

        _canvasGroup.alpha = HIDDEN_ALPHA;

        // 보이지도 않는 버튼이 눌리지 않게 막는다. 등장이 끝나면 되돌린다.
        _canvasGroup.blocksRaycasts = false;
    }

    private void HandleRevealed()
    {
        _canvasGroup.blocksRaycasts = true;
        StartPulse();
    }

    private void StartPulse()
    {
        if (!_playIdlePulse)
        {
            return;
        }

        // 등장 트윈이 방금 배율을 건드렸으므로, 맥박의 출발점을 원래 배율로 확실히 맞춰 두고 시작한다.
        _rect.localScale = _homeScale;

        _pulseTween = _rect.DOScale(_homeScale * (1f + _pulseScale), _pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(INFINITE_LOOPS, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void KillTweens()
    {
        _revealSequence?.Kill();
        _revealSequence = null;

        _pulseTween?.Kill();
        _pulseTween = null;
    }
}
