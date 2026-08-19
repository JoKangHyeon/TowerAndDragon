using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 확인형 알림을 오른쪽 상단에 FIFO 스택으로 표시한다.
/// 카드 내부 레이아웃은 건드리지 않고 생성된 카드와 아래쪽 창들의 바깥 좌표만 조정한다.
///
/// 선택지가 여럿인 카드도 여기서 처리한다. 우상단에 스택을 하나 더 두지 않는 이유는
/// <b>이 컴포넌트가 우상단 세로 열의 유일한 주인</b>이기 때문이다 - 스택 좌표와 아래 창들의
/// 밀어내기를 혼자 계산하므로, 같은 열에 두 번째 컴포넌트가 생기면 서로 자리를 덮어쓴다.
///
/// 그래서 카드 높이가 고정이 아니다. 선택지 3개짜리 카드는 확인 1개짜리보다 크므로
/// 스택 좌표를 곱셈이 아니라 카드별 실제 높이의 누적합으로 잡는다.
/// </summary>
public sealed class UI_ConfirmNotificationToast : MonoBehaviour
{
    private const int MAX_VISIBLE_COUNT = 2;
    private const int MIN_VISIBLE_COUNT = 1;
    private const float DEFAULT_HIDDEN_OFFSET_X = 510f;
    private const float DEFAULT_SLIDE_IN_DURATION = 0.25f;
    private const float DEFAULT_SLIDE_OUT_DURATION = 0.2f;
    private const float DEFAULT_STACK_MOVE_DURATION = 0.2f;
    private const float DEFAULT_CARD_WIDTH = 500f;
    private const float DEFAULT_CARD_HEIGHT = 140f;
    private const float DEFAULT_CARD_SPACING = 10f;
    private const float DEFAULT_CHOICE_SPACING = 8f;
    private const float DEFAULT_BOTTOM_MARGIN = 20f;
    // 씬이 뜬 직후의 로딩 끊김 동안에는 슬라이드가 몇 프레임 만에 끝나버려 애니메이션이 보이지 않는다.
    // 프레임 시간이 안정될 때까지 기다리게 했더니 저사양에서 그 조건이 영영 참이라 알림이 아예 안 나왔다 -
    // 성능과 무관하게 끝나도록 프레임 수로 센다.
    private const int INITIAL_SLIDE_GATE_FRAME_COUNT = 3;
    private const string PATH_SEPARATOR = "/";

    // 선택지가 없는 카드(확인 버튼 하나)가 눌렸을 때의 값. 콜백은 Message.Dismissed 쪽을 쓴다.
    private const int NO_CHOICE_INDEX = -1;

    /// <summary>
    /// 카드에 놓을 선택지 하나. 프리팹의 확인 버튼을 원본 삼아 런타임에 복제하므로
    /// 카드 프리팹에 선택지 개수만큼의 버튼을 미리 만들어 둘 필요가 없다
    /// (그 프리팹은 팀 공용이라 고치지 않는 편이 낫다).
    /// </summary>
    public readonly struct ChoiceOption
    {
        public readonly string LabelLocKey;
        public readonly Action Picked;

        public ChoiceOption(string labelLocKey, Action picked)
        {
            LabelLocKey = labelLocKey;
            Picked = picked;
        }
    }

    [Tooltip("복제할 알림 카드 원본. 런타임에는 템플릿으로만 사용한다.")]
    [SerializeField] private GameObject _messageRoot;

    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _confirmButton;

    [Tooltip("점령 목록. 비어 있으면 같은 Canvas의 자식에서 자동으로 찾는다.")]
    [SerializeField] private RectTransform _claimListRect;

    [Tooltip("카드 스택 아래로 함께 밀려날 창들. 화면에 보이는 순서대로 위에서부터 적는다. " +
             "점령 목록은 이 목록 다음에 자동으로 붙으므로 여기 또 넣지 않는다.")]
    [WiringOptional]
    [SerializeField] private List<RectTransform> _extraFollowerRects = new();

    [Tooltip("카드 스택 <b>위</b>에 고정할 창들. 이 창들은 카드가 떠도 움직이지 않고, " +
             "대신 카드 스택이 이 창들의 높이만큼 아래에서 시작한다.")]
    [WiringOptional]
    [SerializeField] private List<RectTransform> _headerRects = new();

    [Tooltip("오른쪽 화면 밖에 숨길 때 더할 위치 오프셋.")]
    [SerializeField] private Vector2 _hiddenOffset = new(DEFAULT_HIDDEN_OFFSET_X, 0f);

    [SerializeField] private float _slideInDuration = DEFAULT_SLIDE_IN_DURATION;
    [SerializeField] private float _slideOutDuration = DEFAULT_SLIDE_OUT_DURATION;
    [SerializeField] private float _stackMoveDuration = DEFAULT_STACK_MOVE_DURATION;
    [SerializeField] private float _cardWidth = DEFAULT_CARD_WIDTH;
    [SerializeField] private float _cardHeight = DEFAULT_CARD_HEIGHT;
    [SerializeField] private float _cardSpacing = DEFAULT_CARD_SPACING;

    [Tooltip("선택지 버튼 사이 간격. 선택지가 2개 이상인 카드에서만 쓰인다.")]
    [SerializeField] private float _choiceSpacing = DEFAULT_CHOICE_SPACING;

    [SerializeField] private float _bottomMargin = DEFAULT_BOTTOM_MARGIN;

    private readonly struct Message
    {
        public readonly string LocKey;
        public readonly object[] Args;
        public readonly Action Dismissed;
        public readonly IReadOnlyList<ChoiceOption> Choices;

        public Message(string locKey, object[] args, Action dismissed, IReadOnlyList<ChoiceOption> choices)
        {
            LocKey = locKey;
            Args = args;
            Dismissed = dismissed;
            Choices = choices;
        }

        /// <summary>선택지가 있는 카드인가. 없으면 프리팹의 확인 버튼을 문구까지 그대로 쓴다.</summary>
        public bool HasChoices => Choices != null && Choices.Count > 0;

        public int ButtonCount => HasChoices ? Choices.Count : 1;

        public string Resolve()
        {
            string raw = StringTable.GetString(LocKey);
            if (Args == null || Args.Length == 0)
            {
                return raw;
            }

            var resolvedArgs = new object[Args.Length];

            for (int i = 0; i < Args.Length; i++)
            {
                resolvedArgs[i] = Args[i] is LocalizedArgument localizedArgument
                    ? localizedArgument.Resolve()
                    : Args[i];
            }

            return string.Format(raw, resolvedArgs);
        }
    }

    private readonly struct LocalizedArgument
    {
        private readonly string _locKey;

        public LocalizedArgument(string locKey)
        {
            _locKey = locKey;
        }

        public string Resolve()
        {
            return StringTable.GetString(_locKey);
        }
    }

    private sealed class ActiveCard
    {
        public Message Message;
        public GameObject Root;
        public RectTransform RootRect;
        public TMP_Text MessageText;
        public CanvasGroup CanvasGroup;
        public Tween SlideTween;
        public Tween StackMoveTween;
        public bool IsTransitioning;

        // 이 카드가 실제로 차지하는 높이. 선택지 수에 따라 카드마다 다르다.
        public float Height;

        // 첫 칸은 프리팹의 확인 버튼 그 자체이고, 나머지는 그것을 복제한 것이다.
        public readonly List<Button> Buttons = new();
        public readonly List<TMP_Text> ButtonLabels = new();

        // 프리팹 버튼의 라벨에는 LocalizedText가 붙어 있다. 이것이 활성화 시점과 언어 변경 때
        // 자기 key로 글자를 되돌리므로, 선택지 문구는 text가 아니라 이쪽의 key를 갈아 끼워야 한다.
        public readonly List<LocalizedText> ButtonLocalizedLabels = new();
    }

    private readonly LinkedList<Message> _pendingMessages = new();
    private readonly List<ActiveCard> _activeCards = new();
    private readonly List<ActiveCard> _initialSlideWaitingCards = new();

    // 카드 스택 아래로 밀려나는 창들. 목록 순서가 곧 화면의 위아래 순서다.
    private readonly List<RectTransform> _followerRects = new();
    private readonly List<Vector2> _followerOrigins = new();
    private readonly List<Tween> _followerMoveTweens = new();

    private RectTransform _cardsParent;
    private RectTransform _canvasRect;
    private UI_ClaimListWindow _claimListWindow;
    private Vector2 _stackOrigin;
    private string _messageTextPath;
    private string _confirmButtonPath;
    private float _choiceButtonHeight;

    // 프리팹 버튼이 카드 모서리에서 떨어져 있던 거리. 선택지 카드의 여백을 여기서 그대로 물려받는다 -
    // 여백 값을 새로 만들면 프리팹을 손볼 때마다 코드의 숫자와 어긋난다.
    private Vector2 _choiceButtonBasePosition;

    private bool _isInitialSlideGateOpen;

    private float ChoiceStep => _choiceButtonHeight + _choiceSpacing;

    private void Awake()
    {
        if (_messageRoot == null)
        {
            return;
        }

        RectTransform ownRect = transform as RectTransform;
        _cardsParent = transform.parent as RectTransform;
        _stackOrigin = ownRect != null ? ownRect.anchoredPosition : Vector2.zero;

        if (_cardsParent != null)
        {
            Canvas canvas = _cardsParent.GetComponentInParent<Canvas>();
            _canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            _claimListWindow = _cardsParent.GetComponentInChildren<UI_ClaimListWindow>(true);
        }

        if (_claimListRect == null && _claimListWindow != null)
        {
            _claimListRect = _claimListWindow.transform as RectTransform;
        }

        _messageTextPath = GetRelativePath(_messageRoot.transform, _messageText);
        _confirmButtonPath = GetRelativePath(_messageRoot.transform, _confirmButton);
        MeasureTemplateButton();
        BuildFollowerList();
        _messageRoot.SetActive(false);
        RepositionFollowers(false);
    }

    private void Start()
    {
        OpenInitialSlideGateAsync().Forget();
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += RefreshLocalizedText;

        if (_claimListWindow != null)
        {
            _claimListWindow.LayoutChanged += HandleClaimListLayoutChanged;
        }
    }

    private void OnDisable()
    {
        List<Action> dismissedCallbacks = CollectDismissedCallbacks();

        StringTable.OnLanguageChanged -= RefreshLocalizedText;

        if (_claimListWindow != null)
        {
            _claimListWindow.LayoutChanged -= HandleClaimListLayoutChanged;
        }

        KillFollowerTweens();

        _initialSlideWaitingCards.Clear();
        _pendingMessages.Clear();

        foreach (ActiveCard card in _activeCards)
        {
            card.SlideTween?.Kill();
            card.StackMoveTween?.Kill();

            if (card.Root != null)
            {
                Destroy(card.Root);
            }
        }

        _activeCards.Clear();
        RestoreFollowerPositions();
        InvokeDismissedCallbacks(dismissedCallbacks);
    }

    public void Show(string locKey, params object[] args)
    {
        Show(locKey, null, args);
    }

    public void Show(string locKey, Action onDismissed, params object[] args)
    {
        if (!isActiveAndEnabled || !HasRequiredReferences())
        {
            onDismissed?.Invoke();
            return;
        }

        _pendingMessages.AddLast(new Message(locKey, args, onDismissed, null));
        TryFillVisibleCards();
    }

    public void ShowWithLocalizedArgument(string locKey, string argumentLocKey)
    {
        ShowWithLocalizedArgument(locKey, null, argumentLocKey);
    }

    public void ShowWithLocalizedArgument(string locKey, Action onDismissed, string argumentLocKey)
    {
        Show(locKey, onDismissed, new LocalizedArgument(argumentLocKey));
    }

    /// <summary>
    /// 선택지가 여럿인 카드를 띄운다. 고른 선택지의 콜백 하나만 발화한다.
    ///
    /// <b>아무것도 고르지 않고 카드가 사라지면 어느 콜백도 발화하지 않는다</b>(창이 꺼지는 경로).
    /// 그래서 부르는 쪽은 "답을 받았다"를 콜백 안에서만 기록해야 한다 - 밖에서 미리 세워 두면
    /// 답하지 못한 채 창이 꺼졌을 때 다시 물어볼 기회가 사라진다.
    /// </summary>
    public void ShowChoices(string locKey, IReadOnlyList<ChoiceOption> choices, params object[] args)
    {
        if (!isActiveAndEnabled || !HasRequiredReferences() || choices == null || choices.Count == 0)
        {
            return;
        }

        _pendingMessages.AddLast(new Message(locKey, args, null, choices));
        TryFillVisibleCards();
    }

    // 선택지형 카드는 여기서 아무것도 모으지 않는다 - 대신 골라 줄 수 없기 때문이다
    // (Message.Dismissed가 애초에 null이라 아래 루프가 자연히 건너뛴다).
    private List<Action> CollectDismissedCallbacks()
    {
        var callbacks = new List<Action>();

        foreach (ActiveCard card in _activeCards)
        {
            if (card.Message.Dismissed != null)
            {
                callbacks.Add(card.Message.Dismissed);
            }
        }

        foreach (Message message in _pendingMessages)
        {
            if (message.Dismissed != null)
            {
                callbacks.Add(message.Dismissed);
            }
        }

        return callbacks;
    }

    private void InvokeDismissedCallbacks(List<Action> callbacks)
    {
        foreach (Action callback in callbacks)
        {
            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    /// <summary>
    /// 인스펙터에 적은 창들을 위에서부터 잇고, <b>점령 목록은 맨 아래에</b> 붙인다.
    ///
    /// 점령 목록을 마지막에 두는 이유: 이 창은 원정이 없어도 300px짜리 자리를 그대로 차지한다
    /// (내용에 맞춰 줄지 않는다). 위에 두면 원정을 보내지 않은 대부분의 시간에 아래 창들이
    /// 빈 300px만큼 아래로 밀려 화면 밖으로 나간다. 내용만큼만 차지하는 창을 위로 올린다.
    ///
    /// 인스펙터를 비워 두면 점령 목록 하나만 남아 기존 동작 그대로다.
    /// </summary>
    private void BuildFollowerList()
    {
        _followerRects.Clear();
        _followerOrigins.Clear();
        _followerMoveTweens.Clear();

        foreach (RectTransform follower in _extraFollowerRects)
        {
            if (follower != null && !_followerRects.Contains(follower))
            {
                _followerRects.Add(follower);
            }
        }

        if (_claimListRect != null && !_followerRects.Contains(_claimListRect))
        {
            _followerRects.Add(_claimListRect);
        }

        // 각 창의 제자리는 씬에 저장된 좌표 그 자체다. 여기서 읽어 두고 그 아래로만 밀어낸다.
        foreach (RectTransform follower in _followerRects)
        {
            _followerOrigins.Add(follower.anchoredPosition);
            _followerMoveTweens.Add(null);
        }
    }

    private void KillFollowerTweens()
    {
        for (int i = 0; i < _followerMoveTweens.Count; i++)
        {
            _followerMoveTweens[i]?.Kill();
            _followerMoveTweens[i] = null;
        }
    }

    private void RestoreFollowerPositions()
    {
        for (int i = 0; i < _followerRects.Count; i++)
        {
            if (_followerRects[i] != null)
            {
                _followerRects[i].anchoredPosition = _followerOrigins[i];
            }
        }
    }

    private bool HasRequiredReferences()
    {
        return _messageRoot != null && _messageText != null && _confirmButton != null && _cardsParent != null;
    }

    private void TryFillVisibleCards()
    {
        TrimOverflowingCards();

        while (_activeCards.Count < MAX_VISIBLE_COUNT && _pendingMessages.Count > 0)
        {
            Message message = _pendingMessages.First.Value;

            // 한 장은 화면을 넘겨서라도 반드시 보여준다 - 안 보이면 확인해서 치울 방법도 없다.
            if (_activeCards.Count >= MIN_VISIBLE_COUNT && !CanFitAnotherCard(MeasureCardHeight(message)))
            {
                break;
            }

            _pendingMessages.RemoveFirst();
            CreateCard(message);
        }

        RefreshInteractions();
        RepositionFollowers(true);
    }

    // 아래 창들이 커져 자리가 모자라지면 뒤쪽 카드부터 대기열로 되돌린다.
    private void TrimOverflowingCards()
    {
        while (_activeCards.Count > MIN_VISIBLE_COUNT && !FitsInAvailableHeight(TotalStackHeight()))
        {
            int lastIndex = _activeCards.Count - 1;
            ActiveCard card = _activeCards[lastIndex];
            card.SlideTween?.Kill();
            card.StackMoveTween?.Kill();
            _initialSlideWaitingCards.Remove(card);
            _activeCards.RemoveAt(lastIndex);
            _pendingMessages.AddFirst(card.Message);

            if (card.Root != null)
            {
                card.Root.SetActive(false);
                Destroy(card.Root);
            }
        }
    }

    private bool CanFitAnotherCard(float nextCardHeight)
    {
        return FitsInAvailableHeight(TotalStackHeight() + nextCardHeight + _cardSpacing);
    }

    private bool FitsInAvailableHeight(float stackHeight)
    {
        if (_canvasRect == null)
        {
            return true;
        }

        float topOffset = Mathf.Max(0f, -_stackOrigin.y);
        float availableHeight = _canvasRect.rect.height - topOffset - _bottomMargin;

        return TotalHeaderHeight() + stackHeight + TotalFollowerHeight() <= availableHeight;
    }

    private void CreateCard(Message message)
    {
        GameObject root = Instantiate(_messageRoot, _cardsParent, false);
        root.name = _messageRoot.name;

        float cardHeight = MeasureCardHeight(message);
        RectTransform rootRect = root.transform as RectTransform;
        rootRect.anchorMin = Vector2.one;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.sizeDelta = new Vector2(_cardWidth, cardHeight);

        // 카드는 아래 창들보다 앞 형제에 둔다(원래 점령 목록 기준으로 잡던 자리 그대로).
        RectTransform siblingAnchor =
            _claimListRect != null ? _claimListRect : (_followerRects.Count > 0 ? _followerRects[0] : null);

        if (siblingAnchor != null)
        {
            root.transform.SetSiblingIndex(siblingAnchor.GetSiblingIndex());
        }

        TMP_Text messageText = ResolveCloneComponent<TMP_Text>(root, _messageTextPath);
        Button confirmButton = ResolveCloneComponent<Button>(root, _confirmButtonPath);
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

        // 자기 자신은 아직 목록에 없으므로, 지금 있는 카드들 바로 아래가 곧 제자리다.
        Vector2 shownPosition = GetCardPosition(_activeCards.Count);

        var card = new ActiveCard
        {
            Message = message,
            Root = root,
            RootRect = rootRect,
            MessageText = messageText,
            CanvasGroup = canvasGroup,
            Height = cardHeight,
            IsTransitioning = true
        };

        BuildCardButtons(card, confirmButton);
        LayoutChoiceBody(card, messageText);

        _activeCards.Add(card);
        root.SetActive(true);
        rootRect.anchoredPosition = shownPosition + _hiddenOffset;
        messageText.text = message.Resolve();
        SetInteraction(card, false);

        QueueSlideIn(card);
    }

    // 확인 버튼을 원본 삼아 선택지 수만큼 복제한다. 선택지가 없으면 프리팹 버튼을 문구까지 그대로 둔다 -
    // 기존 알림(새끼용 알)이 이 경로를 지나므로 그쪽 표시는 하나도 바뀌지 않는다.
    private void BuildCardButtons(ActiveCard card, Button confirmButton)
    {
        if (confirmButton == null)
        {
            return;
        }

        int buttonCount = card.Message.ButtonCount;
        var firstRect = confirmButton.transform as RectTransform;

        RegisterCardButton(card, confirmButton);

        for (int i = 1; i < buttonCount; i++)
        {
            Button clone = Instantiate(confirmButton, confirmButton.transform.parent, false);
            clone.name = confirmButton.name;

            var cloneRect = clone.transform as RectTransform;
            cloneRect.anchorMin = firstRect.anchorMin;
            cloneRect.anchorMax = firstRect.anchorMax;
            cloneRect.pivot = firstRect.pivot;
            cloneRect.sizeDelta = firstRect.sizeDelta;

            RegisterCardButton(card, clone);
        }

        LayoutChoiceButtons(card);
        RefreshCardButtonLabels(card);
    }

    /// <summary>
    /// 선택지 버튼들을 카드 아래쪽에 세로로 쌓는다.
    ///
    /// 두 가지를 바꾼다.
    /// <b>위로 쌓는다</b> - 프리팹의 확인 버튼은 카드 아래쪽에 앵커돼 있어(anchor (1,0))
    /// 아래로 쌓으면 카드 밖으로 나간다. 첫 선택지가 맨 위에 오도록 순서를 뒤집는다.
    /// <b>가로로 늘린다</b> - 확인 버튼은 "확인" 두 글자에 맞춘 100px짜리다. 선택지는 문장이므로
    /// 그 폭을 물려받으면 글자가 넘치거나 뭉개진다. 좌우 여백만 남기고 카드 폭 전체를 쓴다.
    /// 여백은 원래 버튼이 오른쪽에서 떨어져 있던 거리를 그대로 쓴다 - 프리팹이 정한 값을 존중한다.
    ///
    /// 선택지가 없는 카드는 여기 오지 않으므로 기존 알림의 버튼은 그대로다.
    /// </summary>
    private void LayoutChoiceButtons(ActiveCard card)
    {
        if (!card.Message.HasChoices)
        {
            return;
        }

        for (int i = 0; i < card.Buttons.Count; i++)
        {
            var buttonRect = card.Buttons[i].transform as RectTransform;
            int stepsFromBottom = card.Buttons.Count - 1 - i;

            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(-HorizontalMargin * 2f, _choiceButtonHeight);
            buttonRect.anchoredPosition =
                new Vector2(0f, VerticalMargin + ChoiceStep * stepsFromBottom);
        }
    }

    /// <summary>
    /// 커진 카드의 본문을 글 높이에 맞춰 카드 위쪽에 고정한다.
    ///
    /// 프리팹의 본문은 카드 세로 <b>중앙</b>에 앵커돼 있고 높이가 0이다. 카드가 커지면 본문도 함께
    /// 내려와 아래 버튼과 겹치고, 글이 길면 rect를 넘어 위아래로 흘러나온다.
    /// 여기서 글상자를 실제 글 높이로 잡아 위에 붙이면 둘 다 사라진다 -
    /// 카드 높이도 같은 값에서 역산했으므로(MeasureCardHeight) 글이 카드를 넘지 않는다.
    /// </summary>
    private void LayoutChoiceBody(ActiveCard card, TMP_Text messageText)
    {
        // 프리팹 자리에 들어간 짧은 알림은 손대지 않는다 - 기존 알림의 글 위치가 바뀌면 안 된다.
        if (messageText == null || !NeedsExpansion(card.Message))
        {
            return;
        }

        var textRect = messageText.transform as RectTransform;

        if (textRect == null)
        {
            return;
        }

        // 글상자를 <b>글에 맞춘 높이로</b> 잡고 카드 위쪽에 붙인다. 카드 높이도 같은 값에서
        // 역산했으므로(MeasureCardHeight) 글이 카드를 넘지 않는다.
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -VerticalMargin);
        textRect.sizeDelta = new Vector2(
            -HorizontalMargin * 2f,
            MeasureBodyHeight(card.Message));
    }

    private void RegisterCardButton(ActiveCard card, Button button)
    {
        // 지금 몇 번째로 등록되는가가 곧 이 버튼이 가리키는 선택지 번호다.
        int choiceIndex = card.Message.HasChoices ? card.Buttons.Count : NO_CHOICE_INDEX;

        card.Buttons.Add(button);
        card.ButtonLabels.Add(button.GetComponentInChildren<TMP_Text>(true));
        card.ButtonLocalizedLabels.Add(button.GetComponentInChildren<LocalizedText>(true));

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => HandleChoiceClicked(card, choiceIndex));
    }

    private void RefreshCardButtonLabels(ActiveCard card)
    {
        if (!card.Message.HasChoices)
        {
            return;
        }

        for (int i = 0; i < card.Buttons.Count && i < card.Message.Choices.Count; i++)
        {
            string labelLocKey = card.Message.Choices[i].LabelLocKey;

            // key를 갈아 끼우면 활성화·언어 변경 때도 LocalizedText가 알아서 이 문구를 다시 그린다.
            if (card.ButtonLocalizedLabels[i] != null)
            {
                card.ButtonLocalizedLabels[i].SetKey(labelLocKey);
                continue;
            }

            if (card.ButtonLabels[i] != null)
            {
                card.ButtonLabels[i].text = StringTable.GetString(labelLocKey);
            }
        }
    }

    private void QueueSlideIn(ActiveCard card)
    {
        if (_isInitialSlideGateOpen)
        {
            PlaySlideIn(card);
            return;
        }

        // 게이트는 Start에서 무조건 열리므로 여기서는 줄만 세운다.
        _initialSlideWaitingCards.Add(card);
    }

    private async UniTaskVoid OpenInitialSlideGateAsync()
    {
        await UniTask.DelayFrame(
            INITIAL_SLIDE_GATE_FRAME_COUNT,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        OpenInitialSlideGate();
    }

    private void OpenInitialSlideGate()
    {
        _isInitialSlideGateOpen = true;

        ActiveCard[] waitingCards = _initialSlideWaitingCards.ToArray();
        _initialSlideWaitingCards.Clear();

        foreach (ActiveCard card in waitingCards)
        {
            if (_activeCards.Contains(card) && card.Root != null)
            {
                PlaySlideIn(card);
            }
        }
    }

    private void PlaySlideIn(ActiveCard card)
    {
        Vector2 shownPosition = GetCardPosition(_activeCards.IndexOf(card));

        card.SlideTween = card.RootRect
            .DOAnchorPos(shownPosition, _slideInDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(card.Root)
            .OnComplete(() =>
            {
                card.IsTransitioning = false;
                RefreshInteractions();
            });
    }

    private void HandleChoiceClicked(ActiveCard card, int choiceIndex)
    {
        if (_activeCards.Count == 0 || _activeCards[0] != card || card.IsTransitioning)
        {
            return;
        }

        // 무시된 클릭(맨 앞 카드가 아니거나 슬라이드 중)에는 소리를 내지 않도록 위 가드 뒤에서 재생한다.
        SoundManager.Play(SoundId.UiButtonClick);

        card.IsTransitioning = true;
        SetInteraction(card, false);
        card.SlideTween?.Kill();
        card.StackMoveTween?.Kill();

        card.SlideTween = card.RootRect
            .DOAnchorPos(card.RootRect.anchoredPosition + _hiddenOffset, _slideOutDuration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(card.Root)
            .OnComplete(() => CompleteCard(card, choiceIndex));
    }

    private void CompleteCard(ActiveCard card, int choiceIndex)
    {
        _activeCards.Remove(card);
        Action picked = ResolvePickedCallback(card, choiceIndex);

        if (card.Root != null)
        {
            Destroy(card.Root);
        }

        RepositionVisibleCards();

        try
        {
            picked?.Invoke();
        }
        finally
        {
            TryFillVisibleCards();
        }
    }

    private static Action ResolvePickedCallback(ActiveCard card, int choiceIndex)
    {
        if (!card.Message.HasChoices)
        {
            return card.Message.Dismissed;
        }

        bool isValidIndex = choiceIndex >= 0 && choiceIndex < card.Message.Choices.Count;
        return isValidIndex ? card.Message.Choices[choiceIndex].Picked : null;
    }

    private void RepositionVisibleCards()
    {
        for (int i = 0; i < _activeCards.Count; i++)
        {
            ActiveCard card = _activeCards[i];
            card.StackMoveTween?.Kill();
            card.StackMoveTween = card.RootRect
                .DOAnchorPos(GetCardPosition(i), _stackMoveDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(card.Root);
        }
    }

    // 각 창은 자기 위에 있는 것들(카드 스택 + 앞선 창들)의 높이 합만큼 내려간다.
    private void RepositionFollowers(bool animated)
    {
        float offset = TotalHeaderHeight() + TotalStackHeight();

        for (int i = 0; i < _followerRects.Count; i++)
        {
            RectTransform follower = _followerRects[i];

            if (follower == null)
            {
                continue;
            }

            Vector2 targetPosition = _followerOrigins[i] + Vector2.down * offset;
            _followerMoveTweens[i]?.Kill();

            if (animated)
            {
                _followerMoveTweens[i] = follower
                    .DOAnchorPos(targetPosition, _stackMoveDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(follower.gameObject);
            }
            else
            {
                _followerMoveTweens[i] = null;
                follower.anchoredPosition = targetPosition;
            }

            offset += GetFollowerHeight(follower);
        }
    }

    private Vector2 GetCardPosition(int index)
    {
        return _stackOrigin + Vector2.down * (TotalHeaderHeight() + GetStackOffset(index));
    }

    /// <summary>
    /// 카드 스택 위에 고정된 창들이 차지하는 높이. 카드는 이만큼 아래에서 시작한다.
    ///
    /// 고정 창(오늘 할 일 목록)은 카드가 떠도 자리를 지켜야 한다 - 매번 밀려 내려가면
    /// 볼 때마다 다른 자리에 있어 눈으로 찾는 비용이 든다. 대신 움직이는 쪽을 카드로 정했다.
    /// </summary>
    private float TotalHeaderHeight()
    {
        float total = 0f;

        foreach (RectTransform header in _headerRects)
        {
            total += GetFollowerHeight(header);
        }

        return total;
    }

    // 앞선 카드들의 실제 높이를 더한다. 카드마다 높이가 달라 곱셈으로는 구할 수 없다.
    private float GetStackOffset(int index)
    {
        float offset = 0f;

        for (int i = 0; i < index && i < _activeCards.Count; i++)
        {
            offset += _activeCards[i].Height + _cardSpacing;
        }

        return offset;
    }

    private float TotalStackHeight()
    {
        return GetStackOffset(_activeCards.Count);
    }

    private float TotalFollowerHeight()
    {
        float total = 0f;

        foreach (RectTransform follower in _followerRects)
        {
            total += GetFollowerHeight(follower);
        }

        return total;
    }

    /// <summary>
    /// 이 창이 세로로 차지하는 자리. 꺼져 있으면 0이다 - 퀘스트 목록은 보일 것이 없으면 통째로 꺼진다.
    ///
    /// preferredHeight와 실제 rect 중 <b>큰 쪽</b>을 쓴다. 레이아웃 컴포넌트가 없는 창은
    /// preferredHeight가 0으로 나오는데(점령 목록이 그렇다), 그것만 믿으면 높이 0으로 보고
    /// 다음 창을 같은 자리에 겹쳐 놓는다.
    /// </summary>
    private static float GetFollowerHeight(RectTransform follower)
    {
        if (follower == null || !follower.gameObject.activeInHierarchy)
        {
            return 0f;
        }

        return Mathf.Max(LayoutUtility.GetPreferredHeight(follower), follower.rect.height);
    }

    private float HorizontalMargin => Mathf.Abs(_choiceButtonBasePosition.x);
    private float VerticalMargin => Mathf.Abs(_choiceButtonBasePosition.y);
    private float ChoiceBodyWidth => _cardWidth - HorizontalMargin * 2f;

    /// <summary>
    /// 내용이 들어가는 카드 높이. <b>본문 글 높이에서 역산하되 프리팹 높이 아래로는 줄이지 않는다.</b>
    ///
    /// 프리팹의 140은 "알을 얻었습니다" 한 줄에 맞춘 값이다. 조언자 대사나 퀘스트 설명처럼 문단이
    /// 들어오면 글이 글상자를 넘어 위아래로 흘러나온다(TMP 기본 오버플로는 잘라내지 않고 rect 밖에 그린다).
    /// 그래서 넘칠 때는 키운다.
    ///
    /// 반대로 <b>줄이지는 않는다</b> - 짧은 알림까지 글에 딱 맞게 줄이면 기존 알림의 모양이 바뀐다.
    /// </summary>
    private float MeasureCardHeight(Message message)
    {
        if (!NeedsExpansion(message))
        {
            return _cardHeight;
        }

        // 위 여백 + 본문 + 본문과 버튼 사이 여백 + 버튼들 + 아래 여백.
        return VerticalMargin * 3f + MeasureBodyHeight(message) + MeasureButtonsHeight(message);
    }

    private float MeasureButtonsHeight(Message message)
    {
        int buttonCount = message.ButtonCount;
        return buttonCount * _choiceButtonHeight + (buttonCount - 1) * _choiceSpacing;
    }

    /// <summary>
    /// 프리팹 카드에 이 내용이 안 들어가는가.
    ///
    /// 기준은 <b>프리팹이 실제로 내주는 본문 자리</b>다 - 카드 높이에서 위아래 여백과 버튼 줄을 뺀 나머지.
    /// "카드 높이를 넘는가"로 재면 한 줄짜리 알림도 몇 픽셀 차이로 확장 경로를 타서
    /// 기존 알림의 글 위치가 바뀐다(실제로 4.5px 차이로 그랬다).
    /// 선택지가 둘 이상이면 이 값이 음수가 되므로 자연히 확장으로 판정된다.
    /// </summary>
    private bool NeedsExpansion(Message message)
    {
        float availableBodyHeight = _cardHeight - VerticalMargin * 2f - MeasureButtonsHeight(message);
        return MeasureBodyHeight(message) > availableBodyHeight;
    }

    // 템플릿 글상자에 물어본다. 복제본은 아직 없고, 폰트·크기는 어차피 템플릿에서 물려받는다.
    private float MeasureBodyHeight(Message message)
    {
        if (_messageText == null)
        {
            return _cardHeight;
        }

        return _messageText.GetPreferredValues(message.Resolve(), ChoiceBodyWidth, 0f).y;
    }

    private void MeasureTemplateButton()
    {
        var buttonRect = _confirmButton == null ? null : _confirmButton.transform as RectTransform;

        if (buttonRect == null)
        {
            return;
        }

        _choiceButtonHeight = buttonRect.rect.height;
        _choiceButtonBasePosition = buttonRect.anchoredPosition;
    }

    private void HandleClaimListLayoutChanged()
    {
        TryFillVisibleCards();
    }

    private void RefreshLocalizedText()
    {
        foreach (ActiveCard card in _activeCards)
        {
            if (card.MessageText != null)
            {
                card.MessageText.text = card.Message.Resolve();
            }

            RefreshCardButtonLabels(card);
        }
    }

    private void RefreshInteractions()
    {
        for (int i = 0; i < _activeCards.Count; i++)
        {
            ActiveCard card = _activeCards[i];
            SetInteraction(card, i == 0 && !card.IsTransitioning);
        }
    }

    private static void SetInteraction(ActiveCard card, bool isEnabled)
    {
        if (card.CanvasGroup != null)
        {
            card.CanvasGroup.interactable = isEnabled;
            card.CanvasGroup.blocksRaycasts = isEnabled;
        }

        foreach (Button button in card.Buttons)
        {
            if (button != null)
            {
                button.interactable = isEnabled;
            }
        }
    }

    private static string GetRelativePath(Transform root, Component component)
    {
        if (root == null || component == null)
        {
            return null;
        }

        Transform current = component.transform;
        var pathParts = new Stack<string>();

        while (current != null && current != root)
        {
            pathParts.Push(current.name);
            current = current.parent;
        }

        return current == root ? string.Join(PATH_SEPARATOR, pathParts) : null;
    }

    private static T ResolveCloneComponent<T>(GameObject cloneRoot, string relativePath) where T : Component
    {
        Transform target = string.IsNullOrEmpty(relativePath)
            ? cloneRoot.transform
            : cloneRoot.transform.Find(relativePath);

        return target != null ? target.GetComponent<T>() : null;
    }
}
