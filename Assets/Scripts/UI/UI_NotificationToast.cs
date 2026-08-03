using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 범용 알림 토스트. 문구를 데이터(로컬 키 + 포맷 인자)로만 받으므로 어떤 도메인(새끼용·점령 등)도 모른다.
/// 여러 알림이 짧은 시간에 겹치면 큐에 쌓아 순차적으로 페이드 인 → 유지 → 아웃한다(UI_WarningWindow와 달리 앞 메시지를 지우지 않음).
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

    private CanvasGroup _messageGroup;
    private Sequence _sequence;
    private readonly Queue<string> _pendingMessages = new();
    private bool _isShowing;

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
    public void Show(string locKey, params object[] args)
    {
        if (_messageRoot == null || _messageText == null)
        {
            return;
        }

        _pendingMessages.Enqueue(string.Format(StringTable.GetString(locKey), args));

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
        _messageText.text = _pendingMessages.Dequeue();

        _sequence?.Kill();
        _messageRoot.SetActive(true);
        _messageGroup.alpha = 0f;

        _sequence = DOTween.Sequence()
            .SetLink(_messageRoot)
            // 일시정지(Time.timeScale == 0) 중에도 알림 토스트는 정상적으로 페이드 인/아웃되어야 한다.
            .SetUpdate(true)
            .Append(_messageGroup.DOFade(1f, _fadeDuration))
            .AppendInterval(_showDuration)
            .Append(_messageGroup.DOFade(0f, _fadeDuration))
            .OnComplete(() =>
            {
                _messageRoot.SetActive(false);
                PlayNext();
            });
    }
}
