using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 범용 알림 토스트. 문구를 데이터(로컬 키 + 포맷 인자)로만 받으므로 어떤 도메인(새끼용·점령 등)도 모른다.
/// 여러 알림이 짧은 시간에 겹치면 큐에 쌓아 순차적으로 페이드 인 → 유지 → 아웃한다(UI_WarningWindow와 달리 앞 메시지를 지우지 않음).
/// 포맷 인자가 들어가는 문구라 LocalizedText를 붙일 수 없으므로(팀 스트링테이블 규칙), UI_IngameWindow와 같이
/// StringTable.OnLanguageChanged를 직접 구독해 다시 그린다. 키와 인자를 들고 있다가 포맷을 다시 하므로
/// 큐에서 대기 중인 알림도 바뀐 언어로 뜬다.
/// </summary>
public class UI_NotificationToast : MonoBehaviour
{
    private const float DEFAULT_SHOW_DURATION = 2f;
    private const float DEFAULT_FADE_DURATION = 0.3f;

    [Tooltip("메시지 텍스트를 담는 루트 오브젝트. CanvasGroup이 없으면 자동으로 붙인다.")]
    [SerializeField] private GameObject _messageRoot;
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("페이드 인/아웃 사이 완전히 보이는 시간(초).")]
    [SerializeField] private float _showDuration = DEFAULT_SHOW_DURATION;

    [Tooltip("페이드 인/아웃 연출 시간(초).")]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    // 알림 하나가 뜨고 사라지기까지 걸리는 총 시간. 토스트 뒤에 무언가를 이어 붙이는 쪽이
    // 길이를 따로 베껴 두지 않고 여기서 읽어가도록 노출한다.
    public float TotalDuration => _fadeDuration + _showDuration + _fadeDuration;

    // 포맷된 결과가 아니라 원재료(키 + 인자)를 쌓는다 - 대기 중에 언어가 바뀌어도 새 언어로 뜬다.
    // 유지 시간을 메시지마다 들고 있는 이유: 같은 토스트로 나가는 알림이라도 읽는 데 걸리는 시간이 다르다.
    // 컴포넌트 값 하나로 묶으면 긴 안내에 맞춰 올린 시간이 짧은 알림까지 늘어지게 만든다.
    private readonly struct Message
    {
        public readonly string LocKey;
        public readonly object[] Args;
        public readonly float ShowDuration;

        public Message(string locKey, object[] args, float showDuration)
        {
            LocKey = locKey;
            Args = args;
            ShowDuration = showDuration;
        }

        public string Resolve()
        {
            string raw = StringTable.GetString(LocKey);
            return Args == null || Args.Length == 0 ? raw : string.Format(raw, Args);
        }
    }

    private CanvasGroup _messageGroup;
    private Sequence _sequence;
    private readonly Queue<Message> _pendingMessages = new();
    private Message _currentMessage;
    private bool _isShowing;

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += RefreshCurrentMessage;
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= RefreshCurrentMessage;
    }

    private void RefreshCurrentMessage()
    {
        if (_isShowing && _messageText != null)
        {
            _messageText.text = _currentMessage.Resolve();
        }
    }

    private void Awake()
    {
        if (_messageRoot == null)
        {
            return;
        }

        _messageGroup = _messageRoot.GetComponent<CanvasGroup>();
        if (_messageGroup == null)
        {
            _messageGroup = _messageRoot.AddComponent<CanvasGroup>();
        }

        _messageRoot.SetActive(false);
    }

    // 문구는 호출부가 로컬 키로 넘긴다 - 이 컴포넌트는 문자열 리터럴을 갖지 않는다.
    public void Show(string locKey, params object[] args) =>
        ShowFor(_showDuration, locKey, args);

    /// <summary>
    /// 유지 시간을 그 알림만 따로 정해 띄운다. 한 번 읽고 외워야 하는 조작 안내처럼 기본값으로는
    /// 너무 빨리 지나가는 문구에 쓴다 - 컴포넌트의 <see cref="_showDuration"/>을 올리면
    /// 같은 토스트로 나가는 다른 알림까지 함께 늘어진다.
    /// </summary>
    public void ShowFor(float showSeconds, string locKey, params object[] args)
    {
        if (_messageRoot == null || _messageText == null)
        {
            return;
        }

        _pendingMessages.Enqueue(new Message(locKey, args, showSeconds));

        if (!_isShowing)
        {
            PlayNext();
        }
    }

    private void PlayNext()
    {
        if (_pendingMessages.Count == 0)
        {
            _isShowing = false;
            return;
        }

        _isShowing = true;
        _currentMessage = _pendingMessages.Dequeue();
        _messageText.text = _currentMessage.Resolve();

        _sequence?.Kill();
        _messageRoot.SetActive(true);
        _messageGroup.alpha = 0f;

        _sequence = DOTween.Sequence()
            .SetLink(_messageRoot)
            // 일시정지(Time.timeScale == 0) 중에도 알림 토스트는 정상적으로 페이드 인/아웃되어야 한다.
            .SetUpdate(true)
            .Append(_messageGroup.DOFade(1f, _fadeDuration))
            .AppendInterval(_currentMessage.ShowDuration)
            .Append(_messageGroup.DOFade(0f, _fadeDuration))
            .OnComplete(() =>
            {
                _messageRoot.SetActive(false);
                PlayNext();
            });
    }
}
