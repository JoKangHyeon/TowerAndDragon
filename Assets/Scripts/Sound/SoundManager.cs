using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 재생의 단일 소유자. "무엇을 언제 재생할지"만 담당한다.
/// 볼륨은 SettingsService가 AudioMixer 파라미터로 따로 소유하므로 여기서는 건드리지 않는다
/// (두 스크립트는 서로를 참조하지 않고 AudioMixer를 통해서만 만난다).
///
/// 접근 방식이 다른 매니저(SerializeField 주입)와 다른 이유: 효과음 호출자 대부분이
/// 런타임에 Instantiate되는 프리팹(타워·몬스터)이라 씬 오브젝트를 주입할 수 없다.
/// StringTable과 같은 정적 접근점을 두되, 씬에 SoundManager가 없으면 전부 조용히 무시한다
/// (SoundManager가 없는 테스트 씬들이 그대로 동작해야 한다).
/// </summary>
public class SoundManager : MonoBehaviour
{
    // 크로스페이드하려면 이전 곡과 다음 곡을 동시에 물고 있어야 하므로 BGM 소스는 2개다.
    private const int BGM_SOURCE_COUNT = 2;

    private static SoundManager _current;

    [Tooltip("id → 클립·볼륨·피치 매핑. 비워 두면 모든 재생 요청이 무시된다.")]
    [SerializeField] private SoundCatalog _catalog;

    [Tooltip("효과음 출력 그룹. Assets/AudioMixer.mixer의 SE 그룹을 지정한다.")]
    [SerializeField] private AudioMixerGroup _seGroup;

    [Tooltip("배경음 출력 그룹. Assets/AudioMixer.mixer의 BGM 그룹을 지정한다.")]
    [SerializeField] private AudioMixerGroup _bgmGroup;

    [Tooltip("동시에 울릴 수 있는 효과음 개수. 넘치면 가장 오래된 소리를 덮어쓴다.")]
    [SerializeField] private int _sePoolSize = 12;

    [Tooltip("BGM이 바뀔 때 이전 곡과 겹쳐 넘어가는 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float _bgmFadeDuration = 2f;

    [Tooltip("낮/밤 BGM 전환에 사용. 비워 두면 씬에서 자동으로 찾고, 없으면 자동 전환만 꺼진다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("보스 웨이브(주기 마지막 밤) 판별에 사용. 비워 두면 씬에서 자동으로 찾고, " +
        "없으면 보스 밤에도 일반 밤 BGM이 나온다.")]
    [SerializeField] private WaveCycleProgression _waveCycleProgression;

    [Tooltip("게임오버 BGM 전환에 사용. 비워 두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private GameManager _gameManager;

    private AudioSource[] _seSources;
    private int _nextSeIndex;

    // 같은 사운드가 짧은 간격으로 겹쳐 울리는 것을 막기 위한 마지막 재생 시각(unscaled).
    private readonly Dictionary<SoundId, float> _lastPlayTimes = new();

    private AudioSource[] _bgmSources;
    private int _activeBgmIndex;
    private BgmId _currentBgm;
    private bool _isBgmPlaying;
    private CancellationTokenSource _bgmFadeCts;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 참조가 정적 필드에 그대로 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    /// <summary>효과음을 한 번 재생한다. 씬에 SoundManager가 없으면 아무 일도 하지 않는다.</summary>
    public static void Play(SoundId id)
    {
        if (TryGetCurrent(out SoundManager current))
        {
            current.PlaySe(id);
        }
    }

    /// <summary>BGM을 크로스페이드하며 교체한다. 이미 같은 곡이 흐르고 있으면 무시한다.</summary>
    public static void PlayBgm(BgmId id)
    {
        if (TryGetCurrent(out SoundManager current))
        {
            current.StartBgm(id);
        }
    }

    public static void StopBgm()
    {
        if (TryGetCurrent(out SoundManager current))
        {
            current.FadeOutBgm();
        }
    }

    // Unity 연산자 비교라 파괴된 오브젝트도 null로 걸러진다 - OnDestroy를 놓친 경우의 안전망.
    private static bool TryGetCurrent(out SoundManager current)
    {
        current = _current;
        return current != null;
    }

    private void Awake()
    {
        _current = this;

        _seSources = CreateSources(Mathf.Max(1, _sePoolSize), _seGroup, false);
        _bgmSources = CreateSources(BGM_SOURCE_COUNT, _bgmGroup, true);

        if (_cycleManager == null)
        {
            _cycleManager = FindFirstObjectByType<CycleManager>();
        }

        // UI_Canvas는 프리팹이라 씬 오브젝트를 직렬화해 둘 수 없다 - 실제로는 이쪽 경로로 연결된다.
        if (_waveCycleProgression == null)
        {
            _waveCycleProgression = FindFirstObjectByType<WaveCycleProgression>();
        }

        if (_gameManager == null)
        {
            _gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void OnEnable()
    {
        // 이 UnityEvent들은 인라인 초기화가 없어 씬 YAML에 항목이 없으면 null이다(테스트 씬 등).
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred?.AddListener(HandleGameOver);
        }

        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayStart?.AddListener(HandleDayStart);
        _cycleManager.OnNightStart?.AddListener(HandleNightStart);

        // 구독 직후 현재 주기를 한 번 반영해, 첫 발화를 놓쳐도 무음으로 남지 않게 한다.
        PlayCycleBgm(_cycleManager.CurrentCycle);
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred?.RemoveListener(HandleGameOver);
        }

        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayStart?.RemoveListener(HandleDayStart);
        _cycleManager.OnNightStart?.RemoveListener(HandleNightStart);
    }

    private void OnDestroy()
    {
        if (_current == this)
        {
            _current = null;
        }

        CancelFade();
    }

    // AudioSource는 이 오브젝트에 그대로 붙인다 - 자식 오브젝트를 만들면 이름 문자열이 필요해지고,
    // 2D 사운드라 위치를 따로 둘 이유도 없다.
    private AudioSource[] CreateSources(int count, AudioMixerGroup group, bool isLooping)
    {
        var sources = new AudioSource[count];

        for (int i = 0; i < count; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = isLooping;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            sources[i] = source;
        }

        return sources;
    }

    private void PlaySe(SoundId id)
    {
        if (_catalog == null || !_catalog.TryGet(id, out SoundEntry entry))
        {
            return;
        }

        // 클립 미수급 상태(애셋을 아직 못 받은 항목)는 정상이므로 경고 없이 넘긴다.
        if (entry.Clip == null || IsOnCooldown(id, entry))
        {
            return;
        }

        _lastPlayTimes[id] = Time.unscaledTime;

        AudioSource source = TakeSeSource();
        source.pitch = entry.NextPitch;
        source.PlayOneShot(entry.Clip, entry.Volume);
    }

    private bool IsOnCooldown(SoundId id, SoundEntry entry)
    {
        if (entry.MinInterval <= 0f)
        {
            return false;
        }

        return _lastPlayTimes.TryGetValue(id, out float lastPlayTime)
            && Time.unscaledTime - lastPlayTime < entry.MinInterval;
    }

    // 비어 있는 소스를 우선 쓰고, 전부 재생 중이면 가장 오래 전에 잡힌 소스를 덮어쓴다.
    private AudioSource TakeSeSource()
    {
        for (int offset = 0; offset < _seSources.Length; offset++)
        {
            int index = (_nextSeIndex + offset) % _seSources.Length;
            if (!_seSources[index].isPlaying)
            {
                _nextSeIndex = (index + 1) % _seSources.Length;
                return _seSources[index];
            }
        }

        AudioSource oldest = _seSources[_nextSeIndex];
        _nextSeIndex = (_nextSeIndex + 1) % _seSources.Length;
        return oldest;
    }

    private void HandleGameOver() => StartBgm(BgmId.GameOver);

    private void HandleDayStart(int cycle) => PlayCycleBgm(CycleManager.CycleState.Day);

    private void HandleNightStart(int cycle) => PlayCycleBgm(CycleManager.CycleState.Night);

    private void PlayCycleBgm(CycleManager.CycleState state)
    {
        // 게임이 끝난 뒤에도 주기가 한 번 더 돌면 결과 BGM이 낮/밤 BGM에 덮인다.
        if (_gameManager != null && _gameManager.IsGameEnded)
        {
            return;
        }

        StartBgm(state == CycleManager.CycleState.Night ? NightBgm : BgmId.Day);
    }

    /// <summary>
    /// 이번 밤에 틀 BGM. 주기의 마지막 웨이브(7일차)는 보스전이라 곡이 다르다.
    /// 스냅샷은 그날 낮 시작에 확정되므로(WaveCycleProgression.HandleDayStart),
    /// 밤 시작 시점에는 이미 오늘 값으로 갱신돼 있다.
    /// </summary>
    private BgmId NightBgm
    {
        get
        {
            bool isBossNight =
                _waveCycleProgression != null &&
                _waveCycleProgression.HasCurrentSnapshot &&
                _waveCycleProgression.CurrentSnapshot.IsBossWave;

            // 보스 곡을 아직 못 받았다면 무음이 되는 것보다 일반 밤 BGM이 낫다.
            return isBossNight && HasBgmClip(BgmId.Boss) ? BgmId.Boss : BgmId.Night;
        }
    }

    private bool HasBgmClip(BgmId id)
    {
        return _catalog != null && _catalog.TryGet(id, out BgmEntry entry) && entry.Clip != null;
    }

    private void StartBgm(BgmId id)
    {
        if (_isBgmPlaying && _currentBgm == id)
        {
            return;
        }

        if (_catalog == null || !_catalog.TryGet(id, out BgmEntry entry) || entry.Clip == null)
        {
            return;
        }

        _currentBgm = id;
        _isBgmPlaying = true;

        _activeBgmIndex = (_activeBgmIndex + 1) % BGM_SOURCE_COUNT;

        AudioSource next = _bgmSources[_activeBgmIndex];
        next.clip = entry.Clip;
        next.volume = 0f;
        next.Play();

        StartFade(entry.Volume);
    }

    private void FadeOutBgm()
    {
        if (!_isBgmPlaying)
        {
            return;
        }

        _isBgmPlaying = false;
        StartFade(0f);
    }

    private void StartFade(float targetVolume)
    {
        CancelFade();
        _bgmFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        FadeAsync(targetVolume, _bgmFadeCts.Token).Forget();
    }

    private void CancelFade()
    {
        if (_bgmFadeCts == null)
        {
            return;
        }

        _bgmFadeCts.Cancel();
        _bgmFadeCts.Dispose();
        _bgmFadeCts = null;
    }

    private async UniTaskVoid FadeAsync(float targetVolume, CancellationToken token)
    {
        var startVolumes = new float[BGM_SOURCE_COUNT];
        for (int i = 0; i < BGM_SOURCE_COUNT; i++)
        {
            startVolumes[i] = _bgmSources[i].volume;
        }

        float elapsed = 0f;
        while (elapsed < _bgmFadeDuration)
        {
            await UniTask.Yield(token);

            // 일시정지(GameSpeedManager가 Time.timeScale을 0으로 만든다) 중에도 전환이 끝나도록
            // scaled가 아닌 unscaled 시간을 쓴다. 아니면 두 곡이 섞인 채로 멈춘다.
            elapsed += Time.unscaledDeltaTime;
            ApplyFade(startVolumes, targetVolume, Mathf.Clamp01(elapsed / _bgmFadeDuration));
        }

        ApplyFade(startVolumes, targetVolume, 1f);
        StopSilentBgmSources();
    }

    private void ApplyFade(float[] startVolumes, float targetVolume, float t)
    {
        for (int i = 0; i < BGM_SOURCE_COUNT; i++)
        {
            float sourceTarget = IsActiveBgmSource(i) ? targetVolume : 0f;
            _bgmSources[i].volume = Mathf.Lerp(startVolumes[i], sourceTarget, t);
        }
    }

    private bool IsActiveBgmSource(int index) => _isBgmPlaying && index == _activeBgmIndex;

    // 다 줄어든 소스는 계속 돌려 둘 이유가 없으므로 정지하고 클립 참조도 놓아 준다.
    private void StopSilentBgmSources()
    {
        for (int i = 0; i < BGM_SOURCE_COUNT; i++)
        {
            if (IsActiveBgmSource(i))
            {
                continue;
            }

            _bgmSources[i].Stop();
            _bgmSources[i].clip = null;
        }
    }
}
