using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬트리 노드 1개의 시각 표현. Bind()가 상태별 색상·라벨·배지를 매 갱신마다 다시 채운다.
public class UI_DragonSkillNode : MonoBehaviour
{
    private const float AVAILABLE_TINT_RATIO = 0.55f;
    private const float DEEP_LOCKED_ALPHA = 0.55f;
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
            _button.onClick.AddListener(() =>
            {
                SoundManager.Play(SoundId.UiButtonClick);
                onClick?.Invoke(Node);
            });
        }
    }

    // 자원 부족(InsufficientResources)은 "선행·게이트는 충족했고 자원만 모자란" 상태라
    // 불투명하게 두고, 그 외 잠금 사유(선행·게이트·낮밤·무효)는 더 멀리 잠겨 있음을
    // 나타내도록 살짝 투명하게 처리한다.
    private static Color ResolveColor(ProgressionNodeState state, Color attributeColor)
    {
        switch (state)
        {
            case ProgressionNodeState.Completed:
                return attributeColor;
            case ProgressionNodeState.Available:
                return Color.Lerp(LOCKED_COLOR, attributeColor, AVAILABLE_TINT_RATIO);
            case ProgressionNodeState.InsufficientResources:
                return LOCKED_COLOR;
            default:
                Color deepLocked = LOCKED_COLOR;
                deepLocked.a = DEEP_LOCKED_ALPHA;
                return deepLocked;
        }
    }
}
