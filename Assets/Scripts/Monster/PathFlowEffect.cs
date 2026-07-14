using UnityEngine;

/// <summary>
/// LineRenderer의 색상 그라디언트를 시간에 따라 움직여, 포탈(t=0) -> 성(t=1) 방향으로
/// 빛 뭉치가 흐르는 듯한 이펙트를 낸다. 적이 포탈에서 성으로 진격하는 방향을 시각적으로 암시한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PathFlowEffect : MonoBehaviour
{
    private const int GRADIENT_KEY_COUNT = 5;

    [Tooltip("포탈에서 성까지 빛 뭉치가 한 번 이동하는 데 걸리는 시간(초)")]
    [SerializeField] private float _travelDuration = 1f;

    [Tooltip("빛 뭉치의 폭 (0~1, 라인 전체 길이 대비 비율)")]
    [SerializeField] private float _pulseWidth = 0.15f;

    [SerializeField] private Color _baseColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color _pulseColor = Color.white;

    private LineRenderer _lineRenderer;
    private readonly GradientColorKey[] _colorKeys = new GradientColorKey[GRADIENT_KEY_COUNT];
    private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[GRADIENT_KEY_COUNT];
    private Gradient _gradient;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _gradient = new Gradient();
    }

    private void Update()
    {
        // -_pulseWidth ~ 1+_pulseWidth 구간을 반복해, 빛 뭉치가 라인 양 끝에서 자연스럽게 들어오고 빠져나가게 한다.
        float cycleLength = 1f + _pulseWidth * 2f;
        float pulseCenter = Mathf.Repeat(Time.time / _travelDuration, cycleLength) - _pulseWidth;

        ApplyPulseGradient(pulseCenter);
    }

    private void ApplyPulseGradient(float pulseCenter)
    {
        float beforeTime = Mathf.Clamp01(pulseCenter - _pulseWidth);
        float peakTime = Mathf.Clamp01(pulseCenter);
        float afterTime = Mathf.Clamp01(pulseCenter + _pulseWidth);

        _colorKeys[0] = new GradientColorKey(_baseColor, 0f);
        _colorKeys[1] = new GradientColorKey(_baseColor, beforeTime);
        _colorKeys[2] = new GradientColorKey(_pulseColor, peakTime);
        _colorKeys[3] = new GradientColorKey(_baseColor, afterTime);
        _colorKeys[4] = new GradientColorKey(_baseColor, 1f);

        _alphaKeys[0] = new GradientAlphaKey(_baseColor.a, 0f);
        _alphaKeys[1] = new GradientAlphaKey(_baseColor.a, beforeTime);
        _alphaKeys[2] = new GradientAlphaKey(_pulseColor.a, peakTime);
        _alphaKeys[3] = new GradientAlphaKey(_baseColor.a, afterTime);
        _alphaKeys[4] = new GradientAlphaKey(_baseColor.a, 1f);

        _gradient.SetKeys(_colorKeys, _alphaKeys);
        _lineRenderer.colorGradient = _gradient;
    }
}
