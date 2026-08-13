using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면(StartScene) 인트로 연출. 버튼 배선은 UI_TitleWindow가 하고, 여기서는 보여 주는 순서만 맡는다.
///
/// 진입 시에는 로고와 "Click To Start" 안내만 두고, 안내 문구를 알파 0↔1로 깜빡인다.
/// 클릭하면 안내가 사라지고 로고가 위로 올라가면서, 비워진 자리에서 메뉴 버튼이
/// 왼쪽에서 오른쪽으로 하나씩 밀려 들어오며 나타난다.
///
/// 알파는 버튼 자신(Button_01)이 아니라 그 부모(레이아웃 자식)의 CanvasGroup으로 조절한다.
/// 버튼 쪽 CanvasGroup은 UI_ButtonInteractableFade가 LateUpdate마다 덮어써서 트윈이 먹지 않는다.
/// CanvasGroup 알파는 부모·자식이 곱해지므로, 이어하기/불러오기의 비활성 흐림 처리는 그대로 남는다.
/// </summary>
public class UI_TitleIntro : MonoBehaviour
{
    private const float DEFAULT_BLINK_DURATION = 0.9f;
    private const float DEFAULT_PROMPT_FADE_OUT_DURATION = 0.25f;
    private const float DEFAULT_LOGO_RISE_DURATION = 0.6f;
    private const float DEFAULT_LOGO_RISE_DISTANCE = 250f;
    private const float DEFAULT_BUTTON_REVEAL_DURATION = 0.35f;
    private const float DEFAULT_BUTTON_SLIDE_DISTANCE = 150f;
    private const float DEFAULT_BUTTON_INTERVAL = 0.1f;

    private const float HIDDEN_ALPHA = 0f;
    private const float VISIBLE_ALPHA = 1f;
    private const int INFINITE_LOOPS = -1;

    [Header("클릭 안내")]
    [Tooltip("클릭 안내 전체(문구 + 장식선)의 루트. 클릭하면 통째로 사라진다. CanvasGroup이 없으면 자동으로 붙인다.")]
    [SerializeField] private GameObject _startPrompt;

    [Tooltip("깜빡일 대상. 보통 문구만 깜빡이고 장식선은 그대로 둔다. 비워 두면 루트 전체가 깜빡인다.")]
    [SerializeField] private GameObject _blinkTarget;

    [Tooltip("안내 문구가 알파 0에서 1까지 가는 데 걸리는 시간(초). 왕복이므로 한 번 깜빡이는 데는 두 배가 걸린다.")]
    [SerializeField] private float _blinkDuration = DEFAULT_BLINK_DURATION;

    [Tooltip("클릭한 뒤 안내 문구가 사라지는 시간(초).")]
    [SerializeField] private float _promptFadeOutDuration = DEFAULT_PROMPT_FADE_OUT_DURATION;

    [Header("로고")]
    [SerializeField] private RectTransform _logo;

    [Tooltip("로고가 올라가는 높이(px). 현재 위치 기준 상대값이라 씬에서 로고를 옮겨도 그대로 쓸 수 있다.")]
    [SerializeField] private float _logoRiseDistance = DEFAULT_LOGO_RISE_DISTANCE;

    [SerializeField] private float _logoRiseDuration = DEFAULT_LOGO_RISE_DURATION;
    [SerializeField] private Ease _logoRiseEase = Ease.OutCubic;

    [Header("메뉴 버튼")]
    [Tooltip("메뉴 버튼들의 부모. 자식 하나가 버튼 하나로 취급된다.")]
    [SerializeField] private GameObject _menuButtonsRoot;

    [Tooltip("버튼이 밀려 들어오기 시작하는 왼쪽 오프셋(px).")]
    [SerializeField] private float _buttonSlideDistance = DEFAULT_BUTTON_SLIDE_DISTANCE;

    [Tooltip("버튼 하나가 제자리에 놓이며 나타나는 시간(초).")]
    [SerializeField] private float _buttonRevealDuration = DEFAULT_BUTTON_REVEAL_DURATION;

    [Tooltip("버튼 사이의 등장 간격(초).")]
    [SerializeField] private float _buttonInterval = DEFAULT_BUTTON_INTERVAL;

    [SerializeField] private Ease _buttonSlideEase = Ease.OutCubic;

    private readonly List<RectTransform> _menuItemRects = new();
    private readonly List<CanvasGroup> _menuItemGroups = new();
    private readonly List<Vector2> _menuItemHomePositions = new();

    private CanvasGroup _promptGroup;
    private CanvasGroup _blinkGroup;
    private LayoutGroup _menuLayoutGroup;
    private Tween _blinkTween;
    private Sequence _revealSequence;

    // 클릭을 이미 받았는지. 연출 도중 다시 눌려 시퀀스가 겹치는 것을 막는다.
    private bool _isRevealed;

    private void Awake()
    {
        // 씬에 어떤 활성 상태로 저장돼 있든 인트로는 항상 같은 화면에서 시작한다.
        if (_startPrompt != null)
        {
            _promptGroup = EnsureCanvasGroup(_startPrompt);
            _startPrompt.SetActive(true);

            // 깜빡임은 문구만, 사라지는 것은 장식선까지 포함한 루트 전체다.
            // 둘이 같은 오브젝트를 가리켜도(= 대상 미지정) 그대로 동작한다.
            _blinkGroup = EnsureCanvasGroup(_blinkTarget != null ? _blinkTarget : _startPrompt);
        }

        if (_logo != null)
        {
            _logo.gameObject.SetActive(true);
        }

        if (_menuButtonsRoot != null)
        {
            _menuButtonsRoot.SetActive(false);
        }
    }

    // 첫 발화는 Start 이후로 미룬다(CLAUDE.md 이벤트 초기화 규칙).
    private void Start()
    {
        StartBlink();
    }

    private void Update()
    {
        if (_isRevealed || Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            PlayReveal();
        }
    }

    private void StartBlink()
    {
        if (_blinkGroup == null)
        {
            return;
        }

        _blinkGroup.alpha = HIDDEN_ALPHA;
        _blinkTween = _blinkGroup.DOFade(VISIBLE_ALPHA, _blinkDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(INFINITE_LOOPS, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void PlayReveal()
    {
        _isRevealed = true;
        SoundManager.Play(SoundId.UiButtonClick);

        // 사라지는 동안 문구가 계속 깜빡이지 않도록 먼저 끊는다.
        // 깜빡임 대상을 따로 지정하지 않았다면 페이드 아웃과 같은 알파를 건드리므로 더더욱 먼저 끊어야 한다.
        _blinkTween?.Kill();

        _revealSequence = DOTween.Sequence().SetLink(gameObject);

        if (_promptGroup != null)
        {
            _revealSequence.Insert(0f, _promptGroup.DOFade(HIDDEN_ALPHA, _promptFadeOutDuration)
                .OnComplete(() => _startPrompt.SetActive(false)));
        }

        if (_logo != null)
        {
            float targetY = _logo.anchoredPosition.y + _logoRiseDistance;
            _revealSequence.Insert(0f, _logo.DOAnchorPosY(targetY, _logoRiseDuration).SetEase(_logoRiseEase));
        }

        InsertMenuReveal(_logoRiseDuration);
    }

    /// <summary>로고가 자리를 비운 뒤부터 버튼을 하나씩 끼워 넣는다.</summary>
    private void InsertMenuReveal(float startTime)
    {
        if (_menuButtonsRoot == null)
        {
            return;
        }

        CollectMenuItems();

        for (int i = 0; i < _menuItemGroups.Count; i++)
        {
            CanvasGroup group = _menuItemGroups[i];
            RectTransform rect = _menuItemRects[i];
            Vector2 homePosition = _menuItemHomePositions[i];
            float itemTime = startTime + i * _buttonInterval;

            _revealSequence.Insert(itemTime, group.DOFade(VISIBLE_ALPHA, _buttonRevealDuration)
                .OnComplete(() => group.blocksRaycasts = true));

            _revealSequence.Insert(itemTime, rect.DOAnchorPos(homePosition, _buttonRevealDuration)
                .SetEase(_buttonSlideEase));
        }

        // 트윈이 끝나면 자식 위치는 레이아웃이 계산해 둔 값과 같으므로, 그대로 다시 맡겨도 화면은 흔들리지 않는다.
        _revealSequence.OnComplete(RestoreMenuLayout);
    }

    /// <summary>
    /// 메뉴를 켜서 레이아웃이 최종 위치를 잡게 한 뒤, 그 위치를 목적지로 기억하고 레이아웃을 끈다.
    /// 레이아웃 그룹은 매 프레임 자식 위치를 되돌리므로, 켜 둔 채로는 슬라이드 트윈이 보이지 않는다.
    /// 비활성 오브젝트에는 레이아웃 계산이 돌지 않으니 SetActive가 반드시 먼저다.
    /// </summary>
    private void CollectMenuItems()
    {
        _menuItemRects.Clear();
        _menuItemGroups.Clear();
        _menuItemHomePositions.Clear();

        _menuButtonsRoot.SetActive(true);

        RectTransform rootRect = (RectTransform)_menuButtonsRoot.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

        _menuLayoutGroup = _menuButtonsRoot.GetComponent<LayoutGroup>();
        if (_menuLayoutGroup != null)
        {
            _menuLayoutGroup.enabled = false;
        }

        foreach (Transform child in rootRect)
        {
            RectTransform rect = child as RectTransform;
            if (rect == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            CanvasGroup group = EnsureCanvasGroup(child.gameObject);
            Vector2 homePosition = rect.anchoredPosition;

            _menuItemRects.Add(rect);
            _menuItemGroups.Add(group);
            _menuItemHomePositions.Add(homePosition);

            group.alpha = HIDDEN_ALPHA;

            // 아직 보이지 않는 버튼이 클릭되지 않게 막고, 자기 등장이 끝날 때 되돌린다.
            group.blocksRaycasts = false;

            rect.anchoredPosition = new Vector2(homePosition.x - _buttonSlideDistance, homePosition.y);
        }
    }

    private void RestoreMenuLayout()
    {
        if (_menuLayoutGroup != null)
        {
            _menuLayoutGroup.enabled = true;
        }
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }
}
