using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 컷씬 표시 전담. "지금 어느 컷인지"는 모르고, 받은 컷 하나를 그리기만 한다.
///
/// 페이드를 DOTween이 아니라 직접 도는 이유는 시간 때문이다 - 이 화면은 게임오버 직후,
/// GameSpeedManager가 Time.timeScale을 0으로 굳혀 놓은 상태에서 재생된다.
/// 그래서 모든 시간 계산은 unscaled여야 한다.
/// </summary>
public sealed class UI_TutorialEndingPanel : MonoBehaviour
{
    private const float DEFAULT_FADE_DURATION = 0.5f;
    private const float FULL_TURN_DEGREES = 360f;

    [Tooltip("컷씬 화면 전체. 숨길 때 통째로 비활성화하므로 이 컴포넌트가 붙은 오브젝트 자신이면 안 되고 자식이어야 한다.")]
    [SerializeField] private GameObject _root;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("컷의 일러스트를 띄울 자리. 스프라이트가 없는 컷에서는 통째로 숨긴다.")]
    [SerializeField] private Image _illustration;

    [Tooltip("컷의 연출 프리팹이 생성될 자리. 비우면 일러스트 자리에 붙는다. " +
             "일러스트 뒤에 깔지 앞에 띄울지는 이 자리를 계층 어디에 두느냐로 정한다.")]
    [SerializeField] private RectTransform _effectRoot;

    [Tooltip("다음 컷으로 넘기는 버튼. 자동으로 넘어가는 컷에서는 숨는다.")]
    [SerializeField] private Button _nextButton;

    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    /// <summary>다음 버튼이 눌렸다. 순서 진행은 시퀀서가 판단한다.</summary>
    public event Action NextClicked;

    /// <summary>확인 대기 컷을 넘길 버튼이 실제로 있는지. 없으면 기다려도 깨울 방법이 없다.</summary>
    public bool HasNextButton => _nextButton != null;

    // 포맷 인자가 없어도 언어가 바뀌면 다시 그려야 하므로 현재 키를 들고 있는다(UI_GuideOverlay와 같은 방식).
    private string _currentLocKey;

    // 무한 반복 트윈이라 컷이 바뀌거나 오브젝트가 사라질 때 반드시 죽여야 한다.
    private Tween _spinTween;

    // 지금 컷이 띄운 연출들. 컷이 바뀔 때마다 지우고 새로 만든다 - 풀링하지 않는 이유는
    // 컷마다 다른 프리팹이고 엔딩은 한 번만 재생되기 때문이다.
    private readonly List<GameObject> _activeEffects = new();

    private void Awake()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }

        if (_nextButton != null)
        {
            _nextButton.onClick.AddListener(HandleNextClicked);
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += ApplyText;
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= ApplyText;
    }

    private void OnDestroy()
    {
        _spinTween?.Kill();
        _spinTween = null;
    }

    /// <summary>
    /// [테스트 전용] 컷 하나를 그려 본다. 게임오버를 기다리지 않고 연출을 확인하는 용도다.
    /// 재생 중에만 의미가 있다 - 에디터 정지 상태에서는 프리팹 인스턴스가 씬에 남는다.
    /// </summary>
    public void DebugPreviewBeat(TutorialEndingBeatSO beat)
    {
        if (_root != null)
        {
            _root.SetActive(true);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        ShowBeat(beat);
    }

    private void HandleNextClicked() => NextClicked?.Invoke();

    public async UniTask ShowAsync(CancellationToken cancellationToken)
    {
        if (_root == null)
        {
            return;
        }

        _root.SetActive(true);
        await FadeAsync(0f, 1f, cancellationToken);
    }

    public async UniTask HideAsync(CancellationToken cancellationToken)
    {
        if (_root == null)
        {
            return;
        }

        await FadeAsync(1f, 0f, cancellationToken);

        // 무한 반복이라 화면을 감춰도 계속 돈다 - 여기서 끊지 않으면 컷씬이 끝난 뒤에도 남는다.
        _spinTween?.Kill();
        _spinTween = null;
        ClearEffects();

        _root.SetActive(false);
    }

    public void ShowBeat(TutorialEndingBeatSO beat)
    {
        if (beat == null)
        {
            return;
        }

        _currentLocKey = beat.MessageLocKey;
        ApplyText();

        // 아트가 아직 없는 컷은 빈 사각형이 뜨지 않도록 자리를 통째로 접는다.
        if (_illustration != null)
        {
            _illustration.sprite = beat.Illustration;
            _illustration.gameObject.SetActive(beat.Illustration != null);
            ApplySpin(beat);
        }

        ApplyEffect(beat);

        if (_nextButton != null)
        {
            _nextButton.gameObject.SetActive(beat.WaitForConfirm);
        }
    }

    /// <summary>
    /// 컷의 연출 프리팹을 모두 띄운다. 이 패널은 무엇이 재생되는지 알지 않는다 - 파티클이든 애니메이터든
    /// 프리팹이 스스로 재생하고, 여기서는 생성과 정리만 맡는다.
    /// 리스트 순서대로 붙이므로 뒤에 있는 것이 위에 그려진다.
    /// </summary>
    private void ApplyEffect(TutorialEndingBeatSO beat)
    {
        ClearEffects();

        IReadOnlyList<GameObject> prefabs = beat.EffectPrefabs;

        if (prefabs == null)
        {
            return;
        }

        Transform parent = _effectRoot != null
            ? _effectRoot
            : (_illustration != null ? _illustration.transform : transform);

        foreach (GameObject prefab in prefabs)
        {
            // 목록 중간에 빈 칸이 있어도 나머지는 떠야 한다 - 인스펙터에서 자리만 늘려둔 경우다.
            if (prefab == null)
            {
                continue;
            }

            _activeEffects.Add(Instantiate(prefab, parent, false));
        }
    }

    private void ClearEffects()
    {
        foreach (GameObject effect in _activeEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        _activeEffects.Clear();
    }

    /// <summary>
    /// 일러스트를 계속 돌린다. 컷마다 각도를 0으로 되돌리는 이유는, 앞 컷이 돌려 놓은 채로 끝나면
    /// 다음 컷의 그림이 기울어진 채 나타나기 때문이다.
    ///
    /// timeScale이 0으로 굳은 화면이므로 SetUpdate(true)로 스케일을 무시해야 한다 - 페이드가
    /// Time.unscaledDeltaTime을 쓰는 것과 같은 이유다. 빼먹으면 트윈이 아예 돌지 않는다.
    /// </summary>
    private void ApplySpin(TutorialEndingBeatSO beat)
    {
        _spinTween?.Kill();
        _spinTween = null;

        _illustration.transform.localRotation = Quaternion.identity;

        float spinSeconds = beat.IllustrationSpinSeconds;

        if (Mathf.Approximately(spinSeconds, 0f) || beat.Illustration == null)
        {
            return;
        }

        // 음수는 반대 방향으로 같은 속도로 돈다 - 되감기 연출을 부호 하나로 표현한다.
        float direction = Mathf.Sign(spinSeconds);
        float duration = Mathf.Abs(spinSeconds);

        _spinTween = _illustration.transform
            .DORotate(new Vector3(0f, 0f, -FULL_TURN_DEGREES * direction), duration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    private void ApplyText()
    {
        if (_messageText == null)
        {
            return;
        }

        // 그림만 보여주는 컷은 문구가 없다 - 비우지 않으면 앞 컷의 대사가 그림 위에 남는다.
        _messageText.text = string.IsNullOrWhiteSpace(_currentLocKey)
            ? string.Empty
            : StringTable.GetString(_currentLocKey);
    }

    private async UniTask FadeAsync(float from, float to, CancellationToken cancellationToken)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = from;

        if (_fadeDuration <= 0f)
        {
            _canvasGroup.alpha = to;
            return;
        }

        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            await UniTask.Yield(cancellationToken);
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
        }

        _canvasGroup.alpha = to;
    }
}
