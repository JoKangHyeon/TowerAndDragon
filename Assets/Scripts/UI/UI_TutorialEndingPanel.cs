using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    [Tooltip("컷씬 화면 전체. 숨길 때 통째로 비활성화하므로 이 컴포넌트가 붙은 오브젝트 자신이면 안 되고 자식이어야 한다.")]
    [SerializeField] private GameObject _root;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("컷의 일러스트를 띄울 자리. 스프라이트가 없는 컷에서는 통째로 숨긴다.")]
    [SerializeField] private Image _illustration;

    [Tooltip("다음 컷으로 넘기는 버튼. 자동으로 넘어가는 컷에서는 숨는다.")]
    [SerializeField] private Button _nextButton;

    [Min(0f)]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    /// <summary>다음 버튼이 눌렸다. 순서 진행은 시퀀서가 판단한다.</summary>
    public event Action NextClicked;

    // 포맷 인자가 없어도 언어가 바뀌면 다시 그려야 하므로 현재 키를 들고 있는다(UI_GuideOverlay와 같은 방식).
    private string _currentLocKey;

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
        }

        if (_nextButton != null)
        {
            _nextButton.gameObject.SetActive(beat.WaitForConfirm);
        }
    }

    private void ApplyText()
    {
        if (_messageText == null || string.IsNullOrWhiteSpace(_currentLocKey))
        {
            return;
        }

        _messageText.text = StringTable.GetString(_currentLocKey);
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
