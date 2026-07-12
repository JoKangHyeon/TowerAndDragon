using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 메인 성 체력바 View.
/// - 본 바(_bar): 현재 체력을 즉시 반영(띡 감소).
/// - 트레일 Fill(_trailFill): 본 Fill 뒤에서 옛 체력부터 Lerp로 천천히 따라 내려가 "줄어든 구간"을 보여준다.
/// - 현재/최대 체력 텍스트(_amountText) 갱신.
/// - 줄어든 구간을 네모 조각으로 복제해 위로 살짝 떠오르며 사라지는 연출.
/// </summary>
public class UI_CastleHealth : MonoBehaviour
{
    private const string CHUNK_OBJECT_NAME = "HealthChunk";
    private const string CURRENT_AMOUNT_FORMAT = "{0}/{1}";
    private const float DEFAULT_CHUNK_LIFETIME = 0.5f;
    private const float DEFAULT_RISE_DISTANCE = 30f;
    private const float DEFAULT_TRAIL_LERP_SPEED = 6f;
    private const float TRAIL_SNAP_THRESHOLD = 0.001f;
    private const float DEFAULT_SHAKE_DURATION = 0.2f;
    private const float DEFAULT_SHAKE_STRENGTH = 8f;
    private const int DEFAULT_SHAKE_VIBRATO = 10;

    [SerializeField] private Castle _castle;
    [Tooltip("현재 체력을 즉시 반영하는 앞쪽 바.")]
    [SerializeField] private Slider _bar;
    [Tooltip("본 Fill 뒤에서 Lerp로 천천히 따라 내려가는 잔상 Fill (Filled 이미지).")]
    [SerializeField] private Image _trailFill;
    [Tooltip("트레일이 목표까지 따라가는 속도. 클수록 빠르게 수렴한다.")]
    [SerializeField] private float _trailLerpSpeed = DEFAULT_TRAIL_LERP_SPEED;
    [Tooltip("현재 체력/최대 체력을 표시하는 텍스트.")]
    [SerializeField] private TMP_Text _amountText;

    [Header("떠오르는 조각 연출")]
    [Tooltip("조각이 생성·상승할 부모이자 폭 기준. 보통 Slider의 FillArea를 지정한다.")]
    [SerializeField] private RectTransform _chunkParent;
    [Tooltip("조각의 색·스프라이트를 복제할 원본. 보통 Fill 이미지를 지정한다.")]
    [SerializeField] private Image _fillGraphic;
    [SerializeField] private float _chunkLifetime = DEFAULT_CHUNK_LIFETIME;
    [Tooltip("조각이 사라질 때까지 위로 떠오르는 거리(px).")]
    [SerializeField] private float _chunkRiseDistance = DEFAULT_RISE_DISTANCE;

    [Header("피격 흔들림 연출")]
    [Tooltip("체력이 깎일 때 흔들 대상. 비워두면 이 오브젝트 자신을 흔든다. 보통 체력바 루트를 지정한다.")]
    [SerializeField] private RectTransform _shakeTarget;
    [Tooltip("흔들림 지속 시간(초).")]
    [SerializeField] private float _shakeDuration = DEFAULT_SHAKE_DURATION;
    [Tooltip("흔들림 세기(px). 클수록 크게 흔들린다.")]
    [SerializeField] private float _shakeStrength = DEFAULT_SHAKE_STRENGTH;
    [Tooltip("흔들림 진동 횟수. 클수록 촘촘하게 떨린다.")]
    [SerializeField] private int _shakeVibrato = DEFAULT_SHAKE_VIBRATO;

    private float _lastRatio = 1f;
    private float _targetRatio = 1f;

    private Vector2 _shakeRestPosition;
    private Tween _shakeTween;

    private void OnEnable()
    {
        _castle.HealthChanged += Render;
        // 흔들 대상이 지정되지 않으면 이 오브젝트 자신을 흔든다.
        if (_shakeTarget == null)
        {
            _shakeTarget = transform as RectTransform;
        }
        if (_shakeTarget != null)
        {
            _shakeRestPosition = _shakeTarget.anchoredPosition;
        }
        // 창을 다시 켰을 때는 두 바 모두 애니메이션 없이 현재값으로 즉시 맞춘다.
        _lastRatio = SafeRatio(_castle.CurrentHealth, _castle.MaxHealth);
        _targetRatio = _lastRatio;
        _bar.value = _lastRatio;
        if (_trailFill != null)
        {
            _trailFill.fillAmount = _lastRatio;
        }
        UpdateAmountText(_castle.CurrentHealth, _castle.MaxHealth);
    }

    private void OnDisable()
    {
        _castle.HealthChanged -= Render;
        // 진행 중인 흔들림을 정리하고 원위치로 되돌린다.
        _shakeTween?.Kill();
        _shakeTween = null;
        if (_shakeTarget != null)
        {
            _shakeTarget.anchoredPosition = _shakeRestPosition;
        }
    }

    private void Update()
    {
        if (_trailFill == null)
        {
            return;
        }

        float value = _trailFill.fillAmount;
        if (Mathf.Abs(value - _targetRatio) <= TRAIL_SNAP_THRESHOLD)
        {
            if (value != _targetRatio)
            {
                _trailFill.fillAmount = _targetRatio;
            }
            return;
        }

        // 트레일만 부드럽게 따라간다(본 바는 Render에서 이미 즉시 반영됨).
        _trailFill.fillAmount = Mathf.Lerp(value, _targetRatio, _trailLerpSpeed * Time.deltaTime);
    }

    private void Render(float current, float max)
    {
        float ratio = SafeRatio(current, max);
        if (ratio < _lastRatio)
        {
            SpawnChunk(ratio, _lastRatio);
            Shake();
        }
        else if (_trailFill != null)
        {
            // 증가·초기화(회복, 첫 Initialize 등)에는 트레일을 즉시 맞춘다.
            // 안 그러면 트레일이 옛 값에서 위로 Lerp되며 그 사이 첫 감소를 삼켜버린다.
            _trailFill.fillAmount = ratio;
        }

        _bar.value = ratio;      // 본 바: 즉시
        _targetRatio = ratio;    // 트레일: 감소 시에만 Update에서 Lerp로 수렴
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

    /// <summary>피격 시 흔들 대상을 원위치 기준으로 살짝 흔든다. 연속 피격 시 위치가 밀리지 않도록 매번 원위치로 리셋 후 시작한다.</summary>
    private void Shake()
    {
        if (_shakeTarget == null)
        {
            return;
        }

        _shakeTween?.Kill();
        _shakeTarget.anchoredPosition = _shakeRestPosition;
        _shakeTween = _shakeTarget
            .DOShakeAnchorPos(_shakeDuration, _shakeStrength, _shakeVibrato)
            .SetLink(_shakeTarget.gameObject)
            .OnComplete(() => _shakeTarget.anchoredPosition = _shakeRestPosition);
    }

    /// <summary>fromRatio~toRatio 구간(=줄어든 체력)을 조각으로 복제해 떠오르게 한다. toRatio가 더 큰 값.</summary>
    private void SpawnChunk(float fromRatio, float toRatio)
    {
        if (_chunkParent == null || _fillGraphic == null)
        {
            return;
        }

        float width = _chunkParent.rect.width;
        float height = _chunkParent.rect.height;
        float lostWidth = (toRatio - fromRatio) * width;
        if (lostWidth <= 0)
        {
            return;
        }

        GameObject go = new GameObject(CHUNK_OBJECT_NAME, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(_chunkParent, false);
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(lostWidth, height);
        rect.anchoredPosition = new Vector2(fromRatio * width, 0);

        Image image = go.GetComponent<Image>();
        image.sprite = _fillGraphic.sprite;
        image.color = _fillGraphic.color;
        image.type = _fillGraphic.type;
        image.material = _fillGraphic.material;
        image.raycastTarget = false;

        HealthChunkEffect chunk = go.AddComponent<HealthChunkEffect>();
        chunk.Play(_chunkLifetime, _chunkRiseDistance);
    }
}
