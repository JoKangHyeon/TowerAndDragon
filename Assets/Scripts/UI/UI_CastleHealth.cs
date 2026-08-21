using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 메인 성 체력바 View.
/// - 본 바(_bar): 현재 체력을 즉시 반영(띡 감소).
/// - 트레일 Fill(_trailFill): 본 Fill 뒤에서 옛 체력부터 DOTween으로 천천히 따라 내려가 "줄어든 구간"을 보여준다.
/// - 현재/최대 체력 텍스트(_amountText) 갱신.
/// - 피격 시 fill을 위아래로 짧게 흔드는 연출.
/// </summary>
public class UI_CastleHealth : MonoBehaviour
{
    private const string CURRENT_AMOUNT_FORMAT = "{0}/{1}";

    [SerializeField] private Castle _castle;
    [Tooltip("현재 체력을 즉시 반영하는 앞쪽 바.")]
    [SerializeField] private Slider _bar;
    [Tooltip("본 Fill 뒤에서 천천히 따라 내려가는 잔상 Fill (Filled 이미지).")]
    [SerializeField] private Image _trailFill;
    [Tooltip("트레일이 새 체력까지 따라 내려가는 시간(초).")]
    [SerializeField] private float _trailDuration = 0.4f;
    [Tooltip("현재 체력/최대 체력을 표시하는 텍스트. 비워두면 숫자 없이 바만 보여준다 - " +
        "성 위에 떠 있는 월드 체력바처럼 텍스트 자식이 없는 바가 그렇다.")]
    [WiringOptional]
    [SerializeField] private TMP_Text _amountText;

    [Header("피격 흔들림 연출")]
    [Tooltip("체력이 깎일 때 흔들 대상. 비워두면 이 오브젝트 자신을 흔든다. 보통 fill(FillArea)을 지정한다.")]
    [WiringOptional]
    [SerializeField] private RectTransform _shakeTarget;
    [Tooltip("흔들림 지속 시간(초).")]
    [SerializeField] private float _shakeDuration = 0.15f;
    [Tooltip("위아래 흔들림 세기(px). 클수록 크게 흔들린다.")]
    [SerializeField] private float _shakeStrength = 8f;
    [Tooltip("흔들림 진동 횟수. 클수록 촘촘하게 떨린다.")]
    [SerializeField] private int _shakeVibrato = 20;
    [Tooltip("흔들림 방향 랜덤성. 0이면 순수 위아래, 클수록 좌우로도 퍼진다.")]
    [SerializeField] private float _shakeRandomness = 0f;

    private float _lastRatio = 1f;

    private Vector2 _shakeRestPosition;
    private Tween _shakeTween;
    private Tween _trailTween;

    private void OnEnable()
    {
        // Castle이 연결되지 않은 씬(UI 전용 씬 등)에서는 체력 연동을 건너뛴다.
        if (_castle == null)
        {
            return;
        }
        _castle.HealthChanged.AddListener(Render);
        // 흔들 대상이 지정되지 않으면 이 오브젝트 자신을 흔든다.
        if (_shakeTarget == null)
        {
            _shakeTarget = transform as RectTransform;
        }
        if (_shakeTarget != null)
        {
            _shakeRestPosition = _shakeTarget.anchoredPosition;
        }
        // 창을 다시 켰을 때는 애니메이션 없이 현재값으로 즉시 맞춘다.
        _lastRatio = SafeRatio(_castle.CurrentHealth, _castle.MaxHealth);
        _bar.value = _lastRatio;
        if (_trailFill != null)
        {
            _trailTween?.Kill();
            _trailFill.fillAmount = _lastRatio;
        }
        UpdateAmountText(_castle.CurrentHealth, _castle.MaxHealth);
    }

    private void OnDisable()
    {
        // OnEnable에서 Castle 미연결로 조기 반환한 경우, 구독·트윈이 없으므로 정리할 것도 없다.
        if (_castle == null)
        {
            return;
        }
        _castle.HealthChanged.RemoveListener(Render);
        // 진행 중인 트윈을 정리하고 흔들림은 원위치로 되돌린다.
        _trailTween?.Kill();
        _trailTween = null;
        _shakeTween?.Kill();
        _shakeTween = null;
        if (_shakeTarget != null)
        {
            _shakeTarget.anchoredPosition = _shakeRestPosition;
        }
    }

    private void Render(float current, float max)
    {
        float ratio = SafeRatio(current, max);
        _bar.value = ratio;      // 본 바: 즉시 반영

        if (ratio < _lastRatio)
        {
            // 감소: 트레일을 옛 값에서 새 값까지 DOTween으로 부드럽게 따라내림 + 피격 흔들림.
            if (_trailFill != null)
            {
                _trailTween?.Kill();
                _trailTween = _trailFill.DOFillAmount(ratio, _trailDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(_trailFill.gameObject);
            }
            Shake();
        }
        else if (_trailFill != null)
        {
            // 증가·초기화(회복, 첫 Initialize 등)에는 트레일을 즉시 맞춘다.
            _trailTween?.Kill();
            _trailFill.fillAmount = ratio;
        }

        _lastRatio = ratio;
        UpdateAmountText(current, max);
    }

    private void UpdateAmountText(float current, float max)
    {
        if (_amountText == null)
        {
            return;
        }
        _amountText.text = string.Format(CURRENT_AMOUNT_FORMAT, Mathf.RoundToInt(current), Mathf.RoundToInt(max));
    }

    private static float SafeRatio(float current, float max)
    {
        return max > 0 ? current / max : 0;
    }

    /// <summary>피격 시 흔들 대상을 원위치 기준으로 위아래로 살짝 흔든다. 연속 피격 시 위치가 밀리지 않도록 매번 원위치로 리셋 후 시작한다.</summary>
    private void Shake()
    {
        if (_shakeTarget == null)
        {
            return;
        }

        _shakeTween?.Kill();
        _shakeTarget.anchoredPosition = _shakeRestPosition;
        // 위아래(Y축)로만 흔든다. x 세기 0 + randomness 0 → 순수 수직 흔들림.
        _shakeTween = _shakeTarget
            .DOShakeAnchorPos(_shakeDuration, new Vector2(0f, _shakeStrength), _shakeVibrato, _shakeRandomness)
            .SetLink(_shakeTarget.gameObject)
            .OnComplete(() => _shakeTarget.anchoredPosition = _shakeRestPosition);
    }
}
