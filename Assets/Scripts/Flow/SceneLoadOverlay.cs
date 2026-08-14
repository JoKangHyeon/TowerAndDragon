using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 동안 화면을 덮는 로딩 화면.
///
/// 측정 결과(콜드, 에디터): 비동기 로드 1486ms(2프레임) / 활성화 1271ms(13프레임) /
/// Start+첫 프레임 1085ms = 총 3842ms에 프레임 16개, 즉 로딩 중에는 <b>4fps 남짓</b>으로만 그려진다.
/// 그래서 진행바는 두지 않는다 - progress가 오를 시간이 없어 값이 튀기만 한다. 스피너는
/// <see cref="UI_FrameSteppedSpinner"/>가 시간이 아니라 렌더된 프레임을 기준으로 돌려서, 느리더라도
/// 순서가 유지되게 만든다(시간 기준으로 돌리면 위상이 건너뛰어 무작위 깜빡임이 된다).
///
/// 체감이 나아지는 이유는 비동기 로드가 아니라 이 오브젝트가 <see cref="Object.DontDestroyOnLoad"/>로
/// 씬 교체를 넘어 살아남는 것이다. 로딩이 블록되는 동안에는 마지막으로 그려진 프레임이 화면에 남으므로,
/// 그 프레임이 불투명한 로딩 화면이기만 하면 3.8초가 "멈춤"이 아니라 "로딩 중"으로 읽힌다.
///
/// <b>배치 규칙</b>
///  - 씬의 <b>루트</b> 오브젝트여야 한다. DontDestroyOnLoad는 루트에만 걸린다.
///  - 자체 Canvas를 <c>ScreenSpaceOverlay</c>로 둔다. 씬 교체로 원래 카메라가 사라지므로
///    카메라가 필요한 렌더 모드는 로딩 도중 화면이 비어 버린다.
///  - sortingOrder는 새 씬의 UI보다 높게. 새 씬이 뜬 뒤에도 페이드 아웃이 끝날 때까지 위에 있어야 한다.
///  - EventSystem을 포함하지 않는다. 새 씬의 것과 둘이 되면 입력이 어긋난다.
///
/// SetActive로 여닫지 않고 CanvasGroup 알파로만 감춘다 - 비활성으로 저장된 오브젝트의 Awake가
/// 첫 SetActive(true) 안에서 도는 함정(CLAUDE.md의 _isOpen 가드)을 아예 만들지 않기 위해서다.
/// </summary>
public sealed class SceneLoadOverlay : MonoBehaviour
{
    private const string LOADING_MESSAGE_LOC_KEY = "loading_message";

    private const float DEFAULT_FADE_IN_DURATION = 0.2f;
    private const float DEFAULT_FADE_OUT_DURATION = 0.3f;

    // 페이드가 끝난 불투명한 화면이 실제로 한 번 그려지게 한다. 이걸 빼면 로드 블록이
    // 페이드 중간 프레임에서 시작해 반투명한 화면이 몇 초간 얼어붙는다.
    private const int FRAMES_BEFORE_LOAD = 1;

    // 새 씬의 Start와 첫 렌더가 끝난 뒤에 걷는다. 바로 걷으면 아직 아무것도 그려지지 않은 씬이 보인다.
    private const int FRAMES_AFTER_LOAD = 2;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;

    private const float SILENT_VOLUME = 0f;

    [SerializeField] private CanvasGroup _canvasGroup;

    [Tooltip("로딩 문구. key는 코드에서 넣으므로 인스펙터에서 비워 둬도 된다.")]
    [WiringOptional]
    [SerializeField] private LocalizedText _messageLabel;

    [SerializeField] private float _fadeInDuration = DEFAULT_FADE_IN_DURATION;
    [SerializeField] private float _fadeOutDuration = DEFAULT_FADE_OUT_DURATION;

    [Tooltip("화면이 불투명해진 뒤 최소 몇 초를 띄워 둘지. 로딩이 그보다 빨리 끝나면 남은 시간을 더 기다린다. " +
             "0이면 제한 없이 로딩이 끝나는 즉시 걷는다.\n" +
             "로딩 구간에는 프레임이 거의 없어 연출이 끊기므로, 씬이 이미 준비된 뒤의 매끄러운 구간을 " +
             "확보하려면 이 값을 올린다. 그만큼 대기가 길어진다.")]
    [Min(0f)]
    [SerializeField] private float _minimumDisplaySeconds;

    private bool _isLoading;

    // 씬의 SoundManager에게서 넘겨받은 BGM을 로딩 동안 이어 트는 소스.
    // 인스펙터에 직렬화하지 않고 여기서 붙이는 이유는 프리팹을 건드리지 않기 위해서다
    // (SoundManager.CreateSources와 같은 방식).
    private AudioSource _handoffSource;

    // 이 인스턴스가 BGM을 넘겨받았는지. 한 씬에 오버레이가 여럿 있을 수 있으므로(로딩 화면 연출이
    // 경로마다 다른 경우) 반드시 인스턴스별로 들고 있어야 한다 - 이게 없으면 인계와 무관한 오버레이가
    // 씬과 함께 파괴되면서 남의 소유권을 풀어, 아직 로딩 화면이 떠 있는데 새 씬 BGM이 시작된다.
    private bool _hasHandoff;

    private void Awake()
    {
        if (_messageLabel != null)
        {
            _messageLabel.SetKey(LOADING_MESSAGE_LOC_KEY);
        }

        // 클립이 로딩보다 짧아도 끊기지 않도록 루프로 둔다 - 콜드 로딩은 3.8초까지 걸린다(위 측정치).
        _handoffSource = gameObject.AddComponent<AudioSource>();
        _handoffSource.playOnAwake = false;
        _handoffSource.loop = true;
        _handoffSource.spatialBlend = 0f;

        // 참조를 잃으면 Hide()가 돌지 않아, 불투명하고 클릭까지 먹는 로딩 화면에 덮인 채 타이틀이 뜬다.
        // 같은 오브젝트에서 한 번 더 찾아 그 상태를 피한다 - 아래 WiringGuard는 CanvasGroup이
        // 아예 없을 때만 걸린다.
        if (_canvasGroup == null)
        {
            TryGetComponent(out _canvasGroup);
        }

        if (!WiringGuard.Require(_canvasGroup, nameof(_canvasGroup), this))
        {
            return;
        }

        Hide();
    }

    /// <summary>
    /// 로딩 화면을 띄우고 씬을 연다. 씬 로드는 되돌릴 수 없으므로 두 번 불려도 한 번만 나간다.
    /// 완료를 기다릴 곳이 없으면 <c>.Forget()</c>으로 부른다.
    /// </summary>
    public async UniTask LoadAsync(string sceneName)
    {
        if (_isLoading)
        {
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoadOverlay] 열 씬 이름이 비어 있습니다.", this);
            return;
        }

        _isLoading = true;

        // 배선이 빠졌다고 타이틀에 갇히면 안 된다. 로딩 화면 없이라도 씬은 열어 준다
        // (원인은 Awake의 WiringGuard가 이미 콘솔에 남겼다).
        if (_canvasGroup == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        CancellationToken token = this.GetCancellationTokenOnDestroy();

        // 페이드가 도는 동안 타이틀 버튼이 다시 눌리지 않게 먼저 막는다.
        _canvasGroup.blocksRaycasts = true;

        // 어디서 실패해도 불투명한 화면을 걷어야 한다. 안 걷으면 클릭까지 먹는 검은 화면만 남아
        // 타이틀로 돌아갈 길이 없다.
        bool hasCompleted = false;

        try
        {
            await FadeAsync(VISIBLE_ALPHA, _fadeInDuration, token);
            await UniTask.DelayFrame(FRAMES_BEFORE_LOAD, cancellationToken: token);

            // 최소 표시 시간은 여기서부터 센다 - 페이드 인은 아직 화면이 비쳐 보이는 구간이라
            // "로딩 화면을 보여준 시간"으로 치면 실제로 덮여 있는 시간이 그만큼 짧아진다.
            // 로딩이 블록되는 동안 프레임이 멈춰도 unscaledTime은 실제 경과만큼 뛰므로 그대로 반영된다.
            float opaqueSinceUnscaledTime = Time.unscaledTime;

            // 씬이 내려가면 SoundManager도 함께 사라져 흐르던 곡이 뚝 끊기고, 로딩이 끝날 때까지 무음이 된다.
            // 이 오브젝트는 씬 경계를 넘어 살아남으므로 곡을 넘겨받아 끊긴 자리에서 이어 튼다.
            // 로드가 시작되기 전에 넘겨받아야 한다 - 씬이 내려간 뒤에는 넘겨줄 SoundManager가 없다.
            BeginHandoffAudio();

            // 씬이 Build Settings에 없거나 빌드에서 항목이 비활성이면 null이 돌아온다.
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                Debug.LogError(
                    $"[SceneLoadOverlay] '{sceneName}' 씬을 열 수 없습니다. Build Settings 등록을 확인하세요.", this);
                return;
            }

            // 로드가 시작된 뒤에 건다 - 실패했는데 걸어 두면 오버레이만 DontDestroyOnLoad 씬에 남는다.
            DontDestroyOnLoad(gameObject);

            await operation.ToUniTask(cancellationToken: token);
            await UniTask.DelayFrame(FRAMES_AFTER_LOAD, cancellationToken: token);
            await WaitForMinimumDisplayAsync(opaqueSinceUnscaledTime, token);
            await FadeAsync(HIDDEN_ALPHA, _fadeOutDuration, token);

            // 화면이 다 걷힌 지금부터 새 씬의 곡이 올라온다. 이 호출을 앞당기면 시계가 도는 동안
            // 새 씬의 BGM이 뒤에서 시작해 넘겨받은 곡과 겹친다.
            ReleaseHandoff();

            hasCompleted = true;
        }
        finally
        {
            // 취소는 이 오브젝트가 파괴됐다는 뜻이므로 아무것도 만지지 않는다.
            if (this != null)
            {
                if (hasCompleted)
                {
                    // blocksRaycasts는 여기까지 켜 둔 채로 끝낸다 - 반쯤 투명한 새 씬에 클릭이 떨어지면
                    // 플레이어가 누른 적 없는 버튼이 눌린다. 파괴가 곧 해제다.
                    Destroy(gameObject);
                }
                else
                {
                    // 실패했으면 화면을 되돌리고 다시 시도할 수 있게 남겨 둔다.
                    Hide();
                    _isLoading = false;
                }
            }
        }
    }

    // 씬이 이미 준비된 뒤에도 잠시 더 덮어 둔다. 이 구간은 프레임이 정상이라 시계·스피너가 매끄럽게 돌고,
    // 마지막 인상이 끊긴 화면이 아니게 된다.
    private async UniTask WaitForMinimumDisplayAsync(float opaqueSinceUnscaledTime, CancellationToken token)
    {
        float remainingSeconds = _minimumDisplaySeconds - (Time.unscaledTime - opaqueSinceUnscaledTime);

        if (remainingSeconds <= 0f)
        {
            return;
        }

        await UniTask.WaitForSeconds(remainingSeconds, ignoreTimeScale: true, cancellationToken: token);
    }

    private void Hide()
    {
        _canvasGroup.alpha = HIDDEN_ALPHA;
        _canvasGroup.blocksRaycasts = false;

        // 로드가 실패해 화면을 되돌리는 경우에도 여기를 지난다 - 멈추지 않으면 원래 씬이 그대로 보이는데
        // 넘겨받은 곡만 계속 흐른다(원래 재생하던 SoundManager는 이미 멈춘 뒤다).
        StopHandoffAudio();
    }

    // 씬의 SoundManager가 흐르던 곡과 재생 위치를 넘겨준다. 넘겨줄 곡이 없으면(무음 상태이거나
    // SoundManager가 없는 씬이면) 아무 일도 하지 않는다.
    private void BeginHandoffAudio()
    {
        if (!SoundManager.TryHandOffBgm(out BgmHandoff handoff))
        {
            return;
        }

        _hasHandoff = true;
        _handoffSource.outputAudioMixerGroup = handoff.OutputGroup;
        _handoffSource.clip = handoff.Clip;
        _handoffSource.volume = handoff.Volume;
        _handoffSource.timeSamples = handoff.TimeSamples;
        _handoffSource.Play();
    }

    private void StopHandoffAudio()
    {
        _handoffSource.Stop();
        _handoffSource.clip = null;
        ReleaseHandoff();
    }

    // 넘겨받은 곡의 소유권을 돌려준다. 안 돌려주면 새 씬의 매니저가 시작을 계속 미뤄 그 씬이 무음으로 남고,
    // 넘겨받은 적도 없는데 돌려주면 아직 로딩 화면이 떠 있는 다른 전환의 소유권을 빼앗는다.
    private void ReleaseHandoff()
    {
        if (!_hasHandoff)
        {
            return;
        }

        _hasHandoff = false;
        SoundManager.ResumeBgmAfterHandoff();
    }

    // 취소 등으로 정상 경로를 밟지 못하고 사라져도 소유권은 반드시 돌아가야 한다.
    // 정상 종료라면 이미 돌려준 뒤라 여기서는 아무 일도 일어나지 않는다.
    private void OnDestroy()
    {
        ReleaseHandoff();
    }

    // DOTween 대신 직접 보간한다 - 이 페이드는 씬 교체를 넘어 이어지고, 트윈은 SetLink한 오브젝트와
    // 생명주기가 얽혀 그 경계에서 다루기가 번거롭다.
    // unscaledDeltaTime을 쓰는 이유: timeScale이 0인 상태(GameSpeedManager의 정지)에서도 페이드는 돌아야 한다.
    private async UniTask FadeAsync(float targetAlpha, float duration, CancellationToken token)
    {
        float startAlpha = _canvasGroup.alpha;

        // 넘겨받은 곡은 화면과 함께 걷힌다. 새 씬의 곡은 이 페이드가 끝난 뒤에야 시작하므로
        // (ReleaseHandoff가 그때 소유권을 돌려준다) 둘이 교차하지 않고 사이가 잠깐 빈다 -
        // 화면이 아직 덮여 있는 동안 새 곡이 올라와 두 곡이 겹쳐 들리는 쪽을 피한 결과다.
        // 페이드 인 구간에서는 아직 넘겨받기 전이라 볼륨이 0이고, 이 계산도 그 값을 그대로 유지한다.
        float startVolume = _handoffSource.volume;
        float targetVolume = Mathf.Approximately(targetAlpha, HIDDEN_ALPHA) ? SILENT_VOLUME : startVolume;

        if (duration <= 0f)
        {
            _canvasGroup.alpha = targetAlpha;
            _handoffSource.volume = targetVolume;
            return;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            _handoffSource.volume = Mathf.Lerp(startVolume, targetVolume, progress);
            await UniTask.Yield(token);
        }

        _canvasGroup.alpha = targetAlpha;
        _handoffSource.volume = targetVolume;
    }
}
