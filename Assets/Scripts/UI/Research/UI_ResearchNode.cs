using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 1개의 시각 표현. 기획 블랙보드(Docs/연구트리_블랙보드.html)의 .node 카드를 옮긴 것이다.
//
// 블랙보드 .node 는 이렇게 생겼다.
//   background #2d2d2d / border 2px #444 / border-left 4px 갈래색 / radius 8 / shadow 0 4px 6px
//   내부(padding 12): [6px 갈래 색 띠] → 주기 N → 이름 → 설명 → 하단 한 줄
//
// 프리팹에는 배경 Image·이름·설명·코스트만 있어서, 나머지 겹(그림자·채움·테두리·색 띠·주기)은
// 코드로 만든다. 트리 자체가 런타임 생성이라 관례가 같고, 프리팹 구조를 손대지 않아도 된다.
//
// 겹치는 순서가 중요하다 - 자식은 부모 Image보다 위에 그려지므로, 루트 Image는 투명하게 두고
// 그림자 → 채움 → 테두리 → 색 띠 → (프리팹의 좌측 띠·글자) 순으로 자식을 깐다.
public class UI_ResearchNode : MonoBehaviour
{
    // 블랙보드 팔레트.
    private static readonly Color CARD_COLOR = new Color(0.176f, 0.176f, 0.176f, 1f);   // #2d2d2d
    private static readonly Color BORDER_COLOR = new Color(0.267f, 0.267f, 0.267f, 1f); // #444
    private static readonly Color SHADOW_COLOR = new Color(0f, 0f, 0f, 0.30f);          // shadow .3
    private static readonly Color NAME_COLOR = new Color(1f, 1f, 1f, 1f);
    private static readonly Color DESC_COLOR = new Color(0.690f, 0.690f, 0.690f, 1f);   // #b0b0b0
    private static readonly Color TIER_COLOR = new Color(0.533f, 0.533f, 0.533f, 1f);   // #888
    private static readonly Color COST_COLOR = new Color(0.533f, 0.533f, 0.533f, 1f);

    // 상태별 밝기. 갈래 띠는 상태를 크게 벌려 표현하고, 글자는 "읽을 수 있는 하한"을 지킨다 -
    // 글자까지 같이 어둡게 하면 화면의 대부분인 잠긴 노드를 아예 못 읽는다(실측).
    private const float ACCENT_COMPLETED = 1f;
    private const float ACCENT_AVAILABLE = 0.90f;
    private const float ACCENT_SHORT = 0.65f;
    private const float ACCENT_LOCKED = 0.42f;

    private const float TEXT_COMPLETED = 1f;
    private const float TEXT_AVAILABLE = 0.95f;
    private const float TEXT_SHORT = 0.80f;
    private const float TEXT_LOCKED = 0.62f;

    // 완료한 노드는 카드 배경에도 갈래 색을 옅게 섞어 "칠해졌다"는 느낌을 준다.
    private const float COMPLETED_CARD_TINT = 0.22f;

    // 블랙보드 .node 의 padding 12 / .node-branch-indicator 6px(+ margin-bottom 8).
    private const float CARD_PADDING = 12f;
    private const float BRANCH_BAR_HEIGHT = 6f;
    private const float BRANCH_BAR_MARGIN_BOTTOM = 8f;

    // box-shadow 0 4px 6px - 아래로 4px 내려간다.
    private const float SHADOW_OFFSET_Y = -4f;

    // 주기 라벨(.node-tier) 규격.
    private const float TIER_LABEL_HEIGHT = 14f;
    private const float TIER_LABEL_FONT_SIZE = 10f;

    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _button;

    [Tooltip("좌측 세로 갈래 띠(블랙보드의 border-left). 갈래 색을 항상 띄운다.")]
    [SerializeField] private GameObject _rootHighlight;

    private Image _shadow;
    private Image _cardFill;
    private Image _cardBorder;
    private Image _branchBar;
    private Image _branchAccent;
    private TextMeshProUGUI _tierText;
    private bool _decorationsReady;

    public ResearchNodeData Node { get; private set; }

    public void Bind(
        ResearchNodeData node,
        ResearchNodeState state,
        Color branchColor,
        Action<ResearchNodeData> onClick)
    {
        Node = node;
        EnsureDecorations();

        float accentBrightness = AccentBrightnessOf(state);
        float textBrightness = TextBrightnessOf(state);
        Color accent = Scaled(branchColor, accentBrightness, 1f);

        if (_nameText != null)
        {
            _nameText.text = StringTable.GetString(node.NameLocKey);
            _nameText.color = Scaled(NAME_COLOR, textBrightness, 1f);
        }

        if (_effectText != null)
        {
            _effectText.text = StringTable.GetString(node.DescriptionLocKey);
            _effectText.color = Scaled(DESC_COLOR, textBrightness, 1f);
        }

        if (_costText != null)
        {
            _costText.text = string.Format(
                StringTable.GetString(ResearchLocKeys.RP_COST),
                node.ResearchPointCost);
            _costText.color = Scaled(COST_COLOR, textBrightness, 1f);
        }

        if (_tierText != null)
        {
            _tierText.text = string.Format(
                StringTable.GetString(ResearchLocKeys.TIER_LABEL), node.Tier);
            _tierText.color = Scaled(TIER_COLOR, textBrightness, 1f);
        }

        if (_cardFill != null)
        {
            // 카드는 항상 불투명하다 - 반투명하게 두면 뒤가 비쳐 글씨를 못 읽는다(실측).
            Color card = state == ResearchNodeState.Completed
                ? Color.Lerp(CARD_COLOR, branchColor, COMPLETED_CARD_TINT)
                : CARD_COLOR;
            card.a = 1f;
            _cardFill.color = card;
        }

        if (_branchBar != null)
        {
            _branchBar.color = accent;
        }

        if (_branchAccent != null)
        {
            _branchAccent.color = accent;
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() =>
            {
                SoundManager.Play(SoundId.UiButtonClick);
                onClick?.Invoke(Node);
            });
        }
    }

    private void EnsureDecorations()
    {
        if (_decorationsReady)
        {
            return;
        }

        _decorationsReady = true;

        // 루트 Image는 클릭 판정만 맡고 그림은 자식들이 그린다(자식이 부모보다 위에 그려지므로).
        if (_background != null)
        {
            _background.color = Color.clear;
            _background.sprite = null;
        }

        float blur = ResearchTreeSprites.CardShadowBlur;

        _shadow = CreateLayer("CardShadow", ResearchTreeSprites.CardShadow, SHADOW_COLOR,
            new Vector2(-blur, -blur + SHADOW_OFFSET_Y), new Vector2(blur, blur + SHADOW_OFFSET_Y));

        _cardFill = CreateLayer("CardFill", ResearchTreeSprites.CardFill, CARD_COLOR,
            Vector2.zero, Vector2.zero);

        _cardBorder = CreateLayer("CardBorder", ResearchTreeSprites.CardOutline, BORDER_COLOR,
            Vector2.zero, Vector2.zero);

        _branchBar = CreateBranchBar();
        _tierText = CreateTierLabel();

        // 좌측 띠는 프리팹의 RootHighlight를 그대로 쓴다(같은 자리·같은 두께).
        // 선행 없는 노드만 켜던 동작은 없앴다 - 갈래 색을 항상 띄우는 쪽이 트리를 읽는 데 더 중요하고,
        // 진입 노드는 어차피 T1 행에 모여 있어 위치로 구분된다.
        if (_rootHighlight != null)
        {
            _rootHighlight.SetActive(true);
            _branchAccent = _rootHighlight.GetComponent<Image>();
        }
    }

    // 카드 전체를 덮는 겹을 만든다. offsetMin/Max로 부모보다 키우거나 줄인다.
    private Image CreateLayer(string name, Sprite sprite, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)layer.transform;
        rect.SetParent(transform, false);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        // 만드는 순서대로 뒤에서 앞으로 쌓이도록 항상 마지막에 놓되, 프리팹 자식(글자)보다는 뒤여야 한다.
        rect.SetSiblingIndex(_layerCount++);

        var image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private int _layerCount;

    private Image CreateBranchBar()
    {
        var barObject = new GameObject("BranchBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)barObject.transform;
        rect.SetParent(transform, false);

        // 블랙보드처럼 카드 안쪽 여백(padding 12) 안에 가로로 꽉 채운다.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(CARD_PADDING, -CARD_PADDING - BRANCH_BAR_HEIGHT);
        rect.offsetMax = new Vector2(-CARD_PADDING, -CARD_PADDING);
        rect.SetSiblingIndex(_layerCount++);

        var image = barObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // 이름 위의 "주기 N" 한 줄(.node-tier). 프리팹에 없어 이름 텍스트를 복제해 만든다 -
    // 폰트 에셋·머티리얼을 코드에서 새로 물리지 않아도 된다.
    private TextMeshProUGUI CreateTierLabel()
    {
        if (_nameText == null)
        {
            return null;
        }

        TextMeshProUGUI label = Instantiate(_nameText, transform);
        label.name = "Tier";
        label.fontSize = TIER_LABEL_FONT_SIZE;
        label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false;

        RectTransform source = _nameText.rectTransform;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(source.sizeDelta.x, TIER_LABEL_HEIGHT);

        // 색 띠 바로 아래(블랙보드의 indicator margin-bottom 만큼 띄운다).
        rect.anchoredPosition = new Vector2(
            source.anchoredPosition.x,
            -(CARD_PADDING + BRANCH_BAR_HEIGHT + BRANCH_BAR_MARGIN_BOTTOM));

        return label;
    }

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
