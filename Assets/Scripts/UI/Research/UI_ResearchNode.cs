using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 1개의 시각 표현. 시각화 HTML(.node)과 동일하게 이름·효과·RP코스트 3줄을 보여준다.
// Bind()가 상태별 색상·텍스트를 매 갱신마다 다시 채운다 - UI_DragonSkillNode와 같은 구조.
public class UI_ResearchNode : MonoBehaviour
{
    private const float AVAILABLE_TINT_RATIO = 0.55f;
    private const float DEEP_LOCKED_ALPHA = 0.55f;
    private static readonly Color LOCKED_COLOR = new Color(0.106f, 0.118f, 0.145f, 1f);

    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private Button _button;

    [Tooltip("진입(루트) 노드 강조 테두리. 선행이 없는 노드에서만 켠다.")]
    [SerializeField] private GameObject _rootHighlight;

    public ResearchNodeData Node { get; private set; }

    public void Bind(
        ResearchNodeData node,
        ResearchNodeState state,
        Color branchColor,
        Action<ResearchNodeData> onClick)
    {
        Node = node;

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

        if (_background != null)
        {
            _background.color = ResolveColor(state, branchColor);
        }

        if (_rootHighlight != null)
        {
            _rootHighlight.SetActive(node.Prerequisites.Count == 0);
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

    // 코스트만 모자란 상태(RP·자원 부족)는 "선행·티어는 이미 통과했다"는 뜻이라 불투명하게 두고,
    // 그보다 멀리 잠긴 사유(티어·선행·낮밤·무효)는 살짝 투명하게 처리한다.
    private static Color ResolveColor(ResearchNodeState state, Color branchColor)
    {
        switch (state)
        {
            case ResearchNodeState.Completed:
                return branchColor;
            case ResearchNodeState.Available:
                return Color.Lerp(LOCKED_COLOR, branchColor, AVAILABLE_TINT_RATIO);
            case ResearchNodeState.InsufficientResearchPoints:
            case ResearchNodeState.InsufficientResources:
                return LOCKED_COLOR;
            default:
                Color deepLocked = LOCKED_COLOR;
                deepLocked.a = DEEP_LOCKED_ALPHA;
                return deepLocked;
        }
    }
}
