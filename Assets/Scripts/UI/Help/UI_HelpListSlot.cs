using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 좌측 목록의 한 줄. 자기 데이터를 스스로 찾지 않고 창에서 주입만 받는다
/// (UI_ClaimListSlot·UI_TutorialObjectiveSlot과 같은 계약).
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

    private HelpEntrySO _entry;
    private Action<HelpEntrySO> _onClick;

    private void Awake()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(HandleClick);
        }
    }

    public void Setup(HelpEntrySO entry, bool isSelected, Action<HelpEntrySO> onClick)
    {
        _entry = entry;
        _onClick = onClick;

        if (_label != null)
        {
            _label.text = entry == null ? string.Empty : StringTable.GetString(entry.TitleLocKey);
        }

        if (_icon != null)
        {
            _icon.sprite = entry == null ? null : entry.Icon;
            _icon.gameObject.SetActive(_icon.sprite != null);
        }

        if (_selectedHighlight != null)
        {
            _selectedHighlight.SetActive(isSelected);
        }
    }

    private void HandleClick()
    {
        if (_entry != null)
        {
            _onClick?.Invoke(_entry);
        }
    }
}
