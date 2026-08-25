using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>뮤테이터 한 항목의 단계 칸 하나("1단계"·"2단계"…).
///
/// 이 버튼은 <b>데이터가 정한 개수만큼 복제된다</b> - 단계 수가 뮤테이터마다 다르므로
/// (<see cref="RunMutatorSO.TierCount"/>) 칸 수를 프리팹에 박아 둘 수 없다.
/// "없음" 칸은 단계가 아니라 선택 해제라서 이 프리팹이 아니라 슬롯에 고정으로 놓는다.
/// </summary>
public sealed class UI_MutatorTierButton : MonoBehaviour
{
    [Tooltip("칸 전체를 덮는 Button - 보통 이 오브젝트 자신의 Button.")]
    [SerializeField] private Button _button;

    [Tooltip("\"{0}단계\" 라벨. 번호가 런타임 값이라 LocalizedText를 붙이지 않고 코드가 채운다.")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("지금 고른 단계임을 보여주는 표식. 비워 두면 선택 표시가 색으로만 남는다.")]
    [WiringOptional]
    [SerializeField] private GameObject _selectedMark;

    [Tooltip("선택된 칸의 라벨 색.")]
    [SerializeField] private Color _selectedColor = Color.white;

    [Tooltip("선택되지 않은 칸의 라벨 색.")]
    [SerializeField] private Color _unselectedColor = new Color(0.62f, 0.62f, 0.62f, 1f);

    private int _tier;

    // 풀에서 재사용되므로 Setup마다 새로 받는다(UI_BabyDragonListSlot과 같은 방식).
    private Action<int> _onClicked;

    private void Awake()
    {
        if (WiringGuard.Require(_button, nameof(_button), this))
        {
            _button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Setup(int tier, bool isSelected, Action<int> onClicked)
    {
        _tier = tier;
        _onClicked = onClicked;

        if (_label != null)
        {
            _label.text = string.Format(
                StringTable.GetString(NewGamePlusLocKeys.TIER_LABEL), tier);
            _label.color = isSelected ? _selectedColor : _unselectedColor;
        }

        if (_selectedMark != null)
        {
            _selectedMark.SetActive(isSelected);
        }
    }

    private void HandleClicked()
    {
        _onClicked?.Invoke(_tier);
    }
}
