using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 위에서 떨어져 바닥에 꽂히는 등장 연출. 패배 창의 땅에 박힌 화살 그림용으로 만들었지만,
/// "떨어져서 쿵 하고 박힌다"가 필요한 요소면 어디든 쓸 수 있다.
/// 움직일 대상 자신에게 붙인다 - 참조를 따로 배선하지 않는다.
///
/// 낙하는 <see cref="Ease.InQuad"/>처럼 **가속하는** 이즈를 쓴다. 감속 이즈(OutQuad 등)는
/// 사뿐히 내려앉아 꽂히는 느낌이 나지 않는다.
/// 착지 순간에는 두 가지가 함께 들어간다 - 아래로 박히는 반동(위치)과 부르르 떠는 진동(회전).
///
/// 패배 창의 타임라인은 여러 컴포넌트가 나눠 맡는다. 전부 창이 열리는 같은 프레임에 OnEnable로
/// 시작하므로, 각자의 시작 지연값만으로 순서가 맞는다.
///   0.05s ~ 0.30s  화살이 떨어져 꽂힌다        (이 컴포넌트)
///   0.00s ~ 0.30s  글자가 내리꽂힌다          (UI_HeaderSlamAnimation)
///   0.45s ~ 0.75s  최종 버틴 날짜가 나타난다   (UI_ButtonRevealAnimation, Result에 부착)
///   0.80s ~        버튼이 나타난다            (UI_ButtonRevealAnimation, 버튼에 부착)
/// 화살의 착지와 글자의 착지가 같은 0.30s에 겹치도록 맞춰져 있다. 한쪽 시간을 바꾸면 다른 쪽도 맞춰야
/// 두 충격이 하나로 읽힌다.
///
/// 시간을 unscaled로 도는 이유: 이 창은 GameSpeedManager가 Time.timeScale을 0으로 못박은 뒤에 열린다.
/// SetUpdate(true)를 빼면 트윈이 한 프레임도 진행하지 않는다.
/// </summary>
public sealed class UI_ArrowImpactAnimation : MonoBehaviour
{
    private const float DEFAULT_START_DELAY = 0.05f;
    private const float DEFAULT_DROP_DISTANCE = 320f;
    private const float DEFAULT_DROP_DURATION = 0.25f;
    private const float DEFAULT_FADE_DURATION = 0.12f;
    private const float DEFAULT_SINK_DISTANCE = 14f;
    private const float DEFAULT_SINK_DURATION = 0.35f;
    private const float DEFAULT_WOBBLE_ANGLE = 4f;
    private const float DEFAULT_WOBBLE_DURATION = 0.6f;

    // 흔들리는 횟수와 제자리로 돌아오는 탄력. 꽂힌 뒤 깔끔히 멈춰야 하므로 탄력은 낮게 둔다.
    private const int PUNCH_VIBRATO = 8;
    private const float PUNCH_ELASTICITY = 0.4f;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;

    [Header("낙하")]
    [Tooltip("창이 열린 뒤 떨어지기 시작할 때까지 기다리는 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _startDelay = DEFAULT_START_DELAY;

    [Tooltip("얼마나 위에서 떨어질지(px). 현재 위치 기준 상대값이라 대상을 옮겨도 그대로 쓸 수 있다.")]
    [SerializeField] private float _dropDistance = DEFAULT_DROP_DISTANCE;

    [Tooltip("떨어지는 데 걸리는 시간(초). _startDelay와 더한 값이 착지 시각이다.")]
    [Min(0f)]
    [SerializeField] private float _dropDuration = DEFAULT_DROP_DURATION;

    [Tooltip("중력처럼 가속해야 꽂히는 느낌이 난다. 감속 이즈를 쓰면 사뿐히 내려앉는다.")]
    [SerializeField] private Ease _dropEase = Ease.InQuad;

    [Tooltip("나타나는 시간(초). 떨어지는 시간보다 짧아야 떨어지는 동안 이미 보인다.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    [Header("착지 충격")]
    [Tooltip("착지 순간 아래로 박히는 깊이(px). 0이면 반동 없이 그냥 멈춘다.")]
    [Min(0f)]
    [SerializeField] private float _sinkDistance = DEFAULT_SINK_DISTANCE;

    [Min(0f)]
    [SerializeField] private float _sinkDuration = DEFAULT_SINK_DURATION;

    [Tooltip("꽂힌 뒤 부르르 떠는 각도(도). 화살이 박혀 흔들리는 느낌을 낸다. 크게 주면 우스워진다.")]
    [Min(0f)]
    [SerializeField] private float _wobbleAngle = DEFAULT_WOBBLE_ANGLE;

    [Tooltip("떨림이 잦아드는 데 걸리는 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _wobbleDuration = DEFAULT_WOBBLE_DURATION;

    private RectTransform _rect;
    private Graphic _graphic;
    private Vector2 _homePosition;

    private Sequence _sequence;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _graphic = GetComponent<Graphic>();
        _homePosition = _rect.anchoredPosition;
    }

    private void OnEnable()
    {
        PlayImpact();
    }

    private void OnDisable()
    {
        // 창을 닫는 순간 트윈이 남아 있으면 다시 열 때 두 겹으로 돈다.
        KillTweens();
    }

    /// <summary>떨어지기 전 상태로 되돌린 뒤 처음부터 다시 재생한다. 창을 다시 열 때마다 같은 그림에서 시작한다.</summary>
    public void PlayImpact()
    {
        KillTweens();
        ApplyStartState();

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        _sequence.Insert(_startDelay, _rect
            .DOAnchorPosY(_homePosition.y, _dropDuration)
            .SetEase(_dropEase));

        if (_graphic != null)
        {
            _sequence.Insert(_startDelay, _graphic.DOFade(VISIBLE_ALPHA, _fadeDuration));
        }

        float impactTime = _startDelay + _dropDuration;

        // 아래로 한 번 박혔다가 제자리로. 착지의 무게를 만드는 것은 이쪽이다.
        if (_sinkDistance > 0f)
        {
            _sequence.Insert(impactTime, _rect
                .DOPunchAnchorPos(new Vector2(0f, -_sinkDistance), _sinkDuration, PUNCH_VIBRATO, PUNCH_ELASTICITY));
        }

        // 박힌 화살이 떠는 느낌. 위치 반동보다 길게 끌어야 여운이 남는다.
        if (_wobbleAngle > 0f)
        {
            _sequence.Insert(impactTime, _rect
                .DOPunchRotation(new Vector3(0f, 0f, _wobbleAngle), _wobbleDuration, PUNCH_VIBRATO, PUNCH_ELASTICITY));
        }
    }

    /// <summary>떨어지기 직전 상태. 제자리보다 위에, 투명하게 대기한다.</summary>
    private void ApplyStartState()
    {
        _rect.anchoredPosition = new Vector2(_homePosition.x, _homePosition.y + _dropDistance);

        // 앞선 재생이 남긴 기울기를 지운다. 남아 있으면 기울어진 채로 떨어진다.
        _rect.localRotation = Quaternion.identity;

        if (_graphic == null)
        {
            return;
        }

        Color hidden = _graphic.color;
        hidden.a = HIDDEN_ALPHA;
        _graphic.color = hidden;
    }

    private void KillTweens()
    {
        _sequence?.Kill();
        _sequence = null;
    }
}
