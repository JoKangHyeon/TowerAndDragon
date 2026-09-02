using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 어미용 액티브 스킬을 쓸 때 화면 전체에 깔리는 속성별 배경 오버레이(A계층)를 켜고 끈다.
///
/// 이 오브젝트는 <b>메인 카메라의 자식</b>으로 씬에 둔다. 오버레이가 월드가 아니라 화면에 붙어 있어야
/// 하기 때문이다 - 카메라를 움직여도 따라와야 한다.
///
/// 프리팹을 미리 인스턴스로 만들어 두는 이유: 밤 전투 중에 발동하는데 그 프레임에 Instantiate가
/// 걸리면 히칭이 보인다. Awake에서 5개를 만들어 꺼 두고 켜고 끄기만 한다.
///
/// <b>페이드아웃은 살아 있는 입자의 알파를 직접 낮춰서 만든다.</b> 방출만 멈추고 입자 수명에 맡기면
/// 배경 팩의 수명이 최장 5초라 연출이 하염없이 남는다(실측). 알파를 내리면 <see cref="_fadeSeconds"/>
/// 안에 확실히 사라진다. 입자 색을 직접 쓰므로 셰이더 종류를 타지 않는다 -
/// ColorOverLifetime은 startColor에 곱해지므로 startColor의 알파를 내리면 최종 알파가 함께 내려간다.
///
/// 어느 속성을 띄울지는 <see cref="DragonTreeManager.ActiveAttribute"/>에서 읽는다. HUD가
/// 활성 속성과 일치하는 액티브만 노출하므로(SkillHudBinder), 발동된 액티브는 항상 활성 속성의 것이다.
///
/// <b>스킬 쪽 이벤트를 구독하지 않는다.</b> "눌렀다"가 아니라 "효과가 있었다"에만 반응해야 하므로,
/// <see cref="SkillTargetingController"/>가 <see cref="Skill.Activate"/>의 반환값을 보고
/// <see cref="PlayForActiveAttribute"/>를 직접 부른다.
///
/// <b>UI_CameraInputBlocker를 붙이지 않는다</b> - 오버레이는 창이 아니다. 붙이면 밤 전투 중
/// 카메라가 죽는다.
/// </summary>
public sealed class SkillCastOverlayHost : MonoBehaviour
{
    // 처음 대여할 입자 버퍼 크기. 모자라면 그때 늘린다 - 오버레이 하나의 동시 입자가 260개쯤이다.
    private const int INITIAL_PARTICLE_BUFFER = 320;

    private const byte MAX_ALPHA = 255;

    // 인스펙터 기본값. 둘을 더한 2초가 화면에 오버레이가 떠 있는 총 시간이다.
    private const float DEFAULT_EMIT_SECONDS = 1.5f;
    private const float DEFAULT_FADE_SECONDS = 0.5f;

    [Header("연출 길이")]
    [Tooltip("스킬을 누른 뒤 입자를 계속 뿜는 시간. 이 뒤로는 방출을 멈추고 페이드아웃한다.")]
    [SerializeField] private float _emitSeconds = DEFAULT_EMIT_SECONDS;

    [Tooltip("남은 입자의 알파를 0까지 내리는 시간. _emitSeconds와 합쳐 화면에 뜨는 총 시간이 된다.")]
    [SerializeField] private float _fadeSeconds = DEFAULT_FADE_SECONDS;

    [Header("참조")]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    // 밤이 끝나 낮으로 바뀌면(몬스터 전멸·시간 종료 무엇이든) 연출을 그 자리에서 걷는다.
    [SerializeField] private CycleManager _cycleManager;

    // 성이 부서지거나 승리해서 게임이 멈추는 경우. 주기 전환이 오지 않을 수 있어 따로 받는다.
    [SerializeField] private GameManager _gameManager;

    [SerializeField] private OverlayEntry[] _overlays;

    /// <summary>속성 하나와 그 오버레이 프리팹의 짝. 배열 순서에 의존하지 않도록 속성을 함께 적는다.</summary>
    [Serializable]
    private sealed class OverlayEntry
    {
        [SerializeField] private DragonType _attribute;
        [SerializeField] private GameObject _prefab;

        public DragonType Attribute => _attribute;
        public GameObject Prefab => _prefab;
    }

    // 떠 있는 오버레이 하나. 매번 GetComponentsInChildren을 돌지 않도록 파티클 목록을 캐시해 둔다.
    private sealed class Overlay
    {
        public GameObject Instance;
        public ParticleSystem[] Particles;
    }

    private readonly Dictionary<DragonType, Overlay> _overlayByAttribute = new();

    private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[INITIAL_PARTICLE_BUFFER];

    private Overlay _playing;
    private CancellationTokenSource _playCancellation;

    private static SkillCastOverlayHost _current;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 참조가 정적 필드에 그대로 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    /// <summary>현재 활성 속성의 시전 오버레이를 띄운다. 호스트가 없는 씬에서는 아무 일도 하지 않는다.</summary>
    public static void PlayForActiveAttribute()
    {
        if (_current != null)
        {
            _current.PlayActive();
        }
    }

    /// <summary>속성을 직접 지정해 띄운다.</summary>
    public static void Play(DragonType attribute)
    {
        if (_current != null)
        {
            _current.PlayInternal(attribute);
        }
    }

    /// <summary>재생 중인 오버레이를 즉시 걷는다. 밤이 끝나거나 게임이 멈출 때 쓴다.</summary>
    public static void StopImmediately()
    {
        if (_current != null)
        {
            _current.CancelAndHide();
        }
    }

    private void Awake()
    {
        BuildOverlays();
    }

    private void OnEnable()
    {
        _current = this;

        WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this);

        if (WiringGuard.Require(_cycleManager, nameof(_cycleManager), this))
        {
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
        }

        if (WiringGuard.Require(_gameManager, nameof(_gameManager), this))
        {
            _gameManager.GameOverOccurred.AddListener(HandleRunEnded);
            _gameManager.VictoryOccurred.AddListener(HandleRunEnded);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
        }

        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred.RemoveListener(HandleRunEnded);
            _gameManager.VictoryOccurred.RemoveListener(HandleRunEnded);
        }

        if (_current == this)
        {
            _current = null;
        }

        CancelAndHide();
    }

    // 카메라 자식이 아니면 오버레이가 월드에 남아 카메라를 움직일 때 따라오지 않는다.
    // 배선 실수를 에디터에서 바로 드러낸다.
    private void OnValidate()
    {
        if (GetComponentInParent<Camera>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(SkillCastOverlayHost)}] 카메라의 자식이 아닙니다 - " +
                "오버레이가 화면에 붙지 않고 월드에 남습니다.", this);
        }

        if (_overlays == null)
        {
            return;
        }

        foreach (OverlayEntry entry in _overlays)
        {
            if (entry != null && entry.Prefab == null)
            {
                Debug.LogWarning(
                    $"[{nameof(SkillCastOverlayHost)}] {entry.Attribute} 오버레이 프리팹이 비어 있습니다.",
                    this);
            }
        }
    }

    private void BuildOverlays()
    {
        if (!WiringGuard.RequireNotEmpty(_overlays, nameof(_overlays), this))
        {
            return;
        }

        foreach (OverlayEntry entry in _overlays)
        {
            if (entry == null || entry.Prefab == null || _overlayByAttribute.ContainsKey(entry.Attribute))
            {
                continue;
            }

            GameObject instance = Instantiate(entry.Prefab, transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);

            _overlayByAttribute.Add(entry.Attribute, new Overlay
            {
                Instance = instance,
                Particles = instance.GetComponentsInChildren<ParticleSystem>(true)
            });
        }
    }

    // 밤이 끝나 낮이 되면(몬스터 전멸이든 시간 종료든) 연출을 남기지 않는다.
    // 밤으로 들어가는 전환에서도 불리지만 그때는 떠 있는 것이 없어 아무 일도 하지 않는다.
    private void HandleCycleChanged(CycleManager.CycleState _) => CancelAndHide();

    private void HandleRunEnded() => CancelAndHide();

    private void PlayActive()
    {
        if (_dragonTreeManager == null || !_dragonTreeManager.ActiveAttribute.HasValue)
        {
            return;
        }

        PlayInternal(_dragonTreeManager.ActiveAttribute.Value);
    }

    private void PlayInternal(DragonType attribute)
    {
        if (!_overlayByAttribute.TryGetValue(attribute, out Overlay overlay))
        {
            return;
        }

        // 앞의 오버레이가 아직 페이드 중일 수 있다. 속성이 무엇이든 일단 걷고 새로 시작해야
        // 알파가 내려간 채로 다시 켜지거나 두 속성이 겹치지 않는다.
        CancelAndHide();

        _playing = overlay;
        overlay.Instance.SetActive(true);

        foreach (ParticleSystem particles in overlay.Particles)
        {
            particles.Clear(false);
            particles.Play(false);
        }

        _playCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        RunAsync(overlay, _playCancellation.Token).Forget();
    }

    private async UniTaskVoid RunAsync(Overlay overlay, CancellationToken token)
    {
        try
        {
            await UniTask.WaitForSeconds(_emitSeconds, cancellationToken: token);

            foreach (ParticleSystem particles in overlay.Particles)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            await FadeOutAsync(overlay, token);

            HideOverlay(overlay);

            if (_playing == overlay)
            {
                _playing = null;
            }
        }
        catch (OperationCanceledException)
        {
            // 다음 발동이나 밤 종료가 앞의 재생을 끊은 정상 경로다. 정리는 끊은 쪽이 맡는다.
        }
    }

    // 남은 입자의 알파를 0까지 내린다. 원래 알파를 따로 저장하지 않고 <b>프레임마다 줄어든 비율만
    // 곱한다</b> - 입자가 수명으로 사라지면서 배열이 바뀌어도 안전하고, 저장할 상태가 없다.
    private async UniTask FadeOutAsync(Overlay overlay, CancellationToken token)
    {
        float elapsed = 0f;
        float previous = 1f;

        while (elapsed < _fadeSeconds)
        {
            await UniTask.Yield(token);

            elapsed += Time.deltaTime;

            float current = Mathf.Clamp01(1f - elapsed / _fadeSeconds);
            ScaleAlpha(overlay, previous > 0f ? current / previous : 0f);
            previous = current;
        }

        // 마지막에 확실히 0으로 만든다 - 프레임 간격에 따라 위 루프가 0에 못 닿을 수 있다.
        ScaleAlpha(overlay, 0f);
    }

    private void ScaleAlpha(Overlay overlay, float ratio)
    {
        foreach (ParticleSystem particles in overlay.Particles)
        {
            int count = particles.particleCount;

            if (count == 0)
            {
                continue;
            }

            if (_particleBuffer.Length < count)
            {
                _particleBuffer = new ParticleSystem.Particle[count];
            }

            int read = particles.GetParticles(_particleBuffer);

            for (int i = 0; i < read; i++)
            {
                Color32 color = _particleBuffer[i].startColor;

                // 반올림하면 비율이 1에 가까울 때 알파가 제자리에 머물러 영영 안 사라진다. 내림을 쓴다.
                color.a = (byte)Mathf.Clamp(Mathf.FloorToInt(color.a * ratio), 0, MAX_ALPHA);
                _particleBuffer[i].startColor = color;
            }

            particles.SetParticles(_particleBuffer, read);
        }
    }

    private void CancelAndHide()
    {
        if (_playCancellation != null)
        {
            _playCancellation.Cancel();
            _playCancellation.Dispose();
            _playCancellation = null;
        }

        if (_playing != null)
        {
            HideOverlay(_playing);
            _playing = null;
        }
    }

    private static void HideOverlay(Overlay overlay)
    {
        foreach (ParticleSystem particles in overlay.Particles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        overlay.Instance.SetActive(false);
    }
}
