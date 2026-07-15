using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼의 interactable 상태에 맞춰 버튼 전체(자식 포함)의 투명도를 조절해 활성/비활성을 시각적으로 드러낸다.
/// Unity 기본 Button의 색 전환은 TargetGraphic(배경)만 바꾸므로,
/// 자식 텍스트/아이콘까지 함께 흐려지도록 CanvasGroup의 alpha를 제어한다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public class UI_ButtonInteractableFade : MonoBehaviour
{
    private const float ENABLED_ALPHA = 1f;

    [SerializeField] private float _disabledAlpha = 0.4f;

    private Button _button;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    // interactable을 매 프레임 갱신하는 곳(UI_BuildModeWindow.Update)보다 뒤에 읽도록 LateUpdate에서 동기화한다.
    private void LateUpdate()
    {
        _canvasGroup.alpha = _button.interactable ? ENABLED_ALPHA : _disabledAlpha;
    }
}
