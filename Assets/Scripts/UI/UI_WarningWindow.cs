using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 경고 메시지 창. 상황별 메시지(Message_*)를 페이드 인 → 잠깐 유지 → 페이드 아웃으로 띄운다(토스트).
/// 각 메시지 오브젝트는 시작 시 꺼두고, 요청된 종류만 켠다.
///
/// 메시지를 늘릴 때는 MessageId에 값을 추가하고 인스펙터 목록에 한 줄만 배정하면 된다.
/// </summary>
public class UI_WarningWindow : MonoBehaviour
{
    private const float DEFAULT_SHOW_DURATION = 2f;
    private const float DEFAULT_FADE_DURATION = 0.3f;

    /// <summary>경고 메시지의 종류. 인스펙터 목록의 드롭다운이자 호출부가 메시지를 지목하는 이름이다.
    /// 값의 선언 순서를 바꾸면 이미 배정해 둔 목록의 종류가 밀린다(직렬화는 순서대로 정수로 저장된다) -
    /// 새 값은 끝에 추가한다.</summary>
    public enum MessageId
    {
        // 밤에 점령을 시도했을 때.
        Claim,

        // 인구를 배치할 수 있는 건물이 하나도 없는데 인구 배치 모드를 켰을 때.
        WorkerMode,

        // 용암이 흐르는 타일에 건설·이동을 시도했을 때(얼음 새끼용 버프 반경이 필요하다는 안내).
        VolcanoConstruction,

        // 건설 비용을 낼 자원이 모자라 건설창에서 건물을 고르지 못했을 때.
        NotEnoughResources,

        // 이미 지어 둔 연구소가 있는데 두 번째 연구소를 놓으려 했을 때.
        ResearchLabDuplicate,

        // 밤에 건설 모드를 켜려 했을 때.
        Build,

        // 땅은 멀쩡한데 요구 자원 노드가 없어서 생산시설을 놓지 못했을 때.
        // 초원처럼 지을 수 있어 보이는 땅에서 나므로 다른 사유와 반드시 구분해야 한다.
        ResourceNodeRequired,

        // 절벽·물처럼 어떤 새끼용으로도 풀 수 없는 지형에 건설·이동을 시도했을 때.
        // (용암은 얼음 새끼용으로 풀 수 있어 VolcanoConstruction으로 따로 안내한다.)
        TerrainNotConstructible,

        // 이미 다른 건물이 차지한 칸에 건설·이동을 시도했을 때.
        CellOccupied,

        // 아직 점령하지 않은 청크에 건설·이동을 시도했을 때.
        ChunkNotConquered,

        // 봉인석을 포탈 봉인 영역 밖에 놓으려 했거나, 그 포탈에 이미 봉인석이 있을 때.
        SealSiteRequired,
    }

    /// <summary>인스펙터 한 줄 = 메시지 하나. 종류를 함께 지정하므로 목록 순서는 상관없다.</summary>
    [Serializable]
    private struct WarningMessage
    {
        public MessageId Id;
        public GameObject Root;
    }

    [Tooltip("경고 메시지 목록. 종류 하나당 한 줄씩 배정한다 - 순서는 상관없다.")]
    [SerializeField] private List<WarningMessage> _messages = new();

    [Tooltip("페이드 인/아웃 사이 완전히 보이는 시간(초).")]
    [SerializeField] private float _showDuration = DEFAULT_SHOW_DURATION;

    [Tooltip("페이드 인/아웃 연출 시간(초).")]
    [SerializeField] private float _fadeDuration = DEFAULT_FADE_DURATION;

    // 메시지 하나의 재생 상태. 진행 중인 Sequence를 메시지마다 따로 들고 있어야
    // 한 메시지를 다시 띄울 때 다른 메시지의 페이드를 끊지 않는다.
    private sealed class MessageView
    {
        public GameObject Root;
        public CanvasGroup Group;
        public Sequence Sequence;
    }

    private readonly Dictionary<MessageId, MessageView> _views = new();

    private void Awake()
    {
        foreach (WarningMessage message in _messages)
        {
            if (message.Root == null)
            {
                continue;
            }

            if (_views.ContainsKey(message.Id))
            {
                Debug.LogError(
                    $"[{nameof(UI_WarningWindow)}] {message.Id} 메시지가 목록에 두 번 있습니다 - 첫 줄만 사용합니다.",
                    this);
                continue;
            }

            _views.Add(message.Id, new MessageView
            {
                Root = message.Root,
                Group = Prepare(message.Root),
            });
        }
    }

    /// <summary>해당 종류의 경고를 잠깐 띄운다.</summary>
    public void Show(MessageId id)
    {
        if (!_views.TryGetValue(id, out MessageView view))
        {
            Debug.LogError(
                $"[{nameof(UI_WarningWindow)}] {id} 메시지가 인스펙터 목록에 배정되지 않았습니다.",
                this);
            return;
        }

        view.Sequence = PlayFade(view.Root, view.Group, view.Sequence);
    }

    // 메시지를 꺼진 상태로 두고 페이드용 CanvasGroup을 확보한다.
    private static CanvasGroup Prepare(GameObject message)
    {
        CanvasGroup group = message.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = message.AddComponent<CanvasGroup>();
        }

        message.SetActive(false);

        return group;
    }

    // 페이드 인 → 유지 → 페이드 아웃. 새로 시작한 Sequence를 돌려주므로 호출부가 보관한다.
    private Sequence PlayFade(GameObject message, CanvasGroup group, Sequence running)
    {
        if (group == null)
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
