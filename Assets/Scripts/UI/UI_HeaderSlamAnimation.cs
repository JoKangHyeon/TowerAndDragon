using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 큰 글자 이미지가 화면 밖에서 확 줄어들며 제자리에 내리꽂히는 등장 연출. 승리 창의 "VICTORY" 글자용이다.
/// 움직일 대상 자신에게 붙인다 - 참조를 따로 배선하지 않고 자기 RectTransform과 Graphic만 쓴다.
///
/// 승리 창의 타임라인은 이 컴포넌트와 <see cref="UI_WingUnfoldAnimation"/>이 나눠 맡는다.
/// 둘 다 창이 열리는 같은 프레임에 OnEnable로 시작하므로, 시작 지연값만으로 순서가 맞는다.
///   0.0s ~ 0.3s  글자가 내리꽂힌다(이 컴포넌트)
///   0.3s         착지 임팩트, 그 순간 날개가 뒤에서 펼쳐지기 시작한다(날개 쪽 _startDelay)
/// 글자가 내려앉는 시간을 바꾸면 날개의 _startDelay도 같이 맞춰야 둘이 한 동작으로 보인다.
///
/// 시간을 unscaled로 도는 이유: 이 창은 GameSpeedManager가 Time.timeScale을 0으로 못박은 뒤에 열린다.
/// SetUpdate(true)를 빼면 트윈이 한 프레임도 진행하지 않는다.
/// </summary>
public sealed class UI_HeaderSlamAnimation : MonoBehaviour
{
    private const float DEFAULT_SLAM_DURATION = 0.3f;
    private const float DEFAULT_FADE_DURATION = 0.15f;
    private const float DEFAULT_START_SCALE = 1.7f;
    private const float DEFAULT_PUNCH_STRENGTH = 0.1f;
    private const float DEFAULT_PUNCH_DURATION = 0.35f;
    private const float DEFAULT_BREATH_SCALE = 0.02f;
    private const float DEFAULT_BREATH_DURATION = 1.4f;

    // 펀치가 흔들리는 횟수와 되돌아오는 탄력. 임팩트 한 번이면 되므로 진동은 적게, 탄력은 낮게 둔다.
    private const int PUNCH_VIBRATO = 6;
    private const float PUNCH_ELASTICITY = 0.6f;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;
    private const int INFINITE_LOOPS = -1;

    [Header("내리꽂기")]
    [Tooltip("등장을 시작하는 배율. 1보다 크면 화면 밖에서 줄어들며 들어오고, 1보다 작으면 솟아오르며 커진다.")]
    [Min(0f)]
    [SerializeField] private float _startScale = DEFAULT_START_SCALE;

    [Tooltip("제자리에 내려앉기까지 걸리는 시간(초). 날개의 _startDelay와 같은 값이어야 착지와 날개 펼침이 겹친다.")]
    [Min(0f)]
    [SerializeField] private float _slamDuration = DEFAULT_SLAM_DURATION;

    [Tooltip("나타나는 시간(초). 내리꽂는 시간보다 짧아야 들어오는 동안 이미 보인다.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    [Tooltip("빠르게 들어와 딱 멈추는 느낌을 내려면 OutQuint/OutExpo 계열을 쓴다.")]
    [SerializeField] private Ease _slamEase = Ease.OutQuint;

    [Header("착지 임팩트")]
    [Tooltip("착지하는 순간 튕기는 크기. 0이면 임팩트 없이 그냥 멈춘다.")]
    [Min(0f)]
    [SerializeField] private float _punchStrength = DEFAULT_PUNCH_STRENGTH;

    [Min(0f)]
    [SerializeField] private float _punchDuration = DEFAULT_PUNCH_DURATION;

    [Header("호흡")]
    [Tooltip("연출이 끝난 뒤 아주 약하게 커졌다 작아질지. 날개 펄럭임과 주기를 맞추면 둘이 한 덩어리로 보인다.")]
    [SerializeField] private bool _playIdleBreath = true;

    [Tooltip("호흡할 때 커지는 비율. 0.02면 2%다.")]
    [Min(0f)]
    [SerializeField] private float _breathScale = DEFAULT_BREATH_SCALE;

    [Tooltip("가장 커질 때까지 걸리는 시간(초). 왕복이므로 한 번 호흡하는 데는 두 배가 걸린다. 날개의 _flutterDuration과 맞춘다.")]
    [Min(0f)]
    [SerializeField] private float _breathDuration = DEFAULT_BREATH_DURATION;

    private RectTransform _rect;
    private Graphic _graphic;
    private Vector3 _homeScale;

    private Sequence _slamSequence;
    private Tween _breathTween;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _graphic = GetComponent<Graphic>();

        // 인스펙터에서 크기를 스케일로 맞춰 놨어도 그 값으로 돌아오도록, 원래 배율을 기억해 둔다.
        _homeScale = _rect.localScale;
    }

    private void OnEnable()
    {
        PlaySlam();
    }

    private void OnDisable()
    {
        // 호흡은 무한 반복이라 창을 닫아도 살아남는다. 여기서 끊지 않으면 다시 열 때 두 겹으로 돈다.
        KillTweens();
    }

    /// <summary>등장 전 상태로 되돌린 뒤 처음부터 다시 재생한다. 창을 다시 열 때마다 같은 그림에서 시작한다.</summary>
    public void PlaySlam()
    {
        KillTweens();
        ApplyStartState();

        _slamSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        _slamSequence.Insert(0f, _rect.DOScale(_homeScale, _slamDuration).SetEase(_slamEase));

        if (_graphic != null)
        {
            _slamSequence.Insert(0f, _graphic.DOFade(VISIBLE_ALPHA, _fadeDuration));
        }

        // 착지하는 순간에만 튕긴다. 날개가 터져 나오는 시각과 같아서 둘이 하나의 충격으로 읽힌다.
        if (_punchStrength > 0f)
        {
            _slamSequence.Insert(_slamDuration, _rect
                .DOPunchScale(_homeScale * _punchStrength, _punchDuration, PUNCH_VIBRATO, PUNCH_ELASTICITY));
        }

        _slamSequence.OnComplete(StartBreath);
    }

    /// <summary>등장 직전 상태. 커진 채로 투명하게 대기한다.</summary>
    private void ApplyStartState()
    {
        _rect.localScale = _homeScale * _startScale;

        if (_graphic == null)
        {
            return;
        }

        Color hidden = _graphic.color;
        hidden.a = HIDDEN_ALPHA;
        _graphic.color = hidden;
    }

    private void StartBreath()
    {
        if (!_playIdleBreath)
        {
            return;
        }

        // 펀치가 스케일을 건드린 직후이므로, 호흡의 출발점을 원래 배율로 확실히 맞춰 두고 시작한다.
        _rect.localScale = _homeScale;

        _breathTween = _rect.DOScale(_homeScale * (1f + _breathScale), _breathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(INFINITE_LOOPS, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void KillTweens()
    {
        _slamSequence?.Kill();
        _slamSequence = null;

        _breathTween?.Kill();
        _breathTween = null;
    }
}
