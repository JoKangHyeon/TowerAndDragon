using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 좌측 목록의 한 줄. 자기 데이터를 스스로 찾지 않고 창에서 주입만 받는다
/// (UI_ClaimListSlot·UI_TutorialObjectiveSlot과 같은 계약).
///
/// 도움말 탭·적 정보 탭이 이 슬롯 하나를 함께 쓴다 - 두 탭의 항목 타입(HelpEntrySO/MonsterData)이
/// 다르므로, 주입은 항목을 통째로 받지 않고 이미 뽑아낸 표시용 값(제목·아이콘)만 받는다.
/// 클릭 콜백도 항목이 아니라 UI_HelpWindow._rows의 줄 번호를 돌려준다 - 창이 그 인덱스로
/// 자기 목록에서 실제 항목을 되찾는다.
/// </summary>
public sealed class UI_HelpListSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Button _button;

    [Tooltip("항목 아이콘. 아이콘이 없는 항목에서는 오브젝트째 꺼진다.")]
    [WiringOptional]
    [SerializeField] private Image _icon;

    [Tooltip("지금 고른 줄임을 표시하는 배경. 없으면 선택 표시가 생략된다.")]
    [WiringOptional]
    [SerializeField] private GameObject _selectedHighlight;

    [Tooltip("아직 도감에서 펼쳐 보지 않은 항목임을 알리는 붉은 점. 없으면 표시가 생략된다.")]
    [WiringOptional]
    [SerializeField] private GameObject _unviewedDot;

    private int _rowIndex;
    private Action<int> _onClick;

    private void Awake()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(HandleClick);
        }
    }

    /// <summary>
    /// 이 줄의 전 상태를 한 번에 덮는다. 슬롯은 ComponentPool이 재사용하고 풀은 상태를 초기화하지
    /// 않으므로(활성화만 한다), 주입을 여러 메서드로 갈라 두면 한쪽을 잊은 경로에서 이전 항목의
    /// 표시가 그대로 남는다. 그래서 SetUnviewed 같은 별도 메서드를 두지 않고 파라미터로 받는다.
    /// </summary>
    public void Setup(
        string titleLocKey, Sprite icon, bool isSelected, bool isUnviewed, int rowIndex, Action<int> onClick)
    {
        _rowIndex = rowIndex;
        _onClick = onClick;

        if (_label != null)
        {
            _label.text = string.IsNullOrEmpty(titleLocKey) ? string.Empty : StringTable.GetString(titleLocKey);
        }

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null);
        }

        if (_selectedHighlight != null)
        {
            _selectedHighlight.SetActive(isSelected);
        }

        if (_unviewedDot != null)
        {
            _unviewedDot.SetActive(isUnviewed);
        }
    }

    private void HandleClick()
    {
        _onClick?.Invoke(_rowIndex);
    }
}
