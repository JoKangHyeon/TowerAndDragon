using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD 버튼에 "제목 + 단축키" 툴팁을 붙인다. 리바인딩해도 항상 최신 키를 보여준다 -
/// SettingsService 이벤트를 구독하는 대신, UI_TooltipTrigger의 콘텐츠 제공자로 등록해
/// 호버할 때마다 InputBindingLabel로 실제 바인딩을 다시 읽는다.
/// </summary>
public class UI_ShortcutTooltip : MonoBehaviour
{
    // stringtable — tooltip_shortcut_body : "단축키: {0}"
    private const string SHORTCUT_BODY_LOC_KEY = "tooltip_shortcut_body";

    [SerializeField] private UI_TooltipTrigger _trigger;

    [Tooltip("단축키 표기의 출처. 액션이 비어 있으면 단축키 문구 없이 제목만 보여준다.")]
    [SerializeField] private InputActionReference _action;

    [Tooltip("툴팁 제목의 스트링테이블 key.")]
    [SerializeField] private string _titleLocKey;

    private void Awake()
    {
        if (!WiringGuard.Require(_trigger, nameof(_trigger), this))
        {
            return;
        }

        _trigger.SetContentProvider(BuildContent);
    }

    private TooltipContent BuildContent()
    {
        string title = StringTable.GetString(_titleLocKey);
        string keyLabel = _action != null ? InputBindingLabel.Resolve(_action.action) : string.Empty;

        string body = string.IsNullOrEmpty(keyLabel)
            ? string.Empty
            : string.Format(StringTable.GetString(SHORTCUT_BODY_LOC_KEY), keyLabel);

        return new TooltipContent(title, body);
    }
}
