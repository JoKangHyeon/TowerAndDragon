using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 가이드 연출 전담. "어디에 무슨 말을 띄울지"만 지시받고 새끼용·튜토리얼 도메인은 모른다.
/// 대상만 밝게 남기는 구멍은 마스크·셰이더 없이 딤 패널 4장(상·하·좌·우)으로 만든다 - 가운데 빈 칸이 곧 구멍이다.
/// 패널의 raycastTarget을 켜면 "대상만 클릭 가능"이 되고, 끄면 어둡기만 하고 뒤쪽이 다 눌린다.
/// 단일 인스턴스로 쓰는 것을 전제한다 - 그래야 안내끼리 겹치지 않는다. 여러 가이드가 동시에 뜨려 하면
/// 우선순위가 높은 쪽이 표시권을 잡고, 진 쪽은 Show가 false를 돌려받아 그리지 않는다(상태는 계속 전진).
/// </summary>
public class UI_GuideOverlay : MonoBehaviour
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

    private static readonly Vector2 CENTER_PIVOT = new Vector2(HALF, HALF);

    [Tooltip("화면 전체를 덮는 루트. 딤 패널 4장이 이 밑에 런타임 생성된다. " +
             "숨길 때 통째로 비활성화하므로 이 컴포넌트가 붙은 오브젝트 자신이면 안 되고 자식이어야 한다.")]
    [SerializeField] private RectTransform _overlayRoot;

    [Tooltip("말풍선 아트를 담은 루트. 씬에서 놓은 자리가 Default 슬롯이 되고, 단계가 다른 슬롯을 요구하면 " +
             "아래 지정한 자리로 옮긴다. _overlayRoot의 자식이어야 한다.")]
    [SerializeField] private RectTransform _bubbleRoot;
    [SerializeField] private TMP_Text _bubbleText;

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

    [SerializeField] private Color _dimColor = new Color(0f, 0f, 0f, DEFAULT_DIM_ALPHA);

    [Tooltip("구멍을 대상보다 이만큼 넓게 뚫는다.")]
    [SerializeField] private float _holePadding = DEFAULT_HOLE_PADDING;

    [Header("반복 연출 (말풍선 · 구멍 테두리 공용)")]
    [SerializeField] private float _pulseDuration = DEFAULT_PULSE_DURATION;
    [SerializeField] private float _pulseScale = DEFAULT_PULSE_SCALE;

    private readonly Vector3[] _cornerBuffer = new Vector3[RECT_CORNER_COUNT];
    private RectTransform[] _dimPanels;
    private Image[] _dimImages;

    // 구멍 자리를 덮는 투명 패널. 딤 4장 사이의 빈 칸이 곧 통로라 대상은 언제나 눌리는데,
    // "가리키되 누르지는 못하게" 해야 하는 단계가 있다(예: 하루 1회뿐인 어미용 속성 변경 버튼을
    // 설명만 하는 단계에서 눌러버리면 그날 기회가 사라진다). 보이지는 않고 클릭만 막는다.
    private RectTransform _holeBlocker;

    private RectTransform _target;

    // 이 단계가 애초에 가리킬 대상을 가지고 있었는지. "원래 대상이 없는 안내"와
    // "가리키던 대상이 사라진 안내"는 다르게 다뤄야 한다 - 앞은 그대로 두고, 뒤는 연출을 거둔다.
    private bool _expectsTarget;

    private bool _blocksInput;
    private bool _blocksTargetInteraction;
    private bool _showConfirmButton;
    private bool _visualsActive;
    private Canvas _canvas;

    // 표시권을 가진 가이드. 이 컴포넌트는 owner가 무엇인지 모르고 우선순위 크기만 비교한다.
    private object _owner;
    private int _ownerPriority;

    // 씬에서 놓은 자리를 Default로 삼는다 - 슬롯을 안 쓰는 단계는 원래 자리로 돌아와야 한다.
    private Vector2 _bubbleHomePosition;
    private bool _hasBubbleHome;
    private Tween _bubblePulseTween;
    private Tween _highlightPulseTween;

    // 포맷 인자가 들어가는 문구라 LocalizedText를 붙일 수 없으므로(팀 스트링테이블 규칙), 원재료를 들고 있다가
    // 언어가 바뀌면 직접 다시 포맷한다. UI_IngameWindow가 쓰는 것과 같은 방식이다.
    private string _currentLocKey;
    private object[] _currentArgs;

    /// <summary>
    /// 표시권을 놓았을 때 알린다. 구독자는 <b>캐시된 요청을 되살리는 대신 자기 현재 상태로 다시 유도</b>해야 한다 -
    /// 양보하는 동안 단계가 전진했을 수 있어 옛 요청을 재생하면 이미 지나간 안내가 다시 뜬다.
    /// </summary>
    public event System.Action DisplayReleased;

    /// <summary>확인 버튼이 눌렸다. 지금 표시권을 가진 쪽에게만 의미가 있다.</summary>
    public event System.Action ConfirmClicked;

    // Overlay 모드에서는 카메라를 넘기면 좌표가 어긋나므로 null이어야 한다.
    private Camera UiCamera =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    // 소유자가 Release 없이 파괴된 경우에도 표시권이 영구히 잠기지 않게 유니티 쪽 null 비교를 태운다.
    private bool HasOwner => _owner is MonoBehaviour behaviour ? behaviour != null : _owner != null;

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

    private void HandleConfirmClicked() => ConfirmClicked?.Invoke();

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
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= ApplyText;
        KillPulses();
    }

    private void ApplyText()
    {
        if (!HasOwner || _bubbleText == null)
        {
            return;
        }

        string raw = StringTable.GetString(_currentLocKey);
        _bubbleText.text = _currentArgs == null || _currentArgs.Length == 0
            ? raw
            : string.Format(raw, _currentArgs);
    }

    private void KillPulses()
    {
        _bubblePulseTween?.Kill();
        _bubblePulseTween = null;
        _highlightPulseTween?.Kill();
        _highlightPulseTween = null;
    }

    /// <summary>
    /// 말풍선을 띄운다. target을 주면 그 대상만 남기고 화면을 어둡게 덮고, target이 없으면 말풍선만 띄운다.
    /// blocksInput은 "어둡게"와 별개로 "대상 외 클릭을 막을지"만 정한다.
    /// 문구는 로컬 키로만 받는다 - 이 컴포넌트는 문자열 리터럴을 갖지 않는다.
    /// 더 높은 우선순위가 표시권을 쥐고 있으면 아무것도 그리지 않고 false를 돌려준다 - 호출자는 그냥 넘어가면 된다.
    /// showConfirmButton은 읽고 넘기는 설명에서만 켠다 - 행동을 기다리는 단계에 버튼이 있으면
    /// 그 행동을 건너뛰고 눌러버릴 수 있다.
    /// </summary>
    public bool Show(object owner, int priority, RectTransform target, string locKey, bool blocksInput,
        bool blocksTargetInteraction, bool showConfirmButton, GuideBubbleSlot bubbleSlot, params object[] args)
    {
        if (owner == null)
        {
            Debug.LogWarning($"[UI_GuideOverlay] owner 없이 Show({locKey})가 호출됐다 - 표시권을 관리할 수 없어 무시한다.");
            return false;
        }

        // 같은 우선순위면 먼저 잡은 쪽이 유지한다 - 매 프레임 서로 빼앗으면 안내가 깜빡인다.
        if (HasOwner && !ReferenceEquals(_owner, owner) && priority <= _ownerPriority)
        {
            return false;
        }

        // 대상 없이 화면을 덮는 것은 빠져나갈 길이 있을 때만 받는다 - 확인 버튼은 딤 위에 있어 계속 눌린다.
        // 그 버튼조차 없으면 아무것도 누를 수 없게 되므로 거절한다.
        // 단계가 버튼을 켜라고 해도 배선이 비어 있으면 실제로는 버튼이 없는 것과 같다 - 둘을 함께 본다.
        // 조용히 사라지면 앵커가 여러 개인 안내에서 원인을 찾을 수 없으므로 반드시 남긴다.
        bool hasEscape = showConfirmButton && _confirmButton != null;
        if (_overlayRoot == null || (target == null && blocksInput && !hasEscape))
        {
            Debug.LogWarning($"[UI_GuideOverlay] {locKey} 안내를 띄울 수 없다 - " +
                             "_overlayRoot가 비었거나, 빠져나갈 버튼 없이 화면 전체를 막으려 했다.");
            Release(owner);
            return false;
        }

        _owner = owner;
        _ownerPriority = priority;
        _target = target;
        _expectsTarget = target != null;
        _blocksInput = blocksInput;
        _blocksTargetInteraction = blocksTargetInteraction;
        _showConfirmButton = showConfirmButton;
        _currentLocKey = locKey;
        _currentArgs = args;
        ApplyText();
        ApplyDimRaycast();
        ApplyBubbleSlot(bubbleSlot);

        SetVisualsActive(true);

        if (_target != null)
        {
            Layout();
        }
        else if (_blocksInput)
        {
            LayoutFullCover();
        }
        else
        {
            SetSpotlightActive(false);
        }

        return true;
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
    /// 표시권은 쥔 채로 화면에서만 걷는다. 안내가 끝났지만 아직 다음 안내에 넘길 때가 아닐 때 쓴다 -
    /// 그냥 Release하면 대기 중이던 낮은 우선순위가 곧바로 그려져 다른 알림과 겹친다.
    /// </summary>
    public void Suspend(object owner)
    {
        if (!HasOwner || !ReferenceEquals(_owner, owner))
        {
            return;
        }

        _target = null;
        SetVisualsActive(false);
    }

    /// <summary>
    /// 표시권을 놓는다. 소유자가 아닌 쪽이 불러도 아무 일도 없다 - 남의 안내를 지우지 못하게 한다.
    /// </summary>
    public void Release(object owner)
    {
        if (!HasOwner || !ReferenceEquals(_owner, owner))
        {
            return;
        }

        _owner = null;
        _target = null;
        SetVisualsActive(false);

        // 기다리던 가이드가 자기 현재 단계로 다시 유도할 기회를 준다.
        DisplayReleased?.Invoke();
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

    // 구멍은 레이아웃이 한 프레임 뒤에 확정되거나 대상이 움직일 수 있으므로 표시 중에 매 프레임 다시 맞춘다.
    // 말풍선은 씬에 고정이라 여기서 손대지 않는다.
    private void LateUpdate()
    {
        // 원래 가리킬 대상이 없는 안내(타일을 클릭하라는 등)는 Show가 그려 둔 그대로 둔다 -
        // 여기서 손대면 대상이 사라진 것으로 오해해 말풍선을 지워버린다.
        if (!HasOwner || !_expectsTarget)
        {
            return;
        }

        bool isTargetVisible = _target != null && _target.gameObject.activeInHierarchy;

        if (isTargetVisible)
        {
            if (!_visualsActive)
            {
                SetVisualsActive(true);
            }

            Layout();
            return;
        }

        // 대상이 사라졌다(가리키던 창을 닫았거나 슬롯이 없어졌다).
        // 확인 버튼으로 넘기는 설명이라면 말풍선은 그대로 두어야 한다 - 같이 감추면
        // 넘길 방법이 사라져 아무것도 누를 수 없는 상태가 된다.
        if (_showConfirmButton)
        {
            if (!_visualsActive)
            {
                SetVisualsActive(true);
            }

            if (_blocksInput)
            {
                LayoutFullCover();
            }
            else
            {
                SetSpotlightActive(false);
            }

            return;
        }

        // 행동을 기다리는 단계는 연출만 감춘다 - 표시권을 놓아버리면 대상이 돌아와도
        // 아무도 다시 Show하지 않아 안내가 영구히 사라진다. 그 행동 자체가 대상을 되살린다.
        if (_visualsActive)
        {
            SetVisualsActive(false);
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

    // 딤은 "보이는 것"과 "막는 것"이 별개다. 대상이 있으면 늘 어둡게 깔되, 막을지는 단계가 정한다 -
    // 알 슬롯처럼 눌러도 반응이 없는 대상을 강조할 때 막아버리면 플레이어가 빠져나갈 길이 없다.
    private void ApplyDimRaycast()
    {
        if (_dimImages == null)
        {
            return;
        }

        foreach (Image image in _dimImages)
        {
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

    private void PlayPulses()
    {
        KillPulses();
        _bubblePulseTween = CreatePulse(_bubbleRoot);

        // 테두리는 대상이 있을 때만 보이므로 그때만 움직인다.
        if (_target != null)
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
