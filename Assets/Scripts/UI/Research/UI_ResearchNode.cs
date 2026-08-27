using System;
using Coffee.UIExtensions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 1개의 시각 표현.
//
// 생김새는 전부 프리팹(ResearchNode.prefab)이 정한다 - 크기·배치·스프라이트를 코드가 만들지
// 않는다. 예전에는 카드 그림자·채움·테두리·갈래 띠를 전부 코드로 깔았는데, 그 탓에 프리팹
// 레이아웃을 바꿔도 화면이 따라오지 않았다.
//
// 코드가 맡는 것은 상태에 따라 바뀌는 것뿐이다.
//  - 슬롯 색(갈래 색을 상태별 밝기로 낮춘다). 글자·파티클 색은 손대지 않는다 - 프리팹 그대로다.
//  - 자물쇠 켜고 끄기
//  - 잠김 → 완료 순간의 해금 연출(ProgressionUnlockEffect - 용 스킬트리와 같은 연출이다)
//
// 연결선이 붙는 자리도 프리팹에서 읽는다(<see cref="ResolveEdgeAnchorRect"/>) - 노드 카드가
// 세로로 길어져 카드 중심과 슬롯 중심이 어긋나기 때문이다.
public class UI_ResearchNode : MonoBehaviour
{
    private const float HALF = 0.5f;
    private const float OPAQUE = 1f;

    // 상태별 밝기. 슬롯은 상태를 크게 벌려 표현하고, 글자는 "읽을 수 있는 하한"을 지킨다 -
    // 글자까지 같이 어둡게 하면 화면의 대부분인 잠긴 노드를 아예 못 읽는다(실측).
    private const float ACCENT_COMPLETED = 1f;
    private const float ACCENT_AVAILABLE = 0.90f;
    private const float ACCENT_SHORT = 0.65f;
    private const float ACCENT_LOCKED = 0.42f;

    private const float TEXT_COMPLETED = 1f;
    private const float TEXT_AVAILABLE = 0.95f;
    private const float TEXT_SHORT = 0.80f;
    private const float TEXT_LOCKED = 0.62f;

    [Header("Visual Parts")]
    [Tooltip("갈래 색으로 칠하는 배경 슬롯. 해금 연출의 번쩍임도 여기서 난다.")]
    [SerializeField] private Image _slot;

    [Tooltip("슬롯 가운데 아이콘. 프리팹에 스프라이트가 없으면 통째로 꺼진다.")]
    [SerializeField] private Image _icon;

    [Tooltip("아직 완료하지 않은 노드에 켜지는 자물쇠. 완료하면 연출과 함께 꺼진다.")]
    [SerializeField] private Image _lock;

    [Tooltip("단계(I/II/III)를 별로 보여주는 묶음. 단계가 없는 연구에서는 통째로 꺼진다. " +
        "주기 해금을 정하는 Tier와는 다른 값이다.")]
    [SerializeField] private GameObject _rankRoot;

    [Tooltip("단계 별. 앞에서부터 단계 수만큼 켠다 - 꽂아 둔 별의 개수가 표시 가능한 최대 단계다.")]
    [SerializeField] private Image[] _rankStars;

    [Tooltip("해금 순간에 뿌리는 파티클(UIParticle). 자식 파티클까지 함께 재생하므로 루트만 꽂으면 된다. " +
        "색은 갈래와 무관하게 프리팹에 저작해 둔 그대로 나온다. " +
        "평소에는 오브젝트를 꺼 둔다 - 켜 두면 노드 전부가 UIParticle 갱신에 참여한다.")]
    [SerializeField] private UIParticle _unlockParticle;

    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _button;

    private Action<ResearchNodeData> _onClick;
    private bool _clickListenerReady;

    // 프리팹에 저작된 별 색. 밝기를 곱해 칠하므로 원본을 따로 들고 있어야 한다.
    private Color[] _rankStarColors;

    // 직전 Bind에서의 상태. "잠김 → 완료" 전이에서만 연출을 재생하려고 들고 있는다.
    // 아직 한 번도 바인딩되지 않았으면 null이라, 창을 다시 열어 이미 완료된 노드를 처음
    // 그릴 때는 재생되지 않는다(전이가 아니라 초기 상태이므로).
    private ResearchNodeState? _previousState;

    private Sequence _unlockTween;

    public ResearchNodeData Node { get; private set; }

    // 연출이 도는 동안에는 Bind가 색·스케일을 덮어쓰지 않는다 - 연구 하나를 끝내면 RefreshAll이
    // 연달아 여러 번 도는데(NodeCompleted · ResourceChanged · RP 변경 · 상세 패널의 onChanged),
    // 그때 값을 다시 칠하면 연출이 첫 프레임에 끊긴다.
    private bool IsUnlockTweenPlaying => _unlockTween != null && _unlockTween.IsActive();

    /// <summary>
    /// 연결선이 붙는 자리(슬롯)를 노드 로컬 좌표로 돌려준다. 카드 전체가 아니라 슬롯인 이유는
    /// 카드가 세로로 길어 카드 테두리에서 선을 끊으면 글자 한복판에서 선이 끝나기 때문이다.
    ///
    /// 레이아웃이 잡힌 뒤에 불러야 한다 - 슬롯의 자리는 프리팹에 직렬화된 값이 아니라
    /// 루트의 VerticalLayoutGroup이 정한다.
    /// </summary>
    public Rect ResolveEdgeAnchorRect()
    {
        var root = (RectTransform)transform;

        if (_slot == null)
        {
            return root.rect;
        }

        RectTransform slotRect = _slot.rectTransform;
        Rect slot = slotRect.rect;

        // 두 좌표계 모두 원점이 각자의 pivot이라, 월드를 거쳐 옮기면 pivot·스케일이 함께 풀린다.
        Vector2 center = root.InverseTransformPoint(slotRect.TransformPoint(slot.center));

        return new Rect(center - slot.size * HALF, slot.size);
    }

    public void Bind(
        ResearchNodeData node,
        ResearchNodeState state,
        Color branchColor,
        Action<ResearchNodeData> onClick)
    {
        Node = node;
        _onClick = onClick;
        EnsureClickListener();

        bool justUnlocked = IsUnlockTransition(state);
        float accentBrightness = AccentBrightnessOf(state);
        float textBrightness = TextBrightnessOf(state);

        // 글자 색은 건드리지 않는다 - 프리팹에 잡아 둔 색을 그대로 쓴다.
        // 상태는 슬롯 색으로 읽히므로 글자까지 상태별로 흔들 필요가 없다.
        if (_nameText != null)
        {
            _nameText.text = StringTable.GetString(node.NameLocKey);
        }

        if (_effectText != null)
        {
            _effectText.text = StringTable.GetString(node.DescriptionLocKey);
        }

        if (_costText != null)
        {
            _costText.text = string.Format(
                StringTable.GetString(ResearchLocKeys.RP_COST),
                node.ResearchPointCost);
        }

        // 해금 연출 중에도 그대로 칠한다 - 슬롯 색을 몰고 가는 트윈이 없다(번쩍임을 뺐다).
        if (_slot != null)
        {
            _slot.color = Scaled(branchColor, accentBrightness, OPAQUE);
        }

        ApplyRank(node.Rank, accentBrightness);

        if (_icon != null)
        {
            // 아이콘은 노드 데이터가 들고 있다. 지정하지 않은 노드는 자리를 통째로 끈다 -
            // 스프라이트 없는 Image는 흰 사각형이 되어 슬롯을 덮는다.
            bool hasIcon = node.Icon != null;
            _icon.gameObject.SetActive(hasIcon);

            if (hasIcon)
            {
                _icon.sprite = node.Icon;

                // 아이콘은 그림이라 색을 입히지 않고 밝기만 글자와 같은 폭으로 낮춘다.
                _icon.color = Scaled(Color.white, textBrightness, OPAQUE);
            }
        }

        // 자물쇠는 완료해야 풀린다. 살 수 있는 상태(Available)여도 아직 산 게 아니라 켜 둔다.
        // 방금 완료된 경우에는 즉시 끄지 않는다 - 연출이 자물쇠를 터뜨리며 끈다.
        if (!justUnlocked && !IsUnlockTweenPlaying)
        {
            ProgressionUnlockEffect.ResetAll(
                BuildEffectTargets(),
                lockVisible: state != ResearchNodeState.Completed);
        }

        if (justUnlocked)
        {
            _unlockTween?.Kill();
            _unlockTween = ProgressionUnlockEffect.Play(BuildEffectTargets());
        }

        _previousState = state;
    }

    private ProgressionUnlockEffect.Targets BuildEffectTargets()
    {
        // Flash는 비워 둔다 - 슬롯이 흰색으로 번쩍였다 돌아오는 연출은 빼기로 했다.
        // 자물쇠·노드 튐·파티클만으로도 해금은 충분히 읽힌다.
        return new ProgressionUnlockEffect.Targets
        {
            Node = transform,
            Lock = _lock,
            Particle = _unlockParticle,
        };
    }

    // 별을 단계 수만큼 켜고, 슬롯과 같은 폭으로 어둡게 한다. 단계가 없는 연구(rank 0)는
    // 묶음째 끈다 - 별 자리만 비워 두면 "단계가 있는데 아직 안 올랐다"로 잘못 읽힌다.
    private void ApplyRank(int rank, float brightness)
    {
        if (_rankRoot != null)
        {
            _rankRoot.SetActive(rank > 0);
        }

        if (_rankStars == null)
        {
            return;
        }

        CacheRankStarColors();

        for (int i = 0; i < _rankStars.Length; i++)
        {
            Image star = _rankStars[i];

            if (star == null)
            {
                continue;
            }

            star.gameObject.SetActive(i < rank);

            Color authored = _rankStarColors[i];
            star.color = Scaled(authored, brightness, authored.a);
        }
    }

    // 별의 원래 색은 프리팹이 정한다 - 상태에 따라 밝기만 낮추므로, 처음 칠하기 전에
    // 저작된 색을 받아 둬야 한다. 안 그러면 낮춘 색을 다시 낮춰 갱신마다 계속 어두워진다.
    private void CacheRankStarColors()
    {
        if (_rankStarColors != null && _rankStarColors.Length == _rankStars.Length)
        {
            return;
        }

        _rankStarColors = new Color[_rankStars.Length];

        for (int i = 0; i < _rankStars.Length; i++)
        {
            _rankStarColors[i] = _rankStars[i] != null ? _rankStars[i].color : Color.white;
        }
    }

    // 클릭 리스너는 한 번만 등록한다. Bind는 트리 갱신마다(연구 완료·자원 변동·주기 전환)
    // 노드 전체에 대해 다시 도는데, 예전에는 그때마다 RemoveAllListeners + 새 람다를 달아
    // 갱신 한 번에 노드 수만큼 클로저가 할당됐다. 콜백은 필드로 받아 최신 값을 쓴다.
    private void EnsureClickListener()
    {
        if (_clickListenerReady || _button == null)
        {
            return;
        }

        _clickListenerReady = true;
        _button.onClick.AddListener(HandleClicked);
    }

    private void HandleClicked()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        _onClick?.Invoke(Node);
    }

    // 자물쇠가 풀리는 그 순간인지. 이미 완료된 노드를 다시 그리는 경우(창 재오픈, 다른 노드
    // 연구로 인한 전체 갱신)에는 상태가 그대로 Completed라 전이가 아니다.
    private bool IsUnlockTransition(ResearchNodeState state) =>
        state == ResearchNodeState.Completed &&
        _previousState.HasValue &&
        _previousState.Value != ResearchNodeState.Completed;

    private static float AccentBrightnessOf(ResearchNodeState state)
    {
        switch (state)
        {
            case ResearchNodeState.Completed:
                return ACCENT_COMPLETED;
            case ResearchNodeState.Available:
                return ACCENT_AVAILABLE;
            case ResearchNodeState.InsufficientResearchPoints:
            case ResearchNodeState.InsufficientResources:
                return ACCENT_SHORT;
            default:
                return ACCENT_LOCKED;
        }
    }

    private static float TextBrightnessOf(ResearchNodeState state)
    {
        switch (state)
        {
            case ResearchNodeState.Completed:
                return TEXT_COMPLETED;
            case ResearchNodeState.Available:
                return TEXT_AVAILABLE;
            case ResearchNodeState.InsufficientResearchPoints:
            case ResearchNodeState.InsufficientResources:
                return TEXT_SHORT;
            default:
                return TEXT_LOCKED;
        }
    }

    // 알파는 그대로 두고 RGB만 어둡게 한다 - Color * float은 알파까지 깎아
    // "밝기 낮추기"와 "투명하게 하기"를 구분할 수 없다(DragonSkillNodePalette와 같은 이유).
    private static Color Scaled(Color color, float brightness, float alpha) =>
        new Color(color.r * brightness, color.g * brightness, color.b * brightness, alpha);
}
