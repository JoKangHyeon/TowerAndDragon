using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가이드 퀘스트 목록 한 줄. 문구와 완료·지각 표시를 그리고, <b>눌리면 알린다</b>
/// (값과 클릭 처리는 UI_GuideQuestWindow가 Setup으로 넘긴다).
///
/// 튜토리얼 목표 줄(UI_TutorialObjectiveSlot)과 다른 점이 둘이다.
/// <b>지각 색</b> - 가이드 퀘스트는 강제하지 않는 대신 놓친 것을 계속 보여주는 것이 존재 이유다.
/// <b>클릭</b> - 자세한 설명은 카드로 밀어붙이지 않고 여기서 눌러 꺼내 본다. 1일차 항목이 아홉 개라
/// 열릴 때마다 카드를 띄우면 확인 버튼만 아홉 번 누르는 튜토리얼이 된다.
///
/// 언어가 바뀌면 창이 Setup을 다시 부르므로 이 컴포넌트는 언어 이벤트를 구독하지 않는다.
/// </summary>
public sealed class UI_GuideQuestSlot : MonoBehaviour
{
    [Tooltip("퀘스트 문구.")]
    [SerializeField] private TMP_Text _titleText;

    [Tooltip("완료 체크 표시. 해낸 항목은 목록에서 아예 빠지므로 지금은 늘 꺼 둔다 - " +
             "프리팹에서 지우지 않는 것은 다시 \"흐리게 남기기\"로 되돌릴 여지를 두기 위해서다.")]
    [WiringOptional]
    [SerializeField] private Graphic _completedMark;

    [Tooltip("줄 전체를 덮는 버튼. 누르면 그 퀘스트의 설명 카드를 연다.")]
    [SerializeField] private Button _button;

    [Tooltip("아직 남은 항목의 문구 색.")]
    [SerializeField] private Color _pendingColor = Color.white;

    [Tooltip("그날 안에 끝냈어야 하는데 못 한 항목의 색. 목록에서 눈에 띄어야 한다.")]
    [SerializeField] private Color _overdueColor = new Color(1f, 0.45f, 0.35f);

    public void Setup(string titleLocKey, bool isOverdue, Action onClicked)
    {
        if (_titleText != null)
        {
            _titleText.text = StringTable.GetString(titleLocKey);
            _titleText.color = isOverdue ? _overdueColor : _pendingColor;
        }

        if (_completedMark != null)
        {
            _completedMark.enabled = false;
        }

        if (_button == null)
        {
            return;
        }

        // 줄은 재사용되므로(ComponentPool) 이전 퀘스트의 콜백이 남지 않도록 매번 비우고 다시 건다.
        _button.onClick.RemoveAllListeners();

        if (onClicked != null)
        {
            _button.onClick.AddListener(() => onClicked());
        }
    }
}
