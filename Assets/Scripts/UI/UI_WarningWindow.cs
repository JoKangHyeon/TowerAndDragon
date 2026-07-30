using DG.Tweening;
using UnityEngine;

/// <summary>
/// 경고 메시지 창. 상황별 메시지(Message_*)를 페이드 인 → 잠깐 유지 → 페이드 아웃으로 띄운다(토스트).
/// 각 메시지 오브젝트는 시작 시 꺼두고, 요청 시 해당 메시지만 켠다.
/// </summary>
public class UI_WarningWindow : MonoBehaviour
{
    private const float DEFAULT_SHOW_DURATION = 2f;
    private const float DEFAULT_FADE_DURATION = 0.3f;

    [Tooltip("밤에 점령을 시도했을 때 띄우는 메시지.")]
    [SerializeField] private GameObject _messageClaim;

    [Tooltip("페이드 인/아웃 사이 완전히 보이는 시간(초).")]
    [SerializeField] private float _showDuration = DEFAULT_SHOW_DURATION;

    [Tooltip("페이드 인/아웃 연출 시간(초).")]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    private CanvasGroup _messageClaimGroup;
    private Sequence _claimSequence;

    private void Awake()
    {
        if (_messageClaim != null)
        {
            _messageClaimGroup = _messageClaim.GetComponent<CanvasGroup>();
            if (_messageClaimGroup == null)
            {
                _messageClaimGroup = _messageClaim.AddComponent<CanvasGroup>();
            }

            _messageClaim.SetActive(false);
        }
    }

    // 밤 점령 경고를 페이드로 잠깐 띄운다.
    public void ShowClaimWarning()
    {
        if (_messageClaim == null)
        {
            return;
        }

        // 진행 중이던 연출은 정리하고 처음부터 다시 띄운다(완료 콜백은 Kill로 호출되지 않아 조기 비활성화 없음).
        _claimSequence?.Kill();

        _messageClaim.SetActive(true);
        _messageClaimGroup.alpha = 0f;

        _claimSequence = DOTween.Sequence()
            .SetLink(_messageClaim)
            // 일시정지(Time.timeScale == 0) 중에도 경고 토스트는 정상적으로 페이드 인/아웃되어야 한다.
            .SetUpdate(true)
            .Append(_messageClaimGroup.DOFade(1f, _fadeDuration))
            .AppendInterval(_showDuration)
            .Append(_messageClaimGroup.DOFade(0f, _fadeDuration))
            .OnComplete(() => _messageClaim.SetActive(false));
    }
}
