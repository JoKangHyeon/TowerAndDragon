using UnityEngine;

/// <summary>
/// LineRenderer의 색상 그라디언트를 시간에 따라 움직여, 포탈(t=0) -> 성(t=1) 방향으로
/// 빛 뭉치가 흐르는 듯한 이펙트를 낸다. 적이 포탈에서 성으로 진격하는 방향을 시각적으로 암시한다.
/// 빛 뭉치는 혜성 꼬리 모양(머리에서 급감쇠 후 뒤로 긴 꼬리)이라, 정지된 프레임 한 장만으로도
/// 진행 방향(꼬리 반대쪽)이 읽힌다. Gradient는 color/alpha 키가 각 8개로 제한되어 있어
/// (2 + 펄스당 2키), 동시에 흘릴 수 있는 빛 뭉치는 최대 3개다.
///
/// 여러 펄스를 라인 [0,1) 안에 꽉 채워 등간격으로 배치하다 보니, 매 주기마다 모든 펄스의
/// 위치가 한 칸씩(spacing만큼) 뒤로 순간 이동하는 시점이 생긴다(firstPeak가 Repeat로 감싸질
/// 때의 불연속점). 이 순간 라인 끝(t=1)에 있던 펄스가 최대 밝기인 채로 사라지면 "확 없어짐"
/// 처럼 보인다. 그래서 각 펄스의 밝기 자체를 양 끝(headWidth 폭)에서 미리 낮춰(edgeWindow),
/// 경계에 닿을 즈음엔 이미 baseColor에 가까워 순간 이동이 눈에 띄지 않게 한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PathFlowEffect : MonoBehaviour
{
    private const int MAX_PULSE_COUNT = 3;
    private const int KEYS_PER_PULSE = 2;
    private const int EDGE_KEY_COUNT = 2; // t=0, t=1 보정 키
    private const float MIN_TRAVEL_DURATION = 0.05f; // 0 나눗셈 방지
    private const float MAX_HEAD_WIDTH_RATIO = 0.5f; // 머리(급감쇠 구간)가 펄스 간격의 절반을 못 넘게

    [Tooltip("포탈에서 성까지 빛 뭉치가 한 번 이동하는 데 걸리는 시간(초)")]
    [Min(MIN_TRAVEL_DURATION)]
    [SerializeField] private float _travelDuration = 1f;

    [Tooltip("라인 위에 동시에 흐르는 빛 뭉치 수. 출발 간격 = Travel Duration ÷ 이 값")]
    [Range(1, MAX_PULSE_COUNT)]
    [SerializeField] private int _pulseCount = MAX_PULSE_COUNT;

    [Tooltip("빛 뭉치 머리(밝은 부분)의 폭 (0~1, 라인 전체 길이 대비 비율). 라인 양 끝에서" +
        " 밝기가 옅어지는 구간의 폭이기도 하다.")]
    [SerializeField] private float _pulseWidth = 0.15f;

    [SerializeField] private Color _baseColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color _pulseColor = Color.white;

    private LineRenderer _lineRenderer;
    private GradientColorKey[] _colorKeys;
    private GradientAlphaKey[] _alphaKeys;
    private Gradient _gradient;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _gradient = new Gradient();
    }

    private void Update()
    {
        int pulseCount = Mathf.Clamp(_pulseCount, 1, MAX_PULSE_COUNT);
        float travelDuration = Mathf.Max(_travelDuration, MIN_TRAVEL_DURATION);
        float spacing = 1f / pulseCount;
        float headWidth = Mathf.Min(_pulseWidth, spacing * MAX_HEAD_WIDTH_RATIO);

        float phase = Mathf.Repeat(Time.time / travelDuration, 1f);
        float firstPeak = Mathf.Repeat(phase, spacing);

        ApplyFlowGradient(pulseCount, spacing, headWidth, firstPeak);
    }

    private void EnsureKeyBuffers(int pulseCount)
    {
        int keyCount = EDGE_KEY_COUNT + pulseCount * KEYS_PER_PULSE;

        if (_colorKeys != null && _colorKeys.Length == keyCount)
        {
            return;
        }

        _colorKeys = new GradientColorKey[keyCount];
        _alphaKeys = new GradientAlphaKey[keyCount];
    }

    private void ApplyFlowGradient(int pulseCount, float spacing, float headWidth, float firstPeak)
    {
        EnsureKeyBuffers(pulseCount);

        _colorKeys[0] = new GradientColorKey(_baseColor, 0f);
        _alphaKeys[0] = new GradientAlphaKey(_baseColor.a, 0f);

        int keyIndex = 1;

        for (int i = 0; i < pulseCount; i++)
        {
            float peakTime = firstPeak + i * spacing;
            float troughTime = Mathf.Min(peakTime + headWidth, 1f);
            float edgeWindow = ComputeEdgeWindow(peakTime, headWidth);

            _colorKeys[keyIndex] = new GradientColorKey(Color.Lerp(_baseColor, _pulseColor, edgeWindow), peakTime);
            _alphaKeys[keyIndex] = new GradientAlphaKey(Mathf.Lerp(_baseColor.a, _pulseColor.a, edgeWindow), peakTime);
            keyIndex++;

            _colorKeys[keyIndex] = new GradientColorKey(_baseColor, troughTime);
            _alphaKeys[keyIndex] = new GradientAlphaKey(_baseColor.a, troughTime);
            keyIndex++;
        }

        _colorKeys[keyIndex] = new GradientColorKey(_baseColor, 1f);
        _alphaKeys[keyIndex] = new GradientAlphaKey(_baseColor.a, 1f);

        _gradient.SetKeys(_colorKeys, _alphaKeys);
        _lineRenderer.colorGradient = _gradient;
    }

    // 라인 양 끝에서 headWidth 폭만큼 밝기를 0으로 낮추는 창 함수. 중앙에서는 1(감쇠 없음).
    private static float ComputeEdgeWindow(float peakTime, float headWidth)
    {
        if (headWidth <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(peakTime / headWidth) * Mathf.Clamp01((1f - peakTime) / headWidth);
    }
}
