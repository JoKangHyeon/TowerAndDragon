using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목표 목록 한 줄. 문구와 완료 표시만 그린다(값은 UI_TutorialObjectiveWindow가 Setup으로 채운다).
/// 로컬 키를 직접 들고 해석하는 이유는 완료 여부에 따라 문구가 달라지지 않기 때문이다 -
/// 언어가 바뀌면 창이 Setup을 다시 부르므로 이 컴포넌트는 언어 이벤트를 구독하지 않는다.
/// </summary>
public sealed class UI_TutorialObjectiveSlot : MonoBehaviour
{
    [Tooltip("목표 문구.")]
    [SerializeField] private TMP_Text _titleText;

    [Tooltip("완료 표시(체크). 오브젝트가 아니라 그래픽만 끈다 - 오브젝트를 끄면 레이아웃에서 자리까지 " +
             "빠져 미완료 줄과 완료 줄의 들여쓰기가 어긋난다.")]
    [SerializeField] private Graphic _completedMark;

    [Tooltip("미완료 문구 색.")]
    [SerializeField] private Color _pendingColor = Color.white;

    [Tooltip("완료 문구 색. 지우지 않고 흐리게 두어 해낸 것이 남아 보이게 한다.")]
    [SerializeField] private Color _completedColor = Color.gray;

    public void Setup(string titleLocKey, bool isCompleted)
    {
        if (_titleText != null)
        {
            _titleText.text = StringTable.GetString(titleLocKey);
            _titleText.color = isCompleted ? _completedColor : _pendingColor;
        }

        if (_completedMark != null)
        {
            _completedMark.enabled = isCompleted;
        }
    }
}
