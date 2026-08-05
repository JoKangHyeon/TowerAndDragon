using DG.Tweening;
using TMPro;
using UnityEngine;
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
    [SerializeField] private RectTransform _bubbleSlotTop;
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
    private RectTransform _target;
    private bool _blocksInput;
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
        bool showConfirmButton, GuideBubbleSlot bubbleSlot, params object[] args)
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

        // 가릴 대상 없이 화면만 덮으면 아무것도 누를 수 없게 되므로 그 조합은 받지 않는다.
        // 조용히 사라지면 앵커가 여러 개인 안내에서 원인을 찾을 수 없으므로 반드시 남긴다.
        if (_overlayRoot == null || (target == null && blocksInput))
        {
            Debug.LogWarning($"[UI_GuideOverlay] {locKey} 안내를 띄울 수 없다 - " +
                             "_overlayRoot가 비었거나, 가릴 대상 없이 입력을 막으려 했다.");
            Release(owner);
            return false;
        }

        _owner = owner;
        _ownerPriority = priority;
        _target = target;
        _blocksInput = blocksInput;
        _showConfirmButton = showConfirmButton;
        _currentLocKey = locKey;
        _currentArgs = args;
        ApplyText();
        ApplyDimRaycast();
        ApplyBubbleSlot(bubbleSlot);

        SetVisualsActive(true);

        if (_target == null)
        {
            SetSpotlightActive(false);
        }
        else
        {
            Layout();
        }

        return true;
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
            GuideBubbleSlot.Top => _bubbleSlotTop,
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
        if (!HasOwner || _target == null)
        {
            return;
        }

        // 대상이 잠시 사라지는 경우(창을 닫았다 다시 여는 등)에는 표시권을 놓지 않고 연출만 감춘다 -
        // 놓아버리면 대상이 돌아와도 아무도 다시 Show하지 않아 안내가 영구히 사라진다.
        bool isTargetVisible = _target.gameObject.activeInHierarchy;
        if (_visualsActive != isTargetVisible)
        {
            SetVisualsActive(isTargetVisible);
        }

        if (!isTargetVisible)
        {
            return;
        }

        Layout();
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
