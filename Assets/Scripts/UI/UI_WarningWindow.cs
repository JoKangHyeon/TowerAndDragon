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

    [Tooltip("어미용 속성을 하루 1회 제한에 걸려 바꾸지 못할 때 띄우는 메시지.")]
    [SerializeField] private GameObject _messageMotherDragonChange;

    [Tooltip("용암이 흐르는 타일에 건설·이동을 시도했을 때 띄우는 메시지(얼음 새끼용 버프 반경이 필요하다는 안내).")]
    [SerializeField] private GameObject _messageVolcanoConstruction;

    [Tooltip("페이드 인/아웃 사이 완전히 보이는 시간(초).")]
    [SerializeField] private float _showDuration = DEFAULT_SHOW_DURATION;

    [Tooltip("페이드 인/아웃 연출 시간(초).")]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    private CanvasGroup _messageClaimGroup;
    private Sequence _claimSequence;

    private CanvasGroup _messageMotherDragonChangeGroup;
    private Sequence _motherDragonChangeSequence;

    private CanvasGroup _messageVolcanoConstructionGroup;
    private Sequence _volcanoConstructionSequence;

    private void Awake()
    {
        _messageClaimGroup = Prepare(_messageClaim);
        _messageMotherDragonChangeGroup = Prepare(_messageMotherDragonChange);
        _messageVolcanoConstructionGroup = Prepare(_messageVolcanoConstruction);
    }

    // 밤 점령 경고를 페이드로 잠깐 띄운다.
    public void ShowClaimWarning()
    {
        _claimSequence = Show(_messageClaim, _messageClaimGroup, _claimSequence);
    }

    // 어미용 속성 변경이 하루 1회 제한에 걸렸을 때 띄운다.
    public void ShowMotherDragonChangeWarning()
    {
        _motherDragonChangeSequence =
            Show(_messageMotherDragonChange, _messageMotherDragonChangeGroup, _motherDragonChangeSequence);
    }

    // 용암이 흐르는 타일이라 건설·이동이 막혔을 때 띄운다(얼음 새끼용의 버프 반경 안에서만 가능).
    public void ShowVolcanoConstructionWarning()
    {
        _volcanoConstructionSequence =
            Show(_messageVolcanoConstruction, _messageVolcanoConstructionGroup, _volcanoConstructionSequence);
    }

    // 메시지를 꺼진 상태로 두고 페이드용 CanvasGroup을 확보한다.
    private static CanvasGroup Prepare(GameObject message)
    {
        if (message == null)
        {
            return null;
        }

        CanvasGroup group = message.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = message.AddComponent<CanvasGroup>();
        }

        message.SetActive(false);

        return group;
    }

    // 페이드 인 → 유지 → 페이드 아웃. 새로 시작한 Sequence를 돌려주므로 호출부가 보관한다
    // (메시지마다 따로 들고 있어야 서로의 연출을 끊지 않는다).
    private Sequence Show(GameObject message, CanvasGroup group, Sequence running)
    {
        if (message == null || group == null)
        {
            return running;
        }

        // 진행 중이던 연출은 정리하고 처음부터 다시 띄운다(완료 콜백은 Kill로 호출되지 않아 조기 비활성화 없음).
        running?.Kill();

        message.SetActive(true);
        group.alpha = 0f;

        return DOTween.Sequence()
            .SetLink(message)
            // 일시정지(Time.timeScale == 0) 중에도 경고 토스트는 정상적으로 페이드 인/아웃되어야 한다.
            .SetUpdate(true)
            .Append(group.DOFade(1f, _fadeDuration))
            .AppendInterval(_showDuration)
            .Append(group.DOFade(0f, _fadeDuration))
            .OnComplete(() => message.SetActive(false));
    }
}
