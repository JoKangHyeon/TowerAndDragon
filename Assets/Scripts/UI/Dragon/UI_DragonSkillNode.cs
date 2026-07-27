using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬트리 노드 1개의 시각 표현. Bind()가 상태별 색상·라벨·배지를 매 갱신마다 다시 채운다.
public class UI_DragonSkillNode : MonoBehaviour
{
    private const float AVAILABLE_TINT_RATIO = 0.55f;
    private static readonly Color LOCKED_COLOR = new Color(0.08f, 0.06f, 0.05f, 1f);

    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private TextMeshProUGUI _badge;
    [SerializeField] private Button _button;

    public DragonSkillNodeData Node { get; private set; }

    public void Bind(
        DragonSkillNodeData node,
        ProgressionNodeState state,
        Color attributeColor,
        string badgeText,
        Action<DragonSkillNodeData> onClick)
    {
        Node = node;

        if (_label != null)
        {
            _label.text = StringTable.GetString(node.NameLocKey);
        }

        if (_background != null)
        {
            _background.color = ResolveColor(state, attributeColor);
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
            _button.onClick.AddListener(() => onClick?.Invoke(Node));
        }
    }

    private static Color ResolveColor(ProgressionNodeState state, Color attributeColor)
    {
        return state switch
        {
            ProgressionNodeState.Completed => attributeColor,
            ProgressionNodeState.Available => Color.Lerp(LOCKED_COLOR, attributeColor, AVAILABLE_TINT_RATIO),
            _ => LOCKED_COLOR,
        };
    }
}
