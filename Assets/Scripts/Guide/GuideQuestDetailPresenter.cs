using UnityEngine;

/// <summary>
/// 목록에서 퀘스트 한 줄을 누르면 그 설명을 띄운다.
///
/// <b>왜 눌러서 여는가.</b> 원래 계획은 퀘스트가 열릴 때마다 카드를 미는 것이었는데,
/// 1일차 항목만 아홉 개라 그러면 확인 버튼을 아홉 번 누르는 튜토리얼이 된다
/// (카드는 한 번에 두 장까지만 보이므로 나머지는 줄까지 선다).
/// 가이드 퀘스트는 튜토리얼을 마친 플레이어를 상대하므로, 설명은 <b>필요할 때 꺼내 보는 것</b>으로 둔다.
/// 목록 한 줄이 곧 "무엇을"이고, 설명이 "왜·언제까지"다.
///
/// 보여주는 방법은 둘이고, 여기서 갈린다.
/// <b>말풍선</b> - 가리킬 UI가 있고 가이드 수준이 "전체"일 때. 설명과 함께 눌러야 할 버튼을 짚어 준다.
/// <b>카드</b> - 그 외 전부(가리킬 대상이 없거나, 수준이 "Tip만"이거나, 다른 안내가 화면을 쥐고 있을 때).
///
/// 그래서 "전체"와 "Tip만"의 차이는 <b>화면을 덮는 강조가 붙느냐</b>뿐이다.
/// 설명 자체를 깎지는 않는다 - 플레이어가 직접 요청한 것이라 짧게 답하는 것은 친절이 아니다.
/// </summary>
public sealed class GuideQuestDetailPresenter : MonoBehaviour, IGuideRequestProvider
{
    [Tooltip("줄 클릭을 알려 주는 목록 창.")]
    [SerializeField] private UI_GuideQuestWindow _window;

    [Tooltip("설명을 띄울 우상단 카드.")]
    [SerializeField] private UI_ConfirmNotificationToast _toast;

    [Tooltip("가리킬 UI가 있을 때 쓸 말풍선. 없으면 항상 카드로 띄운다.")]
    [WiringOptional]
    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("설명에 지금 값을 채워 넣는다. 없으면 문안 그대로 띄운다.")]
    [WiringOptional]
    [SerializeField] private GuideQuestBodyComposer _bodyComposer;

    [Tooltip("가이드 수준을 읽는다. 없으면 전체로 친다.")]
    [WiringOptional]
    [SerializeField] private SettingsService _settingsService;

    // 지금 카드로 떠 있는 퀘스트. 같은 줄을 연타해도 카드가 줄줄이 쌓이지 않게 막는다.
    private GuideQuestSO _cardOnScreen;

    // 지금 말풍선으로 내고 있는 퀘스트. 확인을 누를 때까지 매 프레임 같은 요청을 낸다.
    private GuideQuestSO _bubbleQuest;

    // 설명문에 박은 숫자는 꺼낸 순간의 값으로 굳힌다. 매 프레임 다시 묻지 않는 이유는
    // ResolveBodyArguments의 주석과 같다 - 플레이어가 연 그 시점의 상황을 설명하는 문장이다.
    private object[] _bubbleArgs;

    private GuideLevel Level =>
        _settingsService == null ? GuideLevel.Full : _settingsService.Guide;

    // 플레이어가 스스로 꺼낸 설명이라 어떤 안내보다도 약하다(GuidePriority 주석).
    int IGuideRequestProvider.Priority => GuidePriority.GUIDE_QUEST;

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_window != null)
        {
            _window.QuestClicked.AddListener(HandleQuestClicked);
        }
    }

    private void OnDestroy()
    {
        if (_window != null)
        {
            _window.QuestClicked.RemoveListener(HandleQuestClicked);
        }
    }

    // 제공자 등록은 OnEnable/OnDisable에서 한다 - 꺼진 제공자가 목록에 남으면
    // 아무것도 그리지 않으면서 화면을 쥔 것과 같아진다(UI_GuideOverlay.AddProvider).
    private void OnEnable()
    {
        if (_overlay != null)
        {
            _overlay.AddProvider(this);
        }
    }

    private void OnDisable()
    {
        if (_overlay != null)
        {
            _overlay.RemoveProvider(this);
        }

        ClearBubble();
    }

    /// <summary>
    /// 표시권을 잃었으면 요청을 거둔다. 상태 전이는 TryGetRequest가 아니라 여기서만 한다
    /// (<see cref="IGuideRequestProvider"/> 주석).
    ///
    /// 거두지 않으면 튜토리얼 같은 강한 안내가 끝난 한참 뒤에 플레이어가 잊은 설명이 혼자 떠오른다.
    /// 옛 구조에서 Show가 거절당해 카드로 넘어가던 자리가 여기다.
    /// </summary>
    private void Update()
    {
        if (_bubbleQuest == null || _overlay == null)
        {
            return;
        }

        // "아무도 안 그린다"와 "남이 그린다"를 구분한다 - 오버레이의 해석은 프레임 단위로 캐시되므로,
        // 방금 꺼낸 요청이 아직 반영되지 않은 프레임에 IsShowingFor만 보면 곧바로 스스로를 거둔다.
        if (_overlay.IsShowingGuide && !_overlay.IsShowingFor(this))
        {
            ClearBubble();
        }
    }

    private void HandleQuestClicked(GuideQuestSO quest)
    {
        if (quest == null)
        {
            return;
        }

        if (TryShowBubble(quest))
        {
            return;
        }

        ShowCard(quest);
    }

    /// <summary>
    /// 가리킬 대상이 등록돼 있으면 말풍선으로 띄운다. 표시권을 못 얻으면(다른 안내가 화면을 쥐고 있으면)
    /// false를 돌려주고, 부르는 쪽은 카드로 넘어간다 - 설명을 못 보는 상황을 만들지 않기 위해서다.
    /// </summary>
    private bool TryShowBubble(GuideQuestSO quest)
    {
        if (_overlay == null || Level != GuideLevel.Full || quest.AnchorId == GuideAnchorId.None)
        {
            return false;
        }

        // 이쪽이 가장 약한 우선순위라, 지금 무엇이든 떠 있으면 그려지지 않는다. 그릴 수 없는 요청을
        // 걸어 두고 기다리는 대신 여기서 카드로 넘긴다.
        if (_overlay.IsShowingGuide && !_overlay.IsShowingFor(this))
        {
            return false;
        }

        if (!GuideAnchorRegistry.TryGet(quest.AnchorId, out RectTransform _))
        {
            return false;
        }

        _bubbleQuest = quest;
        _bubbleArgs = ResolveBodyArguments(quest);

        return true;
    }

    /// <summary>
    /// 지금 낼 안내. 낼 것이 없으면 false를 돌려 화면을 넘긴다.
    /// 가리킬 곳이 사라졌으면(창이 닫혔으면) 그때도 넘긴다 - 없는 자리를 가리켜 봐야 읽을 수 없다.
    /// </summary>
    bool IGuideRequestProvider.TryGetRequest(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        if (_bubbleQuest == null ||
            !GuideAnchorRegistry.TryGet(_bubbleQuest.AnchorId, out RectTransform target))
        {
            return false;
        }

        // 대상 클릭을 막지 않는다 - 안내를 읽은 그 자리에서 바로 누를 수 있어야 한다.
        request = GuideRequest.Draw(
            target,
            _bubbleQuest.BodyLocKey,
            blocksTargetInteraction: false,
            keepsInputOpen: false,
            showsConfirmButton: true,
            GuideBubbleSlot.Default,
            _bubbleArgs);

        return true;
    }

    // 오버레이가 화면을 쥔 제공자에게만 보내므로 남의 확인 클릭이 섞이지는 않는다.
    void IGuideRequestProvider.OnConfirmClicked()
    {
        ClearBubble();
    }

    // 대상 클릭을 막지 않으므로 딤에 삼켜지는 클릭도 없다.
    void IGuideRequestProvider.OnBlockedClicked()
    {
    }

    private void ClearBubble()
    {
        _bubbleQuest = null;
        _bubbleArgs = null;
    }

    private void ShowCard(GuideQuestSO quest)
    {
        // 이미 떠 있는 카드가 있으면 무시한다. 카드를 또 넣으면 확인해야 할 것만 늘어난다 -
        // 같은 설명을 다시 보고 싶으면 확인한 뒤 다시 누르면 된다.
        if (_toast == null || _cardOnScreen != null)
        {
            return;
        }

        _cardOnScreen = quest;
        _toast.Show(quest.BodyLocKey, HandleCardDismissed, ResolveBodyArguments(quest));
    }

    // 설명문에 박힌 숫자는 쓴 시점에만 참이다 - 지금 값은 여는 순간에 채운다(GuideQuestBodyComposer).
    private object[] ResolveBodyArguments(GuideQuestSO quest)
    {
        return _bodyComposer == null
            ? System.Array.Empty<object>()
            : _bodyComposer.ResolveArguments(quest);
    }

    private void HandleCardDismissed()
    {
        _cardOnScreen = null;
    }
}
