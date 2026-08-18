using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 스킬트리 노드 1개의 시각 표현.
//
// 생김새는 두 단계로 나뉜다.
//  - ApplyLayout: 노드 종류·방향으로 정해지는 배치(크기·테두리 두께·라벨 위치). 빌드 때 한 번만 부른다.
//  - Bind: 상태에 따라 매 갱신마다 바뀌는 것(라벨·뱃지·색·클릭 핸들러).
//
// 어미용 노드와 새끼용 노드는 프리팹이 아니라 DragonSkillNodeStyleTable의 규격으로 갈린다.
public class UI_DragonSkillNode : MonoBehaviour
{
    // 지름 → 반지름처럼 "절반"을 쓰는 자리가 여러 곳이라 이름을 붙여 둔다.
    private const float HALF = 0.5f;

    [Header("Visual Parts")]
    [FormerlySerializedAs("_background")]
    [Tooltip("바깥 테두리. 어떤 상태에서도 속성 색을 유지한다.")]
    [SerializeField] private Image _ring;

    [Tooltip("테두리 안쪽 채움. 어미용은 속성 색으로 차고, 새끼용은 비어 있다.")]
    [SerializeField] private Image _fill;

    [Tooltip("새끼용 노드에만 켜지는 알 아이콘.")]
    [SerializeField] private Image _icon;

    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private TextMeshProUGUI _badge;
    [SerializeField] private Button _button;

    public DragonSkillNodeData Node { get; private set; }

    /// <summary>
    /// 노드 종류로 정해지는 배치를 적용한다. <paramref name="outwardDirection"/>은 트리 중심에서
    /// 이 노드로 향하는 방향으로, 라벨·뱃지를 그 바깥쪽으로 밀어내는 데 쓴다.
    /// </summary>
    public void ApplyLayout(DragonNodeKind kind, Vector2 outwardDirection)
    {
        DragonSkillNodeStyle style = DragonSkillNodeStyleTable.For(kind);
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(style.Diameter, style.Diameter);

        if (_fill != null)
        {
            // Fill은 (0,0)~(1,1)로 늘어나 있어 offset만 주면 테두리 두께만큼 안으로 들어간다.
            RectTransform fillRect = _fill.rectTransform;
            fillRect.offsetMin = new Vector2(style.RingThickness, style.RingThickness);
            fillRect.offsetMax = new Vector2(-style.RingThickness, -style.RingThickness);
        }

        if (_icon != null)
        {
            float iconDiameter = style.Diameter * DragonSkillNodeStyleTable.ICON_DIAMETER_RATIO;
            _icon.rectTransform.sizeDelta = new Vector2(iconDiameter, iconDiameter);
        }

        // 라벨·뱃지는 노드 바깥쪽(중심에서 멀어지는 방향)으로 밀어낸다. 화면 아래로 고정하면
        // 위쪽을 향한 속성 갈래에서 라벨이 한 단계 안쪽 노드 위에 겹친다.
        Vector2 outward = outwardDirection.sqrMagnitude > 0f ? outwardDirection.normalized : Vector2.up;
        float radius = style.Diameter * HALF;

        // 테두리 바깥으로 라벨 → 뱃지 순으로 쌓는다. 라벨이 없으면 뱃지가 그 자리를 차지한다.
        float stacked = radius;

        if (_label != null)
        {
            _label.fontSize = style.LabelFontSize;
            float labelHeight = _label.rectTransform.sizeDelta.y;
            _label.rectTransform.anchoredPosition =
                outward * (stacked + DragonSkillNodeStyleTable.LABEL_GAP + labelHeight * HALF);
            stacked += DragonSkillNodeStyleTable.LABEL_GAP + labelHeight;
        }

        if (_badge != null)
        {
            float badgeHeight = _badge.rectTransform.sizeDelta.y;
            _badge.rectTransform.anchoredPosition =
                outward * (stacked + DragonSkillNodeStyleTable.BADGE_GAP + badgeHeight * HALF);
        }
    }

    public void Bind(
        DragonSkillNodeData node,
        ProgressionNodeState state,
        Color attributeColor,
        Sprite centerIcon,
        string badgeText,
        Action<DragonSkillNodeData> onClick)
    {
        Node = node;
        DragonSkillNodeStyle style = DragonSkillNodeStyleTable.For(node.Kind);

        if (_label != null)
        {
            _label.text = StringTable.GetString(node.NameLocKey);
            _label.color = DragonSkillNodePalette.LabelColor(state);
        }

        if (_ring != null)
        {
            _ring.color = DragonSkillNodePalette.RingColor(state, attributeColor);
        }

        if (_fill != null)
        {
            _fill.color = DragonSkillNodePalette.FillColor(state, attributeColor, style.IsKin);
        }

        if (_icon != null)
        {
            bool hasIcon = centerIcon != null;
            _icon.gameObject.SetActive(hasIcon);
            _icon.sprite = centerIcon;
            _icon.color = DragonSkillNodePalette.IconColor(state);
        }

        if (_badge != null)
        {
            bool hasBadge = !string.IsNullOrEmpty(badgeText);
            _badge.gameObject.SetActive(hasBadge);
            _badge.text = badgeText;
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
}
