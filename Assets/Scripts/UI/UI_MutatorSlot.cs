using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>새 게임 + 진입 창의 뮤테이터 한 줄.
///
/// 항목은 토글이 아니라 <b>단계 선택기</b>다 - "없음 / 1단계 / 2단계 …" 중 하나를 고른다.
/// 단계 칸의 개수는 <see cref="RunMutatorSO.TierCount"/>에서 나오며 <b>코드에 박지 않는다.</b>
/// 규칙 계열(단계 1개)은 자연히 "없음 / 1단계" 두 칸이 되어 켜짐/꺼짐 토글처럼 읽힌다 -
/// 규칙 계열용 별도 UI를 만들지 않는 것이 설계다.
///
/// 판정은 하나도 하지 않는다. 칸을 누르면 인덱스와 단계만 콜백으로 올리고, 창이
/// <see cref="NewGamePlusSelection.TrySetTier"/>로 처리한 뒤 다시 그리게 한다
/// (UI_BabyDragonListSlot이 배치·카메라를 직접 하지 않는 것과 같은 분담).
/// </summary>
public sealed class UI_MutatorSlot : MonoBehaviour
{
    [Tooltip("뮤테이터 이름. key는 데이터가 갖고 있어 코드가 SetKey로 넣는다.")]
    [SerializeField] private LocalizedText _nameLabel;

    [Tooltip("현재 단계의 설명. 단계를 바꾸면 즉시 갱신된다.")]
    [SerializeField] private TMP_Text _descText;

    [Tooltip("현재 단계의 난이도 점수.")]
    [SerializeField] private TMP_Text _scoreText;

    [Header("단계 선택기")]
    [Tooltip("선택 해제 칸(\"없음\"). 단계가 아니라서 복제 대상이 아니고 여기 고정으로 둔다.")]
    [SerializeField] private Button _noneButton;

    [Tooltip("\"없음\"이 선택돼 있음을 보여주는 표식.")]
    [WiringOptional]
    [SerializeField] private GameObject _noneSelectedMark;

    [Tooltip("단계 칸 프리팹. 뮤테이터의 단계 수만큼 복제한다.")]
    [SerializeField] private UI_MutatorTierButton _tierButtonPrefab;

    [Tooltip("복제된 단계 칸이 들어갈 부모.")]
    [SerializeField] private Transform _tierButtonContainer;

    [Header("색")]
    [Tooltip("단계를 켠 항목의 설명·점수 색.")]
    [SerializeField] private Color _selectedColor = Color.white;

    [Tooltip("켜지 않은 항목의 설명·점수 색. 켰을 때 붙는 1단계 값을 흐리게 미리 보여준다.")]
    [SerializeField] private Color _unselectedColor = new Color(0.62f, 0.62f, 0.62f, 1f);

    private ComponentPool<UI_MutatorTierButton> _tierButtonPool;

    private NewGamePlusSelection _selection;

    private int _index;

    private Action<int, int> _onTierPicked;

    private void Awake()
    {
        _tierButtonPool = new ComponentPool<UI_MutatorTierButton>(_tierButtonPrefab, _tierButtonContainer);

        if (WiringGuard.Require(_noneButton, nameof(_noneButton), this))
        {
            _noneButton.onClick.AddListener(HandleNoneClicked);
        }
    }

    private void OnDestroy()
    {
        if (_noneButton != null)
        {
            _noneButton.onClick.RemoveListener(HandleNoneClicked);
        }
    }

    /// <summary>이 줄이 표시할 항목을 지정하고 그린다. 창이 갱신마다 호출한다.</summary>
    public void Bind(NewGamePlusSelection selection, int index, Action<int, int> onTierPicked)
    {
        _selection = selection;
        _index = index;
        _onTierPicked = onTierPicked;

        Render();
    }

    public void Render()
    {
        if (_selection == null || _tierButtonPool == null)
        {
            return;
        }

        RunMutatorSO mutator = _selection.MutatorAt(_index);
        bool isSelected = _selection.IsSelected(_index);
        Color color = isSelected ? _selectedColor : _unselectedColor;

        if (_nameLabel != null)
        {
            _nameLabel.SetKey(mutator.NameLocKey);
        }

        if (_descText != null)
        {
            _descText.text = StringTable.GetString(_selection.DescLocKeyAt(_index));
            _descText.color = color;
        }

        if (_scoreText != null)
        {
            _scoreText.text = string.Format(
                StringTable.GetString(NewGamePlusLocKeys.SCORE_FORMAT),
                _selection.ShownScoreAt(_index));
            _scoreText.color = color;
        }

        if (_noneSelectedMark != null)
        {
            _noneSelectedMark.SetActive(!isSelected);
        }

        RenderTierButtons(mutator);
    }

    // 칸 수를 데이터에서 만든다 - 3개 하드코딩 금지. 남는 풀 항목은 접는다
    // (UI_LoadGameWindow.Refresh와 같은 ComponentPool 관용구).
    private void RenderTierButtons(RunMutatorSO mutator)
    {
        int tierCount = mutator.TierCount;
        int currentTier = _selection.TierAt(_index);

        for (int i = 0; i < tierCount; i++)
        {
            int tier = RunMutatorSO.FIRST_TIER + i;
            _tierButtonPool.Get(i).Setup(tier, tier == currentTier, HandleTierClicked);
        }

        _tierButtonPool.DeactivateFrom(tierCount);
    }

    private void HandleNoneClicked()
    {
        _onTierPicked?.Invoke(_index, RunMutatorSO.UNSELECTED_TIER);
    }

    private void HandleTierClicked(int tier)
    {
        _onTierPicked?.Invoke(_index, tier);
    }
}
