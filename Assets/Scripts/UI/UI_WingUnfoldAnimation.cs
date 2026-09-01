using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 글자 양옆의 날개 한 쌍이 접혀 있다가 펼쳐지는 연출. 승리 창의 "VICTORY" 날개용으로 만들었지만
/// 좌/우 대칭 이미지 한 쌍이면 어디든 쓸 수 있다.
///
/// 실제로 켜지고 꺼지는 오브젝트(날개들의 부모)에 붙인다. 창이 열릴 때 <see cref="OnEnable"/>로
/// 스스로 재생하므로 바깥에서 호출할 필요가 없다.
///
/// 시간을 unscaled로 도는 이유: 이 창은 게임이 끝난 직후, GameSpeedManager가 Time.timeScale을
/// 0으로 못박아 둔 상태에서 열린다. SetUpdate(true)를 빼면 트윈이 한 프레임도 진행하지 않는다
/// (UIManager가 승리 창 대기에 UnscaledDeltaTime을 쓰는 것과 같은 이유).
/// </summary>
public sealed class UI_WingUnfoldAnimation : MonoBehaviour
{
    // 승리 창에서는 글자가 내려앉는 시간(UI_HeaderSlamAnimation의 _slamDuration)과 같은 값이다.
    private const float DEFAULT_START_DELAY = 0.3f;
    private const float DEFAULT_UNFOLD_DURATION = 0.7f;
    private const float DEFAULT_FADE_DURATION = 0.3f;
    private const float DEFAULT_FOLDED_SCALE_X = 0f;
    private const float DEFAULT_FOLDED_SCALE_Y = 0.3f;
    private const float DEFAULT_FOLDED_ANGLE = 70f;
    private const float DEFAULT_UNFOLD_OVERSHOOT = 2.4f;
    private const float DEFAULT_FLUTTER_ANGLE = 6f;
    private const float DEFAULT_FLUTTER_DURATION = 1.4f;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;
    private const int INFINITE_LOOPS = -1;
    private const float HALF = 0.5f;

    // 펼쳐지는 축은 글자에 붙어 있는 안쪽 끝이다. 왼쪽 날개는 자기 오른쪽 끝, 오른쪽 날개는 자기 왼쪽 끝.
    // 바깥쪽 끝을 축으로 두면 "바깥에서 글자 쪽으로 자라나는" 반대 연출이 된다.
    private static readonly Vector2 LEFT_WING_PIVOT = new(1f, 0.5f);
    private static readonly Vector2 RIGHT_WING_PIVOT = new(0f, 0.5f);

    // 접힌 각도의 부호. 두 날개 모두 끝이 아래를 향한 채 시작해 위로 펼쳐지도록, 좌우가 서로 반대다.
    private const float LEFT_FOLD_SIGN = 1f;
    private const float RIGHT_FOLD_SIGN = -1f;

    [Header("날개")]
    [Tooltip("왼쪽 날개 이미지. 이 컴포넌트가 피벗을 안쪽 끝으로 옮기고 위치를 보정하므로 인스펙터 피벗은 그대로 둬도 된다.")]
    [SerializeField] private RectTransform _leftWing;

    [Tooltip("오른쪽 날개 이미지.")]
    [SerializeField] private RectTransform _rightWing;

    [Header("펼치기")]
    [Tooltip("창이 열린 뒤 날개가 펼쳐지기까지 기다리는 시간(초). " +
             "승리 창에서는 글자가 내려앉는 UI_HeaderSlamAnimation의 _slamDuration과 같아야 착지와 펼침이 한 동작으로 보인다.")]
    [Min(0f)]
    [SerializeField] private float _startDelay = DEFAULT_START_DELAY;

    [Tooltip("날개가 다 펼쳐지는 데 걸리는 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _unfoldDuration = DEFAULT_UNFOLD_DURATION;

    [Tooltip("펼쳐지며 나타나는 시간(초). 펼치기보다 짧아야 끝에서 자연스럽다.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    [Tooltip("튕기며 펼쳐지려면 OutBack 계열을 쓴다.")]
    [SerializeField] private Ease _unfoldEase = Ease.OutBack;

    [Tooltip("펼쳐지며 목표를 지나쳤다 돌아오는 정도. DOTween 기본값은 1.7이고, 키울수록 크게 젖혔다 제자리를 찾는다. " +
             "Back/Elastic 계열 이즈에서만 의미가 있다.")]
    [Min(0f)]
    [SerializeField] private float _unfoldOvershoot = DEFAULT_UNFOLD_OVERSHOOT;

    [Header("접힌 상태")]
    [Tooltip("접혔을 때 가로 배율. 0에 가까울수록 글자 뒤에 완전히 숨었다가 나온다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _foldedScaleX = DEFAULT_FOLDED_SCALE_X;

    [Tooltip("접혔을 때 세로 배율. 1보다 작게 눌러 두면 펴지며 부풀어 오르는 느낌이 난다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _foldedScaleY = DEFAULT_FOLDED_SCALE_Y;

    [Tooltip("접혔을 때 날개 끝이 아래로 기운 각도(도). 펼쳐지면서 0도로 돌아온다.")]
    [SerializeField] private float _foldedAngle = DEFAULT_FOLDED_ANGLE;

    [Header("펄럭임")]
    [Tooltip("다 펼쳐진 뒤 아주 약하게 계속 펄럭일지. 정적인 화면이 싫을 때만 켠다.")]
    [SerializeField] private bool _playIdleFlutter = true;

    [Tooltip("펄럭일 때 좌우로 흔들리는 각도(도). 크게 주면 연출이 아니라 산만해진다.")]
    [SerializeField] private float _flutterAngle = DEFAULT_FLUTTER_ANGLE;

    [Tooltip("한쪽 끝까지 가는 데 걸리는 시간(초). 왕복이므로 한 번 펄럭이는 데는 두 배가 걸린다.")]
    [Min(0f)]
    [SerializeField] private float _flutterDuration = DEFAULT_FLUTTER_DURATION;

    private readonly List<Wing> _wings = new();
    private readonly List<Tween> _flutterTweens = new();

    private Sequence _unfoldSequence;

    private void Awake()
    {
        TryAddWing(_leftWing, LEFT_WING_PIVOT, LEFT_FOLD_SIGN);
        TryAddWing(_rightWing, RIGHT_WING_PIVOT, RIGHT_FOLD_SIGN);
    }

    private void OnEnable()
    {
        PlayUnfold();
    }

    private void OnDisable()
    {
        // 펄럭임은 무한 반복이라 창을 닫아도 살아남는다. 여기서 끊지 않으면 다시 열 때 두 겹으로 돈다.
        KillTweens();
    }

    /// <summary>접힌 상태로 되돌린 뒤 처음부터 다시 펼친다. 창을 다시 열 때마다 같은 그림에서 시작한다.</summary>
    public void PlayUnfold()
    {
        KillTweens();

        if (_wings.Count == 0)
        {
            return;
        }

        _unfoldSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        foreach (Wing wing in _wings)
        {
            ApplyFoldedState(wing);

            _unfoldSequence.Insert(_startDelay,
                wing.Rect.DOScale(wing.HomeScale, _unfoldDuration).SetEase(_unfoldEase, _unfoldOvershoot));

            _unfoldSequence.Insert(_startDelay,
                wing.Rect.DOLocalRotate(Vector3.zero, _unfoldDuration).SetEase(_unfoldEase, _unfoldOvershoot));

            if (wing.Graphic != null)
            {
                _unfoldSequence.Insert(_startDelay, wing.Graphic.DOFade(VISIBLE_ALPHA, _fadeDuration));
            }
        }

        _unfoldSequence.OnComplete(StartFlutter);
    }

    /// <summary>날개를 글자 뒤에 숨은 상태로 눌러 둔다. 펼치기 트윈의 출발점이다.</summary>
    private void ApplyFoldedState(Wing wing)
    {
        wing.Rect.localScale = new Vector3(
            wing.HomeScale.x * _foldedScaleX,
            wing.HomeScale.y * _foldedScaleY,
            wing.HomeScale.z);

        wing.Rect.localRotation = Quaternion.Euler(0f, 0f, _foldedAngle * wing.FoldSign);

        if (wing.Graphic == null)
        {
            return;
        }

        Color folded = wing.Graphic.color;
        folded.a = HIDDEN_ALPHA;
        wing.Graphic.color = folded;
    }

    /// <summary>다 펼쳐진 뒤의 잔잔한 펄럭임. 좌우가 서로 반대로 기울어 대칭으로 움직인다.</summary>
    private void StartFlutter()
    {
        if (!_playIdleFlutter)
        {
            return;
        }

        foreach (Wing wing in _wings)
        {
            Tween flutter = wing.Rect
                .DOLocalRotate(new Vector3(0f, 0f, _flutterAngle * wing.FoldSign), _flutterDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(INFINITE_LOOPS, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);

            _flutterTweens.Add(flutter);
        }
    }

    private void KillTweens()
    {
        _unfoldSequence?.Kill();
        _unfoldSequence = null;

        foreach (Tween flutter in _flutterTweens)
        {
            flutter?.Kill();
        }

        _flutterTweens.Clear();
    }

    private void TryAddWing(RectTransform rect, Vector2 edgePivot, float foldSign)
    {
        if (rect == null)
        {
            return;
        }

        Graphic graphic = rect.GetComponent<Graphic>();

        SetPivotKeepingPosition(rect, ResolveHingePivot(rect, graphic as Image, edgePivot));
        _wings.Add(new Wing(rect, graphic, foldSign));
    }

    /// <summary>
    /// 회전축을 실제 그림의 안쪽 끝에 맞춘다. PreserveAspect가 켜져 있으면 스프라이트가 사각형 안에서
    /// 여백을 두고 중앙 정렬되므로(승리 창의 날개는 100x194 그림이 250x250 사각형에 들어간다),
    /// 사각형 끝을 그대로 축으로 잡으면 그림에서 한참 떨어진 허공을 중심으로 돌게 된다.
    /// </summary>
    private static Vector2 ResolveHingePivot(RectTransform rect, Image image, Vector2 edgePivot)
    {
        if (image == null || image.sprite == null || !image.preserveAspect)
        {
            return edgePivot;
        }

        Vector2 size = rect.rect.size;
        Rect spriteRect = image.sprite.rect;

        if (size.x <= 0f || size.y <= 0f || spriteRect.height <= 0f)
        {
            return edgePivot;
        }

        // 세로가 먼저 꽉 차면 가로에 여백이 남고, 반대면 여백이 없다(Min이 그 둘을 한 번에 고른다).
        float drawnWidth = Mathf.Min(size.x, size.y * (spriteRect.width / spriteRect.height));
        float sidePadding = (size.x - drawnWidth) / size.x * HALF;

        // 바깥쪽 끝(0 또는 1)을 여백만큼 안으로 당긴다. 세로는 어느 쪽이든 중앙이라 그대로 둔다.
        return new Vector2(Mathf.Lerp(sidePadding, 1f - sidePadding, edgePivot.x), edgePivot.y);
    }

    /// <summary>
    /// 화면상의 위치를 유지한 채 피벗만 옮긴다. 피벗을 그냥 바꾸면 사각형이 그 차이만큼 밀려나므로,
    /// 같은 크기만큼 anchoredPosition을 되밀어 준다.
    /// </summary>
    private static void SetPivotKeepingPosition(RectTransform rect, Vector2 pivot)
    {
        if (rect.pivot == pivot)
        {
            return;
        }

        Vector2 delta = pivot - rect.pivot;
        Vector2 size = rect.rect.size;

        rect.pivot = pivot;
        rect.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
    }

    /// <summary>날개 하나가 연출에 필요로 하는 것들. 매번 GetComponent를 돌지 않으려고 Awake에서 한 번 모은다.</summary>
    private sealed class Wing
    {
        public readonly RectTransform Rect;

        /// <summary>알파를 건드릴 대상. 이미지가 아닌 날개(빈 컨테이너 등)도 허용하므로 없을 수 있다.</summary>
        public readonly Graphic Graphic;

        /// <summary>접힌 각도와 펄럭임의 방향. 좌우가 반대 부호라 대칭으로 움직인다.</summary>
        public readonly float FoldSign;

        /// <summary>펼쳐졌을 때의 배율. 인스펙터에서 날개 크기를 스케일로 맞춰 놨어도 그 값으로 돌아온다.</summary>
        public readonly Vector3 HomeScale;

        public Wing(RectTransform rect, Graphic graphic, float foldSign)
        {
            Rect = rect;
            Graphic = graphic;
            FoldSign = foldSign;
            HomeScale = rect.localScale;
        }
    }
}
