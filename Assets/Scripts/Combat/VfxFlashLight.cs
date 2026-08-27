using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 발사·명중 이펙트에 딸린 섬광. 오브젝트가 켜진 순간 최대 밝기로 시작해 <see cref="_durationSeconds"/>
/// 동안 0까지 줄어든다.
///
/// 이 연출은 풀에서 꺼내고 되돌리는 방식이라(<see cref="ProjectilePool.PlayForSeconds"/>)
/// OnEnable이 곧 "발사된 순간"이다 - 발사 이벤트를 따로 구독하지 않는다.
///
/// 타워의 조준 콘(<see cref="TowerLightAimer"/>)과는 별개다. 그쪽은 타겟이 있는 동안 계속 켜져 있는
/// 조준 표시이고, 이쪽은 한 발마다 터지는 섬광이라 파티클과 수명을 공유해야 한다.
/// <see cref="_durationSeconds"/>를 그 이펙트의 반납 시간과 같게 두는 것이 기본이다 -
/// 그래야 빛과 입자가 같은 순간에 사라진다.
/// </summary>
[RequireComponent(typeof(Light2D))]
public sealed class VfxFlashLight : MonoBehaviour
{
    private const float DEFAULT_DURATION_SECONDS = 0.3f;
    private const float DEFAULT_PEAK_INTENSITY = 1f;
    private const float CURVE_START_TIME = 0f;
    private const float CURVE_END_TIME = 1f;

    [Tooltip("섬광이 0까지 줄어드는 데 걸리는 시간(초). 이 이펙트의 반납 시간과 같게 둔다.")]
    [Min(0f)]
    [SerializeField] private float _durationSeconds = DEFAULT_DURATION_SECONDS;

    [Tooltip("켜진 순간의 밝기.")]
    [Min(0f)]
    [SerializeField] private float _peakIntensity = DEFAULT_PEAK_INTENSITY;

    [Tooltip("시간에 따른 밝기 배율. 1에서 0으로 내려오는 모양이어야 한다.")]
    [SerializeField] private AnimationCurve _falloff = AnimationCurve.EaseInOut(
        CURVE_START_TIME,
        CURVE_END_TIME,
        CURVE_END_TIME,
        CURVE_START_TIME);

    private Light2D _light;
    private float _elapsedSeconds;

    private bool IsFinished => _elapsedSeconds >= _durationSeconds;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
    }

    // 풀에서 꺼내질 때마다 처음부터 다시 시작한다. 지난번에 다 타고 0으로 남은 밝기를
    // 여기서 되돌리지 않으면 두 번째 발사부터 빛이 아예 보이지 않는다.
    //
    // 켜는 판단에 IsFinished를 거치는 이유: 지속시간 0은 [Min(0f)]이라 인스펙터에서 넣을 수 있고,
    // 그 경우 IsFinished가 처음부터 참이라 Update가 이른 반환에 걸려 거기 있는
    // _light.enabled = false에 영영 닿지 못한다 - 밝기 0인 빛이 반납될 때까지 켜진 채로 남는다.
    private void OnEnable()
    {
        _elapsedSeconds = 0f;
        _light.enabled = !IsFinished;
        ApplyIntensity();
    }

    private void Update()
    {
        if (IsFinished)
        {
            return;
        }

        _elapsedSeconds += Time.deltaTime;
        ApplyIntensity();

        // 다 타면 컴포넌트를 끈다 - 반납까지 남은 시간 동안 밝기 0인 빛을 계속 그리지 않는다.
        if (IsFinished)
        {
            _light.enabled = false;
        }
    }

    private void ApplyIntensity()
    {
        float normalizedTime = _durationSeconds > 0f
            ? Mathf.Clamp01(_elapsedSeconds / _durationSeconds)
            : CURVE_END_TIME;

        _light.intensity = _peakIntensity * _falloff.Evaluate(normalizedTime);
    }
}
