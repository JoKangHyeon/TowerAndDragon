using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[RequireComponent(typeof(Light2D))]
public class CycleLight : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("IsOnWhileDay")]
    [Tooltip("false면 밤에 빛을 킴 (_isSingleGlobalLight가 true면 사용하지 않음)")]
    private bool _isOnWhileDay;

    [SerializeField]
    [Tooltip("체크하면 이 라이트 하나가 낮/밤 색상을 모두 담당하는 유일한 Global 라이트로 동작한다. " +
        "URP 2D는 같은 정렬 레이어에 활성화된 Global 라이트가 2개 이상이면 안 되므로(Renderer2D가 " +
        "'More than one global light' 에러를 내며 어느 쪽이 적용될지 보장하지 않는다), Day/Night용 " +
        "Global 라이트를 따로 두지 않고 하나로 합칠 때 이 모드를 쓴다.")]
    private bool _isSingleGlobalLight;

    [SerializeField]
    [Tooltip("_isSingleGlobalLight일 때의 밤 색상")]
    private Color _nightColor = new Color(0.237f, 0.184f, 0.764f);

    [SerializeField]
    [Tooltip("_isSingleGlobalLight일 때의 밤 밝기")]
    private float _nightIntensity = 0.15f;

    [SerializeField]
    [Tooltip("꺼짐<->켜짐 전환에 걸리는 시간(초)")]
    private float _transitionDuration = 3f;

    [SerializeField]
    [Tooltip("전환 도중 잠깐 섞여 노을처럼 보이게 할 색")]
    private Color _duskColor = new Color(1f, 0.55f, 0.25f);

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("전환 중간 지점에서 노을색이 섞이는 최대 비율")]
    private float _duskWeight = 0.6f;

    public bool IsOnWhileDay => _isOnWhileDay;

    private Light2D _light2D;
    private CycleManager _cycleManager;
    private Color _baseColor;
    private float _baseIntensity;
    private CancellationTokenSource _transitionCts;

    // 게임 시작(또는 세이브 복원) 직후의 첫 전환은 연출 없이 목표 상태로 맞춘다.
    // 씬에 저장된 라이트 상태가 시작 시점의 낮/밤과 다를 수 있는데, 그때 페이드를 재생하면
    // "켜져 있던 것이 서서히 꺼지는" 연출이 시작 화면에서 재생된다 - 성 등불(_isOnWhileDay=false,
    // 씬에는 켜진 상태로 저장)이 낮에 시작하면서 3초간 꺼지며 성이 어두워 보이던 원인이다.
    // 이후의 낮↔밤 전환은 정상적으로 페이드한다.
    private bool _hasAppliedInitialState;

    private void Awake()
    {
        _light2D = GetComponent<Light2D>();
        _baseColor = _light2D.color;
        _baseIntensity = _light2D.intensity;
    }

    public void Construct(CycleManager cycleManager)
    {
        _cycleManager = cycleManager;
        _cycleManager.OnDayReady.AddListener(OnDayStart);
        _cycleManager.OnNightStart.AddListener(OnNightStart);
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(OnDayStart);
            _cycleManager.OnNightStart.RemoveListener(OnNightStart);
        }

        CancelTransition();
    }

    public void OnDayStart(int cycle)
    {
        if (_isSingleGlobalLight)
            StartTransition(_baseColor, _baseIntensity);
        else
            StartTransition(IsOnWhileDay);
    }

    public void OnNightStart(int cycle)
    {
        if (_isSingleGlobalLight)
            StartTransition(_nightColor, _nightIntensity);
        else
            StartTransition(!IsOnWhileDay);
    }

    // 온/오프 방식(횃불 등 Point 라이트) - 켜질 땐 이 라이트 고유 색/밝기로, 꺼질 땐 밝기 0으로 페이드한다.
    private void StartTransition(bool turningOn) =>
        StartTransition(turningOn ? _baseColor : _light2D.color, turningOn ? _baseIntensity : 0f);

    private void StartTransition(Color targetColor, float targetIntensity)
    {
        CancelTransition();

        // 첫 전환은 초기 상태를 맞추는 것이므로 연출하지 않는다.
        if (!_hasAppliedInitialState)
        {
            _hasAppliedInitialState = true;
            ApplyImmediate(targetColor, targetIntensity);
            return;
        }

        _transitionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        TransitionAsync(targetColor, targetIntensity, _transitionCts.Token).Forget();
    }

    // 색·밝기와 함께 활성 상태까지 한 번에 맞춘다. 밝기가 0인 Point 라이트를 켜 둘 이유가 없으므로
    // 끈다 - 단일 글로벌 라이트는 밤 밝기가 0에 가까워도 계속 켜져 있어야 화면이 새까매지지 않는다.
    private void ApplyImmediate(Color color, float intensity)
    {
        _light2D.color = color;
        _light2D.intensity = intensity;
        _light2D.enabled = _isSingleGlobalLight || intensity > 0f;
    }

    private void CancelTransition()
    {
        if (_transitionCts == null)
        {
            return;
        }

        _transitionCts.Cancel();
        _transitionCts.Dispose();
        _transitionCts = null;
    }

    private async UniTaskVoid TransitionAsync(Color targetColor, float targetIntensity, CancellationToken token)
    {
        Color startColor = _light2D.color;
        float startIntensity = _light2D.intensity;

        _light2D.enabled = true;

        // 시작값과 목표값이 이미 같으면(예: 게임 시작 직후 첫 OnDayStart가 이미 Day 기본값인
        // 라이트에 같은 Day 값을 목표로 걸 때) 실제로는 바뀔 게 없는데도 아래 노을색 블렌드가
        // 무조건 섞여 들어가 라이트가 잠깐 노을색으로 물들었다 돌아오는 것처럼 보인다.
        // 변화가 없는 전환은 그대로 스냅하고 애니메이션을 건너뛴다.
        if (startColor == targetColor && Mathf.Approximately(startIntensity, targetIntensity))
        {
            ApplyImmediate(targetColor, targetIntensity);
            return;
        }

        float elapsed = 0f;
        while (elapsed < _transitionDuration)
        {
            await UniTask.Yield(token);
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / _transitionDuration);
            float easedT = t * t * (3f - 2f * t);

            // 세모형(선형) 대신 제곱한 세모형을 써서 노을색이 중앙에만 좁게 몰리고
            // 전환 끝자락(밝기가 거의 다 찬 구간)에서는 먼저 걷혀 있도록 한다.
            // 그래야 "노을이 안 걷힌 채로 있다가 막판에 밝기+색이 한꺼번에 훅 튀는" 부자연스러움이 없다.
            float tent = 1f - Mathf.Abs(t - 0.5f) * 2f;
            float duskWeight = tent * tent * _duskWeight;

            _light2D.color = Color.Lerp(Color.Lerp(startColor, targetColor, easedT), _duskColor, duskWeight);
            _light2D.intensity = Mathf.Lerp(startIntensity, targetIntensity, easedT);
        }

        ApplyImmediate(targetColor, targetIntensity);
    }
}
