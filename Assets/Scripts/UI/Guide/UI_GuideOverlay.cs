using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 가이드 연출 전담. "어디에 무슨 말을 띄울지"만 지시받고 새끼용·튜토리얼 도메인은 모른다.
/// 대상만 밝게 남기는 구멍은 마스크·셰이더 없이 딤 패널 4장(상·하·좌·우)으로 만든다 - 가운데 빈 칸이 곧 구멍이다.
/// 패널의 raycastTarget을 켜면 "대상만 클릭 가능"이 되고, 끄면 어둡기만 하고 뒤쪽이 다 눌린다.
///
/// <b>안내는 밀어 넣지 않고 끌어온다.</b> 매 프레임 등록된 제공자(<see cref="IGuideRequestProvider"/>)에게
/// 우선순위 순으로 물어 처음 참을 돌려준 하나를 그린다. 예전에는 각 안내가 Show/Release로 표시권을
/// 주고받았는데, "누가 지금 그리는가"를 프레임 사이에 들고 있는 한 인계하는 프레임마다 틈이 생겨
/// 낮은 우선순위 안내가 한 프레임 그려지거나 화면이 통째로 비었다(번쩍임). 상태에서 다시 계산하면
/// 그 틈은 존재할 수 없다.
///
/// 그리는 것은 <see cref="LateUpdate"/>에서만 한다 - 모든 Update가 끝난 뒤라 그 프레임의 단계 전진이
/// 이미 반영돼 있다. 게이트 질의(<see cref="IsBlockingInput"/> 등)는 남의 Update와 입력 콜백에서
/// 불리므로 <see cref="EnsureResolved"/>로 그 자리에서 계산한다 - 프레임당 한 번만 돈다.
/// </summary>
public class UI_GuideOverlay : MonoBehaviour, IDayEndBlockQuery, IPointerClickHandler, ICameraInputTransparent
{
    private const int DIM_PANEL_COUNT = 4;
    private const int RECT_CORNER_COUNT = 4;
    private const string DIM_PANEL_NAME = "GuideDim";
    private const string HOLE_BLOCKER_NAME = "GuideHoleBlocker";
    private const float DEFAULT_DIM_ALPHA = 0.85f;
    private const float DEFAULT_HOLE_PADDING = 8f;
    private const float DEFAULT_PULSE_DURATION = 0.6f;
    private const float DEFAULT_PULSE_SCALE = 1.06f;
    private const float HALF = 0.5f;

    // 1920 기준 화면의 절반가량. 말풍선이 화면을 가로지르지 않으면서 문단도 서너 줄로 접힌다.
    private const float DEFAULT_BUBBLE_MAX_WIDTH = 900f;

    // GetPreferredValues에 넘길 "제한 없음". <b>0을 넘기면 제한 없음이 아니라 0으로 제한된다</b> -
    // 그래서 폭을 0으로 물었을 때 짧은 문구가 몇십 픽셀로, 높이를 0으로 물었을 때 세 줄로 나왔다.
    private const float UNCONSTRAINED_SIZE = 100000f;

    private static readonly Vector2 CENTER_PIVOT = new Vector2(HALF, HALF);

    [Tooltip("화면 전체를 덮는 루트. 딤 패널 4장이 이 밑에 런타임 생성된다. " +
             "숨길 때 통째로 비활성화하므로 이 컴포넌트가 붙은 오브젝트 자신이면 안 되고 자식이어야 한다.")]
    [SerializeField] private RectTransform _overlayRoot;

    [Tooltip("말풍선 아트를 담은 루트. 씬에서 놓은 자리가 Default 슬롯이 되고, 단계가 다른 슬롯을 요구하면 " +
             "아래 지정한 자리로 옮긴다. _overlayRoot의 자식이어야 한다.")]
    [SerializeField] private RectTransform _bubbleRoot;
    [SerializeField] private TMP_Text _bubbleText;

    [Tooltip("말풍선 글상자의 최대 폭(px). 이보다 길어지는 문구만 줄바꿈해 접는다. " +
             "0이면 제한하지 않는다(예전 동작).")]
    [SerializeField] private float _bubbleMaxWidth = DEFAULT_BUBBLE_MAX_WIDTH;

    [Tooltip("말풍선을 옮길 위치 표식(빈 RectTransform). 그리드를 가리면 안 되는 단계에 쓴다. " +
             "비워두면 그 슬롯을 요구해도 기본 자리에 그대로 뜬다.")]
    [FormerlySerializedAs("_bubbleSlotTop")]
    [SerializeField] private RectTransform _bubbleSlotCenter;
    [SerializeField] private RectTransform _bubbleSlotBottom;

    [Tooltip("구멍 둘레에 그릴 테두리 프레임(외곽선 이미지). 구멍 크기에 맞춰 코드가 매 프레임 맞춘다. " +
             "_overlayRoot의 자식이어야 하며, 없으면 테두리 없이 딤만 나온다.")]
    [SerializeField] private RectTransform _holeHighlight;

    [Tooltip("읽고 넘기는 설명에 쓰는 확인 버튼. 말풍선 안(_bubbleRoot의 자식)에 두고, " +
             "행동을 기다리는 단계에서는 자동으로 숨는다. 없으면 설명이 시간으로만 넘어간다.")]
    [SerializeField] private Button _confirmButton;

    [Tooltip("말풍선 아래에 잠깐 붙는 보조 줄. 막힌 버튼을 눌렀을 때 그 사유를 여기에 낸다. " +
             "비워두면 사유를 표시하지 않는다(안내 자체는 그대로 돈다).")]
    [SerializeField] private TMP_Text _hintText;

    [Tooltip("안내가 떠 있는 동안 밤으로 넘어가지 못하게 막는 데 쓴다. 비우면 막지 않는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [SerializeField] private Color _dimColor = new Color(0f, 0f, 0f, DEFAULT_DIM_ALPHA);

    [Tooltip("구멍을 대상보다 이만큼 넓게 뚫는다.")]
    [SerializeField] private float _holePadding = DEFAULT_HOLE_PADDING;

    [Header("반복 연출 (말풍선 · 구멍 테두리 공용)")]
    [SerializeField] private float _pulseDuration = DEFAULT_PULSE_DURATION;
    [SerializeField] private float _pulseScale = DEFAULT_PULSE_SCALE;

    [Tooltip("화면을 쥔 제공자가 바뀐 순간을 프레임 번호와 함께 콘솔에 남긴다. '안내가 한 프레임 스쳤다' 같은 " +
             "제보는 화면만 봐서는 어느 경로였는지 되짚을 수 없어, 재현할 때만 켜서 순서를 확인하는 용도다.")]
    [SerializeField] private bool _logsDisplayHandover;

    private readonly Vector3[] _cornerBuffer = new Vector3[RECT_CORNER_COUNT];
    private RectTransform[] _dimPanels;
    private Image[] _dimImages;

    // 구멍 자리를 덮는 투명 패널. 딤 4장 사이의 빈 칸이 곧 통로라 대상은 언제나 눌리는데,
    // "가리키되 누르지는 못하게" 해야 하는 단계가 있다(예: 하루 1회뿐인 어미용 속성 변경 버튼을
    // 설명만 하는 단계에서 눌러버리면 그날 기회가 사라진다). 보이지는 않고 클릭만 막는다.
    private RectTransform _holeBlocker;

    // 안내를 낼 수 있는 쪽. 우선순위 내림차순으로 정렬해 두고, 같은 우선순위는 먼저 등록한 쪽이 앞이다.
    // List.Sort를 쓰지 않는 이유는 그것이 불안정 정렬이라서다 - 챕터 둘이 같은 값을 가지면
    // 프레임마다 순서가 뒤집힐 수 있고, 그러면 어느 쪽이 그릴지가 동전던지기가 된다.
    //
    // Resolve가 이 목록을 foreach로 도는 동안에는 목록을 바꾸면 안 된다. TryGetRequest가 읽기만 하는 한
    // 그럴 일이 없지만(거기서 OnDisable을 유발할 수 없다), 그 규칙이 곧 이 foreach의 안전 근거다.
    private readonly List<IGuideRequestProvider> _providers = new();

    // 이번 프레임에 화면을 쥔 제공자와 그 요청. 프레임당 한 번 계산하고 LateUpdate가 그리기 직전에
    // 반드시 다시 계산한다 - 남의 Update에서 먼저 계산된 값을 그대로 그리면 한 프레임 옛 컷이 나간다.
    private IGuideRequestProvider _currentProvider;
    private GuideRequestPhase _currentPhase = GuideRequestPhase.Hide;

    // 이번 프레임의 요청. Resolve가 채우고 LateUpdate가 그릴 때 쓴다 - 해석과 그리기를 나눠 두어야
    // 게이트 질의가 화면을 건드리지 않는다.
    private GuideRequest _pendingRequest = GuideRequest.Hidden;
    private int _resolvedFrame = -1;
    private bool _isResolving;
    private bool _hasWarnedReentrantResolve;

    // 실제로 화면에 그린 요청과 그 주인. KeepLast는 "이것을 그대로 두라"는 뜻이고,
    // 확인·차단 클릭도 지금 눈에 보이는 그림의 주인에게 가야 한다.
    private IGuideRequestProvider _drawnProvider;
    private GuideRequest _drawnRequest;
    private bool _hasDrawnRequest;

    // 그린 요청이 애초에 가리킬 대상을 가지고 있었는지. "원래 대상이 없는 안내"와 "가리키던 대상이
    // 사라진 안내"는 다르게 다뤄야 한다 - 앞은 그대로 두고, 뒤는 연출을 거둔다.
    // 요청을 그릴 때마다 새로 계산하므로 주인이 바뀐 뒤까지 살아남지 않는다(예전 _expectsTarget의 사고).
    private bool _drawnExpectsTarget;

    private RectTransform _target;
    private Renderer _worldTarget;

    private bool _blocksInput;
    private bool _blocksTargetInteraction;

    // 지금 그리는 컷이 밤 시작을 허용하는지. _blocksInput과 같은 자리에서 갱신한다 -
    // 눈에 보이는 딤과 게이트가 갈라지면 "어둡지 않은데 막힌다"가 된다.
    private bool _allowsNightStart;
    private bool _showConfirmButton;
    private bool _visualsActive;
    private Canvas _canvas;

    // 씬에서 놓은 자리를 Default로 삼는다 - 슬롯을 안 쓰는 단계는 원래 자리로 돌아와야 한다.
    private Vector2 _bubbleHomePosition;
    private bool _hasBubbleHome;
    private Tween _bubblePulseTween;
    private Tween _highlightPulseTween;

    // 보조 줄이 몇 번째로 뜬 것인지. 겹쳐 뜬 사유의 옛 타이머가 새 사유를 지우지 않게 한다.
    private int _hintSequence;

    /// <summary>
    /// 안내를 낼 쪽을 등록한다. 제공자는 자기 <c>OnEnable</c>에서 걸고 <c>OnDisable</c>에서 뗀다 -
    /// 꺼진 제공자가 목록에 남으면 아무것도 그리지 않으면서 화면을 쥔 것과 같아진다.
    /// </summary>
    public void AddProvider(IGuideRequestProvider provider)
    {
        if (provider == null || _providers.Contains(provider))
        {
            return;
        }

        // 같은 우선순위는 뒤에 넣어 등록 순서를 유지한다.
        int index = 0;
        while (index < _providers.Count && _providers[index].Priority >= provider.Priority)
        {
            index++;
        }

        _providers.Insert(index, provider);
    }

    public void RemoveProvider(IGuideRequestProvider provider)
    {
        _providers.Remove(provider);
    }

    // 딤 패널은 raycastTarget이라 클릭을 받지만 자기 몫의 처리는 없다. 이벤트는 부모로 거슬러 올라오므로
    // 루트에 있는 이 컴포넌트가 대신 받는다 - 딤 조각마다 스크립트를 붙이지 않아도 된다.
    void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
    {
        if (!_visualsActive || !_blocksInput || eventData == null)
        {
            return;
        }

        // 말풍선·확인 버튼을 눌러도 이벤트는 여기까지 올라온다. 막힌 클릭만 골라야 한다.
        if (!IsDimPart(eventData.pointerCurrentRaycast.gameObject))
        {
            return;
        }

        // 지금 눈에 보이는 그림의 주인에게 간다 - 사유를 낼 말풍선이 그 주인의 것이다.
        _drawnProvider?.OnBlockedClicked();
    }

    private bool IsDimPart(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (_holeBlocker != null && ReferenceEquals(candidate, _holeBlocker.gameObject))
        {
            return true;
        }

        if (_dimPanels == null)
        {
            return false;
        }

        foreach (RectTransform panel in _dimPanels)
        {
            if (panel != null && ReferenceEquals(candidate, panel.gameObject))
            {
                return true;
            }
        }

        return false;
    }

    // Overlay 모드에서는 카메라를 넘기면 좌표가 어긋나므로 null이어야 한다.
    private Camera UiCamera =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    private static Camera WorldCamera => Camera.main;

    /// <summary>
    /// 지금 안내가 화면을 쥐고 있는지. <see cref="GuideRequestPhase.KeepLast"/>도 참으로 본다 -
    /// 인계하는 프레임에 게이트가 한 번 열리는 것보다 한 프레임 더 막는 쪽이 안전하다.
    /// <see cref="GuideRequestPhase.Hide"/>와 "아무도 요청을 내지 않음"만 거짓이다.
    /// </summary>
    public bool IsShowingGuide
    {
        get
        {
            EnsureResolved();
            return _currentProvider != null && _currentPhase != GuideRequestPhase.Hide;
        }
    }

    /// <summary>
    /// 이 제공자의 안내가 화면을 쥐고 있는지. 관문을 거는 쪽은 반드시 이것을 봐야 한다 -
    /// 화면에 아무 안내도 없는데 버튼과 단축키가 조용히 죽으면 게임 전체가 막힌 것처럼 보인다.
    /// </summary>
    public bool IsShowingFor(object owner) => IsShowingGuide && ReferenceEquals(_currentProvider, owner);

    /// <summary>
    /// 지금 딤이 대상 밖 클릭을 막고 있는지. 키보드 단축키는 딤을 통과하므로, 마우스와 같은 기준으로
    /// 막으려면 단축키 폴링 지점이 이것을 봐야 한다(<see cref="UIManager.CanUseShortcut"/>이 대신 물어준다).
    /// </summary>
    public bool IsBlockingInput
    {
        get
        {
            EnsureResolved();
            return _blocksInput;
        }
    }

    /// <summary>
    /// 안내가 떠 있는 동안에는 밤으로 넘어가지 않는다. 밤은 되돌릴 수 없는 데다 건설·인구 배치가 잠겨
    /// 안내가 시키는 일을 아예 할 수 없게 되는데, 안내는 그대로 남아 무엇을 하라는 것인지 알 수 없어진다.
    ///
    /// 막는 주체를 안내별로 두지 않고 여기 하나로 모은 이유는 딤과 같다 - 러너마다 걸게 하면
    /// 빠뜨리는 곳이 계속 생긴다(팁 체인과 새끼용 가이드가 실제로 빠져 있었다).
    /// 화면에서 걷히면 곧바로 풀리므로, 부화를 기다리려고 창을 닫은 뒤에는 정상적으로 밤이 온다.
    ///
    /// 예외는 <b>밤 버튼을 누르라고 시키는 컷</b>뿐이다(<see cref="GuideRequest.AllowsNightStart"/>).
    /// 그 컷에서까지 막으면 시킨 대로 눌러도 아무 일이 없다.
    /// </summary>
    bool IDayEndBlockQuery.CanEndDay() => !IsShowingGuide || AllowsNightStart;

    /// <summary>지금 그리는 컷이 밤 시작을 허용하는지. 게이트에서만 본다.</summary>
    private bool AllowsNightStart
    {
        get
        {
            EnsureResolved();
            return _allowsNightStart;
        }
    }

    /// <summary>
    /// 지금 안내가 가리키고 있는 UI 대상. 아무것도 안 가리키면 null이다.
    /// 스스로 자리를 계산해 두는 앵커(<see cref="MonsterPathGuideAnchor"/>)가 <b>자기가 쓰일 때만</b>
    /// 계산하도록 판단하는 데 쓴다 - 매 프레임 도는 계산이라 아무도 안 볼 때 돌면 그대로 낭비다.
    /// </summary>
    public RectTransform CurrentTarget
    {
        get
        {
            EnsureResolved();
            return _target;
        }
    }

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        BuildDimPanels();

        // 씬에서 놓은 자리를 기본 자리로 기억한다. 위치를 옮기기 전에 잡아야 한다.
        if (_bubbleRoot != null)
        {
            _bubbleHomePosition = _bubbleRoot.anchoredPosition;
            _hasBubbleHome = true;
        }

        // 딤 패널은 런타임에 뒤로 붙으므로, 가려지면 안 되는 것들을 그 위로 올린다.
        // 테두리를 먼저, 말풍선을 마지막에 올려 말풍선이 항상 최상단이 된다.
        SetAboveDim(_holeHighlight);
        SetAboveDim(_bubbleRoot);

        // SetAboveDim이 말풍선 안의 모든 Graphic의 raycastTarget을 끄므로, 확인 버튼만 되살린다 -
        // 안내용 그래픽은 클릭 대상이 아니지만 이 버튼은 눌려야 한다.
        if (_confirmButton != null)
        {
            if (_confirmButton.targetGraphic != null)
            {
                _confirmButton.targetGraphic.raycastTarget = true;
            }

            _confirmButton.onClick.AddListener(HandleConfirmClicked);
        }
        else
        {
            // 조용히 죽으면 "보이는데 안 눌리는 버튼"이 되고, 화면 전체를 막는 단계에서는 빠져나갈 길까지 사라진다.
            Debug.LogWarning("[UI_GuideOverlay] _confirmButton 인스펙터 미연결 - 확인 버튼이 레이캐스트에서 빠지고 " +
                             "onClick도 등록되지 않아 읽고 넘기는 안내가 진행되지 않습니다.");
        }

        SetVisualsActive(false);
    }

    // 확인 클릭은 지금 눈에 보이는 그림의 주인에게만 간다. 예전에는 이벤트로 전원에게 뿌리고
    // 각 구독자가 "내 것인가"를 걸러냈는데, 그 필터를 한 곳이라도 빠뜨리면 화면에 뜬 적 없는
    // 안내가 클릭 한 번에 통째로 지나가 버렸다.
    private void HandleConfirmClicked()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        _drawnProvider?.OnConfirmClicked();
    }

    // 진단 전용. 프레임 번호를 함께 남기는 것이 핵심이다 - 같은 프레임 안에서 오간 것은 화면에
    // 나가지 않으므로, 번쩍인 안내는 반드시 프레임을 넘긴 구간에 있다.
    private void LogHandover(string action, object owner)
    {
        if (!_logsDisplayHandover)
        {
            return;
        }

        string ownerName = owner is Object unityObject && unityObject != null ? unityObject.name : "?";
        Debug.Log(
            $"[UI_GuideOverlay] f{Time.frameCount} {action} owner={ownerName} phase={_currentPhase} " +
            $"visuals={_visualsActive} key={(_hasDrawnRequest ? _drawnRequest.MessageLocKey : "-")}", this);
    }

    // 안내용 그래픽은 클릭 대상이 아니다 - 차단하지 않는 단계에서 뒤쪽 조작을 막으면 안 된다.
    private static void SetAboveDim(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetAsLastSibling();
        foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += ApplyText;

        if (_cycleManager != null)
        {
            _cycleManager.AddDayEndBlocker(this);
        }
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= ApplyText;
        KillPulses();

        // 끄면 밤 잠금도 같이 풀어준다 - 안 그러면 영영 막힌 채로 남는다.
        if (_cycleManager != null)
        {
            _cycleManager.RemoveDayEndBlocker(this);
        }
    }

    private void ApplyText()
    {
        if (!_hasDrawnRequest || _bubbleText == null)
        {
            return;
        }

        string raw = StringTable.GetString(_drawnRequest.MessageLocKey);
        object[] args = _drawnRequest.Args;
        _bubbleText.text = args == null || args.Length == 0 ? raw : string.Format(raw, args);

        ClampBubbleWidth();
    }

    /// <summary>
    /// 말풍선이 화면 밖으로 뻗지 않게 폭을 제한한다.
    ///
    /// 말풍선 루트는 ContentSizeFitter로 내용에 맞춰 늘어나고 글상자는 줄바꿈이 꺼져 있다.
    /// 한 줄짜리 안내에서는 이것이 딱 맞는 설정이지만, 문단이 들어오면 글이 한 줄로 뻗어
    /// 말풍선이 화면 폭을 넘고 <b>확인 버튼이 화면 밖으로 밀려난다</b> - 실제로 그렇게 갇혔다.
    ///
    /// 짧은 문구는 손대지 않는다. 자연 폭이 상한 안에 들어오면 그 폭을 그대로 선호 폭으로 넘기므로
    /// 지금까지의 말풍선 모양이 하나도 바뀌지 않고, 넘칠 때만 줄바꿈이 켜지며 접힌다.
    /// </summary>
    private void ClampBubbleWidth()
    {
        if (_bubbleMaxWidth <= 0f)
        {
            return;
        }

        var textRect = _bubbleText.transform as RectTransform;

        if (textRect == null)
        {
            return;
        }

        // 재기 전에 줄바꿈을 반드시 꺼 둔다. 이 컴포넌트는 안내마다 재사용되므로 앞 문구가 켜 둔
        // 줄바꿈이 남아 있으면 자연 폭 대신 "좁게 접었을 때의 폭"이 나온다 -
        // 짧은 문구가 몇십 픽셀로 찌그러진다.
        _bubbleText.enableWordWrapping = false;

        float naturalWidth = _bubbleText
            .GetPreferredValues(_bubbleText.text, UNCONSTRAINED_SIZE, UNCONSTRAINED_SIZE).x;

        bool needsWrap = naturalWidth > _bubbleMaxWidth;

        _bubbleText.enableWordWrapping = needsWrap;

        var element = _bubbleText.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = _bubbleText.gameObject.AddComponent<LayoutElement>();
        }

        float width = needsWrap ? _bubbleMaxWidth : naturalWidth;
        element.preferredWidth = width;

        // 높이도 <b>같은 폭에서 잰 값</b>으로 함께 못박는다. 폭만 정하면 레이아웃이 글상자에 물어보는
        // 선호 높이가 그때의 rect 폭 기준이라, 접힌 뒤의 실제 높이와 어긋나 글이 상자를 넘고
        // 아래 버튼 위로 겹쳐 그려진다.
        element.preferredHeight = _bubbleText
            .GetPreferredValues(_bubbleText.text, width, UNCONSTRAINED_SIZE).y;
    }

    private void KillPulses()
    {
        _bubblePulseTween?.Kill();
        _bubblePulseTween = null;
        _highlightPulseTween?.Kill();
        _highlightPulseTween = null;
    }

    /// <summary>
    /// 이번 프레임에 누가 무엇을 그릴지 상태에서 다시 계산한다. 프레임당 한 번만 돌고,
    /// <see cref="LateUpdate"/>가 그리기 직전에 캐시를 무효화해 그 프레임의 단계 전진이 반드시 반영되게 한다.
    /// </summary>
    private void EnsureResolved()
    {
        if (_resolvedFrame == Time.frameCount)
        {
            return;
        }

        // 제공자의 TryGetRequest가 오버레이를 다시 물으면 여기로 되돌아온다. 조용히 지난 답을 주면
        // 게이트가 엉뚱한 값을 받는데 원인은 드러나지 않으므로, 한 번은 시끄럽게 알린다.
        if (_isResolving)
        {
            if (!_hasWarnedReentrantResolve)
            {
                _hasWarnedReentrantResolve = true;
                Debug.LogError(
                    "[UI_GuideOverlay] 제공자의 TryGetRequest가 오버레이를 다시 질의했습니다 - " +
                    "TryGetRequest는 읽기만 해야 합니다(상태 전이는 제공자 자신의 Update에서).", this);
            }

            return;
        }

        _isResolving = true;
        _resolvedFrame = Time.frameCount;
        Resolve();
        _isResolving = false;
    }

    // 우선순위 순으로 물어 처음 참을 돌려준 하나가 화면을 쥔다. 진 쪽은 그리지 않을 뿐 상태는 계속 전진한다.
    private void Resolve()
    {
        IGuideRequestProvider previousProvider = _currentProvider;

        _currentProvider = null;
        _currentPhase = GuideRequestPhase.Hide;

        GuideRequest request = GuideRequest.Hidden;

        foreach (IGuideRequestProvider provider in _providers)
        {
            if (!IsUsableProvider(provider) || !provider.TryGetRequest(out GuideRequest candidate))
            {
                continue;
            }

            _currentProvider = provider;
            _currentPhase = candidate.Phase;
            request = candidate;
            break;
        }

        _pendingRequest = request;
        ApplyResolvedGateState(request);

        if (!ReferenceEquals(previousProvider, _currentProvider))
        {
            LogHandover("RESOLVE", _currentProvider);
        }
    }


    // 소유자가 파괴됐거나 꺼졌으면 건너뛴다 - 아무것도 그리지 않는 제공자가 화면을 쥐면
    // 그 밑의 안내가 영영 뜨지 못한다.
    private static bool IsUsableProvider(IGuideRequestProvider provider)
    {
        if (provider is MonoBehaviour behaviour)
        {
            return behaviour != null && behaviour.isActiveAndEnabled;
        }

        return provider != null;
    }

    /// <summary>
    /// 게이트가 보는 값(딤 차단·현재 대상)을 이번 프레임의 요청에 맞춘다.
    /// <see cref="GuideRequestPhase.KeepLast"/>는 화면이 앞 그림 그대로이므로 그 값을 그대로 쓴다 -
    /// 눈에 보이는 딤과 게이트가 갈라지면 "어둡지 않은데 막힌다"가 된다.
    /// </summary>
    private void ApplyResolvedGateState(in GuideRequest request)
    {
        switch (_currentPhase)
        {
            case GuideRequestPhase.Draw when _currentProvider != null:
                _blocksInput = ComputeBlocksInput(request);
                _allowsNightStart = request.AllowsNightStart;
                _target = request.Target;
                _worldTarget = request.WorldTarget;
                break;

            case GuideRequestPhase.KeepLast when _currentProvider != null && _hasDrawnRequest:
                _blocksInput = ComputeBlocksInput(_drawnRequest);
                _allowsNightStart = _drawnRequest.AllowsNightStart;
                _target = _drawnRequest.Target;
                _worldTarget = _drawnRequest.WorldTarget;
                break;

            default:
                _blocksInput = false;
                _allowsNightStart = false;
                _target = null;
                _worldTarget = null;
                break;
        }
    }

    /// <summary>
    /// 안내가 떠 있는 동안에는 유도한 곳 말고는 누를 수 없다. 요청에 맡기지 않고 여기서 정한다 -
    /// 단계마다 판단하게 두었더니 빠뜨린 곳이 계속 나왔고, 그때마다 플레이어가 엉뚱한 버튼을 눌러
    /// 안내가 가리키던 창을 닫거나 밤으로 넘어가 안내만 남았다.
    ///
    /// 빠져나갈 길이 없을 때만 열어 둔다 - 구멍도 확인 버튼도 없는데 막으면 아무것도 누를 수 없다.
    /// <see cref="GuideRequest.KeepsInputOpen"/>은 그 자동 판단을 요청이 되돌리는 유일한 통로다.
    /// </summary>
    private bool ComputeBlocksInput(in GuideRequest request)
    {
        // 확인 버튼은 딤 위에 있어 막아도 계속 눌린다. 요청이 켜라고 해도 배선이 비어 있으면
        // 실제로는 버튼이 없는 것과 같으므로 둘을 함께 본다.
        bool hasEscape = request.ShowsConfirmButton && _confirmButton != null;
        bool hasTarget = request.Target != null || request.WorldTarget != null;

        return !request.KeepsInputOpen && (hasTarget || hasEscape);
    }

    /// <summary>
    /// 그리기는 여기서만 한다. 모든 Update가 끝난 뒤라 그 프레임의 단계 전진이 이미 반영돼 있다 -
    /// Update에서 폴링하면 러너의 Update와 순서 보장이 없어 한 프레임 옛 컷을 그린다.
    /// </summary>
    private void LateUpdate()
    {
        // 프레임 중간의 게이트 질의가 남긴 계산은 러너의 Update보다 앞설 수 있다. 그리기 직전에 버린다.
        _resolvedFrame = -1;
        EnsureResolved();

        switch (_currentPhase)
        {
            case GuideRequestPhase.Draw when _currentProvider != null:
                if (!_hasDrawnRequest ||
                    !ReferenceEquals(_drawnProvider, _currentProvider) ||
                    !_pendingRequest.Matches(_drawnRequest))
                {
                    ApplyRequest(_currentProvider, _pendingRequest);
                    break;
                }

                RefreshDrawn();
                break;

            // 앞 프레임의 그림을 그대로 둔다. 대상이 움직였을 수 있으므로 구멍만 다시 맞춘다.
            case GuideRequestPhase.KeepLast when _currentProvider != null:
                RefreshDrawn();
                break;

            default:
                ClearVisuals();
                break;
        }
    }

    private void ApplyRequest(IGuideRequestProvider provider, in GuideRequest request)
    {
        if (_overlayRoot == null)
        {
            Debug.LogWarning(
                $"[UI_GuideOverlay] {request.MessageLocKey} 안내를 띄울 수 없다 - _overlayRoot가 비었다.");
            return;
        }

        _drawnProvider = provider;
        _drawnRequest = request;
        _hasDrawnRequest = true;
        _drawnExpectsTarget = request.Target != null || request.WorldTarget != null;

        _blocksTargetInteraction = request.BlocksTargetInteraction;
        _showConfirmButton = request.ShowsConfirmButton;

        // 앞 컷에서 낸 사유는 여기서 지운다 - 새 안내와 함께 남아 있으면 방금 막힌 것처럼 읽힌다.
        HideHint();

        ApplyText();
        ApplyDim();
        ApplyBubbleSlot(request.BubbleSlot);

        // 문구와 확인 버튼이 바뀌었으므로 말풍선 크기와 연출을 이 자리에서 다시 확정한다.
        SetVisualsActive(true);
        LogHandover("DRAW", provider);

        RefreshDrawn();
    }

    /// <summary>
    /// 구멍은 레이아웃이 한 프레임 뒤에 확정되거나 대상이 움직일 수 있으므로 표시 중에 매 프레임 다시 맞춘다.
    /// 말풍선은 씬에 고정이라 여기서 손대지 않는다.
    ///
    /// 가리키던 대상이 지금 화면에 없으면 연출만 감춘다 - 계속 기다릴지 걷을지는 제공자가
    /// 자기 Update에서 정하므로(대상 실종·미도착 판정) 여기서 표시권을 건드리지 않는다.
    /// <see cref="GuideRequestPhase.KeepLast"/>만은 예외다(아래 참고).
    /// </summary>
    private void RefreshDrawn()
    {
        if (!_hasDrawnRequest)
        {
            return;
        }

        bool isUiTargetVisible = _target != null && _target.gameObject.activeInHierarchy;
        bool isWorldTargetVisible = _worldTarget != null &&
                                    _worldTarget.enabled &&
                                    _worldTarget.gameObject.activeInHierarchy;

        // KeepLast는 "앞 그림을 그대로 붙들어라"는 뜻이다. 그 그림의 대상은 인계를 부른 행동으로
        // 이미 사라져 있을 수 있는데(패널을 닫아야 끝나는 컷이 그렇다), 대상 실종으로 연출을 거두면
        // 새 컷이 도착하기까지 한 프레임 딤이 걷혀 화면이 번쩍인다 - 붙들라는 요청과 정반대다.
        // 구멍만 포기하고 덮개는 유지한다(아래 _blocksInput 분기의 LayoutFullCover).
        bool holdsLastPicture = _currentPhase == GuideRequestPhase.KeepLast;

        bool canShow = holdsLastPicture || !_drawnExpectsTarget || isUiTargetVisible || isWorldTargetVisible;

        if (canShow != _visualsActive)
        {
            SetVisualsActive(canShow);
        }

        if (!canShow)
        {
            return;
        }

        if (isUiTargetVisible)
        {
            Layout();
            return;
        }

        if (isWorldTargetVisible)
        {
            LayoutWorldTarget();
            return;
        }

        if (_blocksInput)
        {
            LayoutFullCover();
            return;
        }

        SetSpotlightActive(false);
    }

    // 화면을 걷는다. 그린 요청도 함께 버려 다음 KeepLast가 지워진 그림을 되살리지 않게 한다.
    private void ClearVisuals()
    {
        if (!_hasDrawnRequest && !_visualsActive)
        {
            return;
        }

        LogHandover("CLEAR", _drawnProvider);

        _drawnProvider = null;
        _drawnRequest = GuideRequest.Hidden;
        _hasDrawnRequest = false;
        _drawnExpectsTarget = false;

        HideHint();
        SetVisualsActive(false);
    }

    /// <summary>
    /// 가릴 대상이 없는 설명 단계에서 화면 전체를 덮는다. 읽는 동안 뒤쪽 버튼이 눌리지 않아야 한다 -
    /// 특히 창의 닫기 버튼을 잘못 누르면 안내가 가리키던 창이 사라진다.
    /// 확인 버튼은 딤 위에 올려 두었으므로 계속 눌린다.
    /// </summary>
    private void LayoutFullCover()
    {
        if (_dimPanels == null)
        {
            return;
        }

        Rect full = _overlayRoot.rect;

        // 한 장으로 전부 덮고 나머지는 접는다 - 구멍이 없으므로 4장으로 나눌 이유가 없다.
        for (int i = 0; i < _dimPanels.Length; i++)
        {
            _dimPanels[i].gameObject.SetActive(true);
            SetPanel(_dimPanels[i], full.xMin, full.yMin, i == 0 ? full.xMax : full.xMin, i == 0 ? full.yMax : full.yMin);
        }

        // 구멍이 없으니 테두리와 구멍 차단막은 쓰지 않는다.
        if (_holeHighlight != null)
        {
            _holeHighlight.gameObject.SetActive(false);
        }

        if (_holeBlocker != null)
        {
            _holeBlocker.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 말풍선 아래에 보조 줄을 잠깐 띄운다. 막았다는 사실을 그 자리에서 알리는 데 쓴다 -
    /// 무반응으로 두면 플레이어가 버그로 읽는다.
    ///
    /// <b>화면을 쥔 쪽만 낼 수 있다.</b> 안내는 둘 이상 동시에 살아 있을 수 있고(챕터 + 새끼용 가이드)
    /// 진 쪽도 관문 질의에는 거절을 돌려주므로, 그대로 두면 화면에 뜬 적 없는 안내가 말을 건다.
    /// </summary>
    public void ShowHint(object owner, string locKey, float durationSeconds)
    {
        if (_hintText == null || !_visualsActive || !IsShowingFor(owner))
        {
            return;
        }

        _hintText.text = StringTable.GetString(locKey);
        _hintText.gameObject.SetActive(true);

        // 힌트가 붙으면 말풍선이 그만큼 자란다. 이 자리에서 확정하지 않으면 한 프레임 늦게 반영돼
        // 문구는 그대로인데 말풍선만 뒤늦게 늘어난다(SetVisualsActive가 같은 이유로 쓰는 호출이다).
        if (_bubbleRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubbleRoot);
        }

        _hintSequence++;
        HideHintLaterAsync(_hintSequence, durationSeconds).Forget();
    }

    // 사유는 잠깐 붙었다 사라진다. 남겨두면 안내 문구처럼 읽혀 "지금 할 일"과 섞인다.
    // 그 사이 새 사유가 뜨면 옛 타이머는 자기 차례가 아니므로 아무것도 하지 않는다.
    private async UniTaskVoid HideHintLaterAsync(int sequence, float durationSeconds)
    {
        await UniTask.WaitForSeconds(
            durationSeconds,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        if (sequence == _hintSequence)
        {
            HideHint();
        }
    }

    private void HideHint()
    {
        if (_hintText != null)
        {
            _hintText.gameObject.SetActive(false);
        }
    }

    // 표식이 비어 있으면 기본 자리에 그대로 둔다 - 배선을 빼먹었을 때 말풍선이 화면 밖으로 날아가지 않게.
    private void ApplyBubbleSlot(GuideBubbleSlot slot)
    {
        if (_bubbleRoot == null || !_hasBubbleHome)
        {
            return;
        }

        RectTransform marker = slot switch
        {
            GuideBubbleSlot.Center => _bubbleSlotCenter,
            GuideBubbleSlot.Bottom => _bubbleSlotBottom,
            _ => null,
        };

        if (marker == null)
        {
            _bubbleRoot.anchoredPosition = _bubbleHomePosition;
            return;
        }

        // 표식이 말풍선과 다른 부모 밑에 있어도 같은 화면 자리에 놓이도록 월드 좌표로 맞춘다.
        _bubbleRoot.position = marker.position;
    }

    private void SetVisualsActive(bool isActive)
    {
        _visualsActive = isActive;

        if (_bubbleRoot != null)
        {
            _bubbleRoot.gameObject.SetActive(isActive);
        }

        if (_confirmButton != null)
        {
            _confirmButton.gameObject.SetActive(isActive && _showConfirmButton);
        }

        if (_overlayRoot != null)
        {
            _overlayRoot.gameObject.SetActive(isActive);
        }

        // 문구와 확인 버튼의 활성 상태를 같은 프레임에 말풍선 높이에 반영한다.
        // ContentSizeFitter의 일반 갱신을 기다리면 버튼이 바뀌는 순간 배경이 한 프레임 늦게 따라온다.
        if (isActive && _bubbleRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubbleRoot);
        }

        if (isActive)
        {
            PlayPulses();
        }
        else
        {
            KillPulses();
        }
    }

    private void BuildDimPanels()
    {
        if (_overlayRoot == null)
        {
            return;
        }

        _dimPanels = new RectTransform[DIM_PANEL_COUNT];
        _dimImages = new Image[DIM_PANEL_COUNT];
        for (int i = 0; i < DIM_PANEL_COUNT; i++)
        {
            var panel = new GameObject(DIM_PANEL_NAME, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(_overlayRoot, false);

            // 앵커를 루트 피벗에 맞춰 두면 anchoredPosition과 RectTransform.rect가 같은 좌표계가 된다.
            rect.anchorMin = _overlayRoot.pivot;
            rect.anchorMax = _overlayRoot.pivot;
            rect.pivot = Vector2.zero;

            var image = panel.GetComponent<Image>();
            image.color = _dimColor;

            _dimPanels[i] = rect;
            _dimImages[i] = image;
        }

        BuildHoleBlocker();
    }

    // 딤 패널 뒤에 만든다 - 이후 SetAboveDim이 테두리와 말풍선을 그 위로 올리므로
    // 확인 버튼은 이 패널에 막히지 않는다.
    private void BuildHoleBlocker()
    {
        var blocker = new GameObject(HOLE_BLOCKER_NAME, typeof(RectTransform), typeof(Image));
        _holeBlocker = (RectTransform)blocker.transform;
        _holeBlocker.SetParent(_overlayRoot, false);
        _holeBlocker.anchorMin = _overlayRoot.pivot;
        _holeBlocker.anchorMax = _overlayRoot.pivot;
        _holeBlocker.pivot = CENTER_PIVOT;

        var image = blocker.GetComponent<Image>();

        // 알파 0이어도 raycastTarget이 켜져 있으면 클릭은 막힌다.
        image.color = Color.clear;
        image.raycastTarget = true;

        blocker.SetActive(false);
    }

    // 어둡게 칠하는 것과 클릭을 막는 것은 같은 값을 쓴다 - 막지 않는 단계를 어둡게 하면 멈춘 줄 알고,
    // 어둡지 않은데 막으면 보이지 않는 벽이 된다. 막지 않을 때도 패널 자체는 투명하게 남겨 둔다.
    //
    // 요청이 그대로면(RefreshDrawn 경로) 다시 칠하지 않는데, 그래도 어긋나지 않는 이유는
    // _blocksInput이 그린 요청만으로 정해지기 때문이다(ComputeBlocksInput은 요청과 확인 버튼 배선만 본다).
    // 요청이 바뀌면 반드시 ApplyRequest를 지나므로 딤도 함께 다시 칠해진다.
    private void ApplyDim()
    {
        if (_dimImages == null)
        {
            return;
        }

        Color color = _blocksInput ? _dimColor : Color.clear;
        foreach (Image image in _dimImages)
        {
            image.color = color;
            image.raycastTarget = _blocksInput;
        }
    }

    private void Layout()
    {
        Rect hole = ResolveLocalRect(_target);
        SetSpotlightActive(true);
        LayoutDim(hole);
        LayoutHighlight(hole);
        LayoutHoleBlocker(hole);
    }

    private void LayoutWorldTarget()
    {
        if (!TryResolveWorldLocalRect(_worldTarget, out Rect hole))
        {
            SetVisualsActive(false);
            return;
        }

        SetSpotlightActive(true);
        LayoutDim(hole);
        LayoutHighlight(hole);
        LayoutHoleBlocker(hole);
    }

    private void LayoutHoleBlocker(Rect hole)
    {
        if (_holeBlocker == null)
        {
            return;
        }

        _holeBlocker.anchoredPosition = hole.center;
        _holeBlocker.sizeDelta = hole.size;
    }

    private void SetSpotlightActive(bool isActive)
    {
        if (_dimPanels != null)
        {
            foreach (RectTransform panel in _dimPanels)
            {
                panel.gameObject.SetActive(isActive);
            }
        }

        if (_holeHighlight != null)
        {
            _holeHighlight.gameObject.SetActive(isActive);
        }

        // 대상이 없으면 막을 것도 없다 - 구멍 없이 켜면 화면 한가운데를 이유 없이 가로막는다.
        if (_holeBlocker != null)
        {
            _holeBlocker.gameObject.SetActive(isActive && _blocksTargetInteraction);
        }
    }

    // 테두리는 구멍과 정확히 같은 사각형을 덮는다. 크기를 매 프레임 덮어쓰므로 펄스는 scale로만 준다.
    private void LayoutHighlight(Rect hole)
    {
        if (_holeHighlight == null)
        {
            return;
        }

        _holeHighlight.anchorMin = _overlayRoot.pivot;
        _holeHighlight.anchorMax = _overlayRoot.pivot;
        _holeHighlight.pivot = CENTER_PIVOT;
        _holeHighlight.anchoredPosition = hole.center;
        _holeHighlight.sizeDelta = hole.size;
    }

    private void LayoutDim(Rect hole)
    {
        if (_dimPanels == null)
        {
            return;
        }

        Rect full = _overlayRoot.rect;
        SetPanel(_dimPanels[0], full.xMin, hole.yMax, full.xMax, full.yMax);
        SetPanel(_dimPanels[1], full.xMin, full.yMin, full.xMax, hole.yMin);
        SetPanel(_dimPanels[2], full.xMin, hole.yMin, hole.xMin, hole.yMax);
        SetPanel(_dimPanels[3], hole.xMax, hole.yMin, full.xMax, hole.yMax);
    }

    private static void SetPanel(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchoredPosition = new Vector2(xMin, yMin);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
    }

    // 대상의 화면 사각형을 오버레이 로컬 좌표(피벗 기준)로 옮긴다.
    private Rect ResolveLocalRect(RectTransform target)
    {
        target.GetWorldCorners(_cornerBuffer);

        Camera uiCamera = UiCamera;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector3 corner in _cornerBuffer)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _overlayRoot, screenPoint, uiCamera, out Vector2 localPoint))
            {
                continue;
            }

            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }

        if (min.x > max.x || min.y > max.y)
        {
            return Rect.zero;
        }

        var padding = new Vector2(_holePadding, _holePadding);
        min -= padding;
        max += padding;
        return new Rect(min, max - min);
    }

    private bool TryResolveWorldLocalRect(Renderer target, out Rect localRect)
    {
        localRect = Rect.zero;
        Camera worldCamera = WorldCamera;

        if (target == null || worldCamera == null)
        {
            return false;
        }

        Bounds bounds = target.bounds;
        Vector3 boundsMin = bounds.min;
        Vector3 boundsMax = bounds.max;

        var screenMin = new Vector2(float.MaxValue, float.MaxValue);
        var screenMax = new Vector2(float.MinValue, float.MinValue);

        AccumulateWorldCorner(new Vector3(boundsMin.x, boundsMin.y, boundsMin.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMin.x, boundsMin.y, boundsMax.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMin.x, boundsMax.y, boundsMin.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMin.x, boundsMax.y, boundsMax.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMax.x, boundsMin.y, boundsMin.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMax.x, boundsMin.y, boundsMax.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMax.x, boundsMax.y, boundsMin.z), worldCamera, ref screenMin, ref screenMax);
        AccumulateWorldCorner(new Vector3(boundsMax.x, boundsMax.y, boundsMax.z), worldCamera, ref screenMin, ref screenMax);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRoot, screenMin, UiCamera, out Vector2 localMin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRoot, screenMax, UiCamera, out Vector2 localMax))
        {
            return false;
        }

        var padding = new Vector2(_holePadding, _holePadding);
        localMin -= padding;
        localMax += padding;
        localRect = new Rect(localMin, localMax - localMin);
        return true;
    }

    private static void AccumulateWorldCorner(
        Vector3 corner,
        Camera worldCamera,
        ref Vector2 screenMin,
        ref Vector2 screenMax)
    {
        Vector2 screenPoint = worldCamera.WorldToScreenPoint(corner);
        screenMin = Vector2.Min(screenMin, screenPoint);
        screenMax = Vector2.Max(screenMax, screenPoint);
    }

    private void PlayPulses()
    {
        KillPulses();
        _bubblePulseTween = CreatePulse(_bubbleRoot);

        // 테두리는 대상이 있을 때만 보이므로 그때만 움직인다.
        if (_target != null || _worldTarget != null)
        {
            _highlightPulseTween = CreatePulse(_holeHighlight);
        }
    }

    private Tween CreatePulse(RectTransform rect)
    {
        if (rect == null)
        {
            return null;
        }

        rect.localScale = Vector3.one;

        // 일시정지(Time.timeScale == 0) 중에도 안내는 계속 움직여야 한다.
        return rect.DOScale(_pulseScale, _pulseDuration)
            .SetLink(rect.gameObject)
            .SetUpdate(true)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }
}
