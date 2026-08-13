using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 확인형 알림을 오른쪽 상단에 FIFO 스택으로 표시한다.
/// 카드 내부 레이아웃은 건드리지 않고 생성된 카드와 점령 목록의 바깥 좌표만 조정한다.
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
    private const float DEFAULT_BOTTOM_MARGIN = 20f;
    // 씬이 뜬 직후의 로딩 끊김 동안에는 슬라이드가 몇 프레임 만에 끝나버려 애니메이션이 보이지 않는다.
    // 프레임 시간이 안정될 때까지 기다리게 했더니 저사양에서 그 조건이 영영 참이라 알림이 아예 안 나왔다 -
    // 성능과 무관하게 끝나도록 프레임 수로 센다.
    private const int INITIAL_SLIDE_GATE_FRAME_COUNT = 3;
    private const string PATH_SEPARATOR = "/";

    [Tooltip("복제할 알림 카드 원본. 런타임에는 템플릿으로만 사용한다.")]
    [SerializeField] private GameObject _messageRoot;

    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _confirmButton;

    [Tooltip("점령 목록. 비어 있으면 같은 Canvas의 자식에서 자동으로 찾는다.")]
    [SerializeField] private RectTransform _claimListRect;

    [Tooltip("오른쪽 화면 밖에 숨길 때 더할 위치 오프셋.")]
    [SerializeField] private Vector2 _hiddenOffset = new(DEFAULT_HIDDEN_OFFSET_X, 0f);

    [SerializeField] private float _slideInDuration = DEFAULT_SLIDE_IN_DURATION;
    [SerializeField] private float _slideOutDuration = DEFAULT_SLIDE_OUT_DURATION;
    [SerializeField] private float _stackMoveDuration = DEFAULT_STACK_MOVE_DURATION;
    [SerializeField] private float _cardWidth = DEFAULT_CARD_WIDTH;
    [SerializeField] private float _cardHeight = DEFAULT_CARD_HEIGHT;
    [SerializeField] private float _cardSpacing = DEFAULT_CARD_SPACING;
    [SerializeField] private float _bottomMargin = DEFAULT_BOTTOM_MARGIN;

    private readonly struct Message
    {
        public readonly string LocKey;
        public readonly object[] Args;
        public readonly Action Dismissed;

        public Message(string locKey, object[] args, Action dismissed)
        {
            LocKey = locKey;
            Args = args;
            Dismissed = dismissed;
        }

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
        public Button ConfirmButton;
        public CanvasGroup CanvasGroup;
        public Tween SlideTween;
        public Tween StackMoveTween;
        public bool IsTransitioning;
    }

    private readonly LinkedList<Message> _pendingMessages = new();
    private readonly List<ActiveCard> _activeCards = new();
    private readonly List<ActiveCard> _initialSlideWaitingCards = new();

    private RectTransform _cardsParent;
    private RectTransform _canvasRect;
    private UI_ClaimListWindow _claimListWindow;
    private Vector2 _stackOrigin;
    private float _claimListPreferredHeight;
    private string _messageTextPath;
    private string _confirmButtonPath;
    private Tween _claimListMoveTween;
    private bool _isInitialSlideGateOpen;

    private float CardStep => _cardHeight + _cardSpacing;

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
        RefreshClaimListPreferredHeight();
        _messageRoot.SetActive(false);
        RepositionClaimList(false);
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

        RefreshClaimListPreferredHeight();
    }

    private void OnDisable()
    {
        List<Action> dismissedCallbacks = CollectDismissedCallbacks();

        StringTable.OnLanguageChanged -= RefreshLocalizedText;

        if (_claimListWindow != null)
        {
            _claimListWindow.LayoutChanged -= HandleClaimListLayoutChanged;
        }

        _claimListMoveTween?.Kill();
        _claimListMoveTween = null;

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
        RestoreClaimListPosition();
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

        _pendingMessages.AddLast(new Message(locKey, args, onDismissed));
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

    private void RestoreClaimListPosition()
    {
        if (_claimListRect != null)
        {
            _claimListRect.anchoredPosition = _stackOrigin;
        }
    }

    private bool HasRequiredReferences()
    {
        return _messageRoot != null && _messageText != null && _confirmButton != null && _cardsParent != null;
    }

    private void TryFillVisibleCards()
    {
        int visibleLimit = ResolveVisibleLimit();
        TrimVisibleCards(visibleLimit);

        while (_activeCards.Count < visibleLimit && _pendingMessages.Count > 0)
        {
            Message message = _pendingMessages.First.Value;
            _pendingMessages.RemoveFirst();
            CreateCard(message);
        }

        RefreshInteractions();
        RepositionClaimList(true);
    }

    private void TrimVisibleCards(int visibleLimit)
    {
        for (int i = _activeCards.Count - 1; i >= visibleLimit; i--)
        {
            ActiveCard card = _activeCards[i];
            card.SlideTween?.Kill();
            card.StackMoveTween?.Kill();
            _initialSlideWaitingCards.Remove(card);
            _activeCards.RemoveAt(i);
            _pendingMessages.AddFirst(card.Message);

            if (card.Root != null)
            {
                card.Root.SetActive(false);
                Destroy(card.Root);
            }
        }
    }

    private void CreateCard(Message message)
    {
        GameObject root = Instantiate(_messageRoot, _cardsParent, false);
        root.name = _messageRoot.name;

        RectTransform rootRect = root.transform as RectTransform;
        rootRect.anchorMin = Vector2.one;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.sizeDelta = new Vector2(_cardWidth, _cardHeight);

        if (_claimListRect != null)
        {
            root.transform.SetSiblingIndex(_claimListRect.GetSiblingIndex());
        }

        TMP_Text messageText = ResolveCloneComponent<TMP_Text>(root, _messageTextPath);
        Button confirmButton = ResolveCloneComponent<Button>(root, _confirmButtonPath);
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        Vector2 shownPosition = GetCardPosition(_activeCards.Count);

        var card = new ActiveCard
        {
            Message = message,
            Root = root,
            RootRect = rootRect,
            MessageText = messageText,
            ConfirmButton = confirmButton,
            CanvasGroup = canvasGroup,
            IsTransitioning = true
        };

        _activeCards.Add(card);
        root.SetActive(true);
        rootRect.anchoredPosition = shownPosition + _hiddenOffset;
        messageText.text = message.Resolve();
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(() => HandleConfirmClicked(card));
        SetInteraction(card, false);

        QueueSlideIn(card);
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

    private void HandleConfirmClicked(ActiveCard card)
    {
        if (_activeCards.Count == 0 || _activeCards[0] != card || card.IsTransitioning)
        {
            return;
        }

        card.IsTransitioning = true;
        SetInteraction(card, false);
        card.SlideTween?.Kill();
        card.StackMoveTween?.Kill();

        card.SlideTween = card.RootRect
            .DOAnchorPos(card.RootRect.anchoredPosition + _hiddenOffset, _slideOutDuration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(card.Root)
            .OnComplete(() => CompleteCard(card));
    }

    private void CompleteCard(ActiveCard card)
    {
        _activeCards.Remove(card);
        Action dismissed = card.Message.Dismissed;

        if (card.Root != null)
        {
            Destroy(card.Root);
        }

        RepositionVisibleCards();

        try
        {
            dismissed?.Invoke();
        }
        finally
        {
            TryFillVisibleCards();
        }
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

    private void RepositionClaimList(bool animated)
    {
        if (_claimListRect == null)
        {
            return;
        }

        Vector2 targetPosition = _stackOrigin + Vector2.down * (CardStep * _activeCards.Count);
        _claimListMoveTween?.Kill();

        if (!animated)
        {
            _claimListRect.anchoredPosition = targetPosition;
            return;
        }

        _claimListMoveTween = _claimListRect
            .DOAnchorPos(targetPosition, _stackMoveDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(_claimListRect.gameObject);
    }

    private Vector2 GetCardPosition(int index)
    {
        return _stackOrigin + Vector2.down * (CardStep * index);
    }

    private int ResolveVisibleLimit()
    {
        if (_claimListRect == null || _cardsParent == null)
        {
            return MAX_VISIBLE_COUNT;
        }

        if (_canvasRect == null)
        {
            return MAX_VISIBLE_COUNT;
        }

        float topOffset = Mathf.Max(0f, -_stackOrigin.y);
        float availableHeight = _canvasRect.rect.height - topOffset - _bottomMargin;
        int fittingCount = Mathf.FloorToInt(
            (availableHeight - _claimListPreferredHeight + _cardSpacing) / CardStep);

        return Mathf.Clamp(fittingCount, MIN_VISIBLE_COUNT, MAX_VISIBLE_COUNT);
    }

    private void HandleClaimListLayoutChanged()
    {
        RefreshClaimListPreferredHeight();
        TryFillVisibleCards();
    }

    private void RefreshClaimListPreferredHeight()
    {
        _claimListPreferredHeight = _claimListRect == null
            ? 0f
            : LayoutUtility.GetPreferredHeight(_claimListRect);
    }

    private void RefreshLocalizedText()
    {
        foreach (ActiveCard card in _activeCards)
        {
            if (card.MessageText != null)
            {
                card.MessageText.text = card.Message.Resolve();
            }
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

        if (card.ConfirmButton != null)
        {
            card.ConfirmButton.interactable = isEnabled;
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
