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

    // 뷰포트 좌표는 화면 중앙이 0.5, 양 끝이 0과 1이다. 중앙에서의 거리를 화면 가장자리가 1이 되도록 정규화한다.
    private const float VIEWPORT_CENTER = 0.5f;
    private const float VIEWPORT_EDGE_SCALE = 2f;

    private const float FULL_VOLUME_SCALE = 1f;
    private const float CENTER_PAN = 0f;
    private const float PAN_LIMIT = 1f;

    // 이보다 작아진 소리는 아예 재생하지 않는다. 볼륨 0에 가까워도 AudioSource는 클립 길이만큼 잡혀 있어
    // 들리지도 않는 화면 밖 소리가 화면 안 소리의 자리를 뺏는다.
    private const float MIN_AUDIBLE_VOLUME_SCALE = 0.05f;

    private static SoundManager _current;

    // 로딩 화면이 이전 씬의 BGM을 넘겨받아 트는 동안에는 새 씬의 매니저가 자기 곡을 시작하지 않는다.
    // 정적인 이유: 넘긴 쪽(사라질 씬의 매니저)과 되돌려받는 쪽(새 씬의 매니저)이 서로 다른 인스턴스다.
    private static bool _isBgmHandedOff;

    // 넘겨준 동안 들어온 재생 요청. 화면이 걷힐 때 이 곡으로 시작한다.
    private static bool _hasPendingBgm;
    private static BgmId _pendingBgm;

    // 넘겨준 곡. 씬이 끝내 바뀌지 않았을 때(로드 실패·취소) 이 곡으로 되돌린다.
    private static BgmId _handedOffBgm;

    // 곡을 넘긴 매니저. 넘긴 쪽은 씬이 내려갈 때까지 1초 넘게 더 살아 있어서 그 동안에도
    // 낮/밤 전환 이벤트를 받는데, 그 요청까지 받아 두면 새 씬이 이전 씬의 곡으로 시작한다.
    private static SoundManager _handOffOwner;

    [Tooltip("id → 클립·볼륨·피치 매핑. 비워 두면 모든 재생 요청이 무시된다.")]
    [SerializeField] private SoundCatalog _catalog;

    [Tooltip("효과음 출력 그룹. Assets/AudioMixer.mixer의 SE 그룹을 지정한다.")]
    [SerializeField] private AudioMixerGroup _seGroup;

    [Tooltip("배경음 출력 그룹. Assets/AudioMixer.mixer의 BGM 그룹을 지정한다.")]
    [SerializeField] private AudioMixerGroup _bgmGroup;

    [Tooltip("동시에 울릴 수 있는 효과음 개수. 넘치면 가장 오래된 소리를 덮어쓴다.")]
    [SerializeField] private int _sePoolSize = 12;

    [Tooltip("켜면 동시에 울린 효과음 수의 최고치가 갱신될 때마다 로그를 남긴다. 믹싱 조정(M6)용이며 평소에는 끈다.")]
    [SerializeField] private bool _logSeUsagePeak;

    [Tooltip("BGM이 바뀔 때 이전 곡과 겹쳐 넘어가는 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float _bgmFadeDuration = 2f;

    [Tooltip("낮/밤 BGM 전환에 사용. 비워 두면 씬에서 자동으로 찾고, 없으면 자동 전환만 꺼진다.")]
    [WiringOptional]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("보스 웨이브(주기 마지막 밤) 판별에 사용. 비워 두면 씬에서 자동으로 찾고, " +
        "없으면 보스 밤에도 일반 밤 BGM이 나온다.")]
    [WiringOptional]
    [SerializeField] private WaveCycleProgression _waveCycleProgression;

    [Tooltip("게임오버 BGM 전환에 사용. 비워 두면 씬에서 자동으로 찾는다.")]
    [WiringOptional]
    [SerializeField] private GameManager _gameManager;

    private AudioSource[] _seSources;
    private int _nextSeIndex;
    private int _seUsagePeak;

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
        _isBgmHandedOff = false;
        _hasPendingBgm = false;
        _handOffOwner = null;
    }

    /// <summary>효과음을 한 번 재생한다. 씬에 SoundManager가 없으면 아무 일도 하지 않는다.</summary>
    public static void Play(SoundId id)
    {
        if (TryGetCurrent(out SoundManager current))
        {
            current.PlaySe(id);
        }
    }

    /// <summary>
    /// 위치가 있는 효과음을 재생한다. 화면 밖이면 재생하지 않고, 화면 가장자리 쪽일수록 작아지며 좌우로 정위된다.
    /// 월드 거리가 아니라 화면 좌표로 판정하는 이유: 카메라가 오르소그래픽이고 줌이 카메라를 움직이지 않고
    /// orthographicSize만 바꾸므로, 월드 거리로 자르면 줌아웃해서 화면에 보이는 타워가 들리지 않는다.
    /// </summary>
    public static void Play(SoundId id, Vector3 worldPosition)
    {
        if (TryGetCurrent(out SoundManager current))
        {
            current.PlaySeAt(id, worldPosition);
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

    /// <summary>
    /// 지금 흐르는 BGM을 다른 재생자에게 넘긴다. 씬이 언로드되면 이 매니저가 함께 사라져 소리가 뚝 끊기므로,
    /// 씬 경계를 넘어 살아남는 쪽(<see cref="SceneLoadOverlay"/>)이 이어 받아 튼다.
    ///
    /// 넘기는 즉시 이쪽은 멈춘다 - 씬 언로드까지 1초 넘게 걸려(SceneLoadOverlay의 측정치) 그 동안
    /// 같은 클립이 두 소스에서 겹쳐 울리면 이어지는 게 아니라 소리만 커진다.
    /// </summary>
    public static bool TryHandOffBgm(out BgmHandoff handoff)
    {
        if (TryGetCurrent(out SoundManager current))
        {
            return current.HandOffBgm(out handoff);
        }

        handoff = default;
        return false;
    }

    /// <summary>
    /// 넘겨줬던 BGM을 되돌려받는다. 로딩 화면이 <b>걷히는 시점</b>에 부른다 - 그보다 먼저 풀면
    /// 아직 시계가 도는 화면 뒤에서 새 씬의 곡이 올라와 두 곡이 겹친다.
    /// 넘겨준 동안 보류해 둔 곡이 있으면 여기서 시작한다. 넘긴 적이 없으면 아무 일도 하지 않는다.
    /// </summary>
    public static void ResumeBgmAfterHandoff()
    {
        if (!_isBgmHandedOff)
        {
            return;
        }

        _isBgmHandedOff = false;
        _handOffOwner = null;

        // 보류된 요청이 있으면 그것이 새 씬이 틀려던 곡이다. 없다면 씬이 끝내 바뀌지 않았다는 뜻이므로
        // (Build Settings 누락으로 로드가 시작조차 못 한 경우 등) 넘겨줬던 곡을 되돌린다 -
        // 그냥 두면 원래 화면이 그대로 보이는데 음악만 영영 죽은 채로 남는다.
        BgmId resumed = _hasPendingBgm ? _pendingBgm : _handedOffBgm;
        _hasPendingBgm = false;

        PlayBgm(resumed);
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

        _cycleManager.OnDayReady?.AddListener(HandleDayStart);
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

        _cycleManager.OnDayReady?.RemoveListener(HandleDayStart);
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


    // 믹싱 조정용 계측. 풀이 실제로 포화되는지가 볼륨·MinInterval을 만지기 전에 알아야 할 첫 숫자다.
    // 포화되면 재생 중인 소스를 덮어써서 소리가 잘리고 피치·정위가 튄다(설계 문서 §14).
    private void LateUpdate()
    {
        if (!_logSeUsagePeak || _seSources == null)
        {
            return;
        }

        int playing = 0;
        foreach (AudioSource source in _seSources)
        {
            if (source != null && source.isPlaying)
            {
                playing++;
            }
        }

        if (playing <= _seUsagePeak)
        {
            return;
        }

        _seUsagePeak = playing;
        Debug.Log($"[SoundManager] 동시 재생 최고치 {_seUsagePeak}/{_seSources.Length}", this);
    }

    private void PlaySe(SoundId id)
    {
        // 소스를 풀에서 돌려 쓰므로 정위를 되돌린다 - 안 그러면 직전 타워음의 좌우 치우침이 UI음에 남는다.
        PlaySe(id, FULL_VOLUME_SCALE, CENTER_PAN);
    }

    private void PlaySeAt(SoundId id, Vector3 worldPosition)
    {
        if (!TryGetScreenPlacement(worldPosition, out float volumeScale, out float pan))
        {
            // 화면 밖이라 들리지 않는다. 재생하지 않았으므로 MinInterval도 소비하지 않는다 -
            // 들리지도 않은 소리가 곧이어 화면 안에서 날 소리를 막으면 안 된다.
            return;
        }

        PlaySe(id, volumeScale, pan);
    }

    private void PlaySe(SoundId id, float volumeScale, float pan)
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
        source.panStereo = pan;
        source.PlayOneShot(entry.Clip, entry.Volume * volumeScale);
    }

    /// <summary>
    /// 화면 좌표로 볼륨 배수와 좌우 정위를 구한다. 화면 밖으로 컷오프 비율만큼 벗어났으면 false다.
    /// spatialBlend는 0으로 두고 여기서 직접 계산한다 - AudioListener가 카메라에 붙어 있는데
    /// 줌이 카메라를 움직이지 않아 Unity의 3D 감쇠는 줌에 반응하지 못한다.
    /// </summary>
    private bool TryGetScreenPlacement(Vector3 worldPosition, out float volumeScale, out float pan)
    {
        volumeScale = FULL_VOLUME_SCALE;
        pan = CENTER_PAN;

        Camera camera = Camera.main;
        if (camera == null || _catalog == null)
        {
            // 카메라 없는 테스트 씬에서는 기존 2D 재생과 똑같이 둔다.
            return true;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(worldPosition);
        float offsetX = (viewportPoint.x - VIEWPORT_CENTER) * VIEWPORT_EDGE_SCALE;
        float offsetY = (viewportPoint.y - VIEWPORT_CENTER) * VIEWPORT_EDGE_SCALE;
        float screenDistance = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY));

        float cutoffRatio = _catalog.SeCutoffViewportRatio;
        if (screenDistance >= cutoffRatio)
        {
            return false;
        }

        float fullVolumeRatio = _catalog.SeFullVolumeViewportRatio;
        volumeScale = screenDistance <= fullVolumeRatio
            ? FULL_VOLUME_SCALE
            : Mathf.InverseLerp(cutoffRatio, fullVolumeRatio, screenDistance);

        volumeScale *= GetZoomVolumeScale(camera);

        if (volumeScale < MIN_AUDIBLE_VOLUME_SCALE)
        {
            return false;
        }

        pan = Mathf.Clamp(offsetX, -PAN_LIMIT, PAN_LIMIT) * _catalog.SePanAmount;
        return true;
    }

    // 줌아웃할수록 멀어진 느낌이 나도록 볼륨을 낮춘다. 화면 위 위치와는 별개라 화면 중앙에 있는 타워도
    // 줌 배율만으로 소리가 변한다. CameraController의 줌 범위를 참조하지 않는 이유는 사운드가 카메라
    // 스크립트에 의존하지 않게 하려는 것 - 줌 범위를 바꿔도 여기는 그대로 둔다.
    private float GetZoomVolumeScale(Camera camera)
    {
        if (!camera.orthographic || camera.orthographicSize <= 0f)
        {
            return FULL_VOLUME_SCALE;
        }

        return Mathf.Clamp(
            _catalog.SeZoomReferenceSize / camera.orthographicSize,
            _catalog.SeMinZoomVolumeScale,
            FULL_VOLUME_SCALE);
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
        // 로딩 화면이 이전 씬의 곡을 이어 트는 중이면 얹지 않고 미뤄 둔다 - 지금 시작하면
        // 아직 시계가 도는 화면 뒤에서 두 곡이 겹친다. 화면이 걷힐 때 ResumeBgmAfterHandoff가 시작한다.
        if (_isBgmHandedOff)
        {
            // 곡을 넘긴 매니저 자신의 요청은 버린다. 그 씬은 이제 내려가는 중인데도 낮/밤 전환 구독이
            // 살아 있어서, 하필 그 순간 밤이 되면 새 씬이 이전 씬의 밤 곡으로 시작해 버린다.
            if (this != _handOffOwner)
            {
                _pendingBgm = id;
                _hasPendingBgm = true;
            }

            return;
        }

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

    private bool HandOffBgm(out BgmHandoff handoff)
    {
        handoff = default;

        if (!_isBgmPlaying)
        {
            return false;
        }

        AudioSource source = _bgmSources[_activeBgmIndex];

        if (source.clip == null)
        {
            return false;
        }

        // 페이드가 도는 중이면 아직 올라가는 중인 볼륨이 그대로 굳는다 - 넘겨받는 쪽에는 이어서
        // 올릴 방법이 없어서, 곡이 시작되자마자 씬을 옮기면 로딩 내내 거의 안 들린다.
        // 그래서 지금 값이 아니라 이 곡이 도달했어야 할 볼륨으로 확정해 넘긴다.
        float volume = _catalog != null && _catalog.TryGet(_currentBgm, out BgmEntry entry)
            ? entry.Volume
            : source.volume;

        handoff = new BgmHandoff(source.clip, source.timeSamples, volume, _bgmGroup);

        // 넘긴 순간부터 화면이 걷힐 때까지는 새 씬의 매니저도 곡을 시작하지 않는다.
        _isBgmHandedOff = true;
        _hasPendingBgm = false;
        _handedOffBgm = _currentBgm;
        _handOffOwner = this;

        CancelFade();
        _isBgmPlaying = false;

        // _isBgmPlaying이 false가 되면 활성 소스도 "활성이 아닌" 것으로 판정되므로 두 소스가 모두 멈춘다.
        StopSilentBgmSources();

        return true;
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
