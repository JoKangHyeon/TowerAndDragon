using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가이드 퀘스트 목록을 상시 표시한다. 퀘스트 상태는 GuideQuestController가 들고 있고 이 창은 그리기만 한다.
///
/// <b>정렬이 이 창의 핵심이다.</b> 가이드 퀘스트는 순서를 강제하지 않고 밤도 막지 않으므로,
/// 놓친 항목이 목록 중간에 조용히 묻히면 "1일차 체크리스트를 완전하게 만든다"는 전제가 무너진다.
/// 그래서 지각한 핵심 항목을 늘 맨 위로 끌어올리고 경고색을 준다.
///
/// 가이드 수준이 "없음"이면 창을 통째로 숨긴다. <b>판정은 계속 돌아간다</b> -
/// 나중에 다시 켰을 때 이미 한 일이 체크돼 있어야 하기 때문이다(GuideQuestController).
///
/// Awake에서 자기 자신을 닫지 않으므로 CLAUDE.md의 _isOpen 가드는 필요 없다 -
/// 나중에 접기 버튼을 붙이게 되면 그때는 가드를 반드시 넣어야 한다.
/// </summary>
public sealed class UI_GuideQuestWindow : MonoBehaviour
{
    // 정렬 묶음. 작을수록 위에 온다.
    private const int SORT_OVERDUE = 0;
    private const int SORT_TODAY = 1;
    private const int SORT_PENDING = 2;
    private const int SORT_COMPLETED = 3;

    [SerializeField] private GuideQuestController _controller;

    [Tooltip("가이드 수준을 읽는다. 없으면 항상 보이는 것으로 친다.")]
    [WiringOptional]
    [SerializeField] private SettingsService _settingsService;

    [Tooltip("퀘스트 한 줄 프리팹.")]
    [SerializeField] private UI_GuideQuestSlot _slotPrefab;

    [Tooltip("생성된 줄이 들어갈 부모.")]
    [SerializeField] private Transform _slotContainer;

    [Tooltip("창을 이루는 것 전부(배경·테두리·헤더·목록)를 담은 루트. 보일 퀘스트가 없을 때 통째로 숨긴다 - " +
             "배경 한 장만 끄면 테두리와 모서리 장식이 빈 액자로 남는다.")]
    [SerializeField] private GameObject _windowRoot;

    /// <summary>
    /// 목록의 한 줄이 눌렸다. 설명 카드를 띄우는 일은 이 창이 하지 않는다 -
    /// 창은 그리기만 하고, 무엇을 어떻게 보여줄지는 GuideQuestDetailPresenter가 정한다.
    /// </summary>
    public UnityEngine.Events.UnityEvent<GuideQuestSO> QuestClicked = new();

    private ComponentPool<UI_GuideQuestSlot> _slotPool;

    // 갱신마다 리스트를 새로 만들지 않도록 들고 있는다.
    private readonly List<GuideQuestSO> _sortedQuests = new();

    private const int FIRST_DAY_NUMBER = 1;

    private bool IsHiddenByGuideLevel =>
        _settingsService != null && _settingsService.Guide == GuideLevel.Off;

    /// <summary>
    /// 조언자에게 답하기 전에는 목록을 열지 않는다 - 수준을 고르기도 전에 목록부터 떠 있으면
    /// "안내 없음"을 고르려던 사람에게 이미 안내를 들이민 셈이다.
    ///
    /// 단 2일차부터는 답 여부와 무관하게 연다. 조언자 카드는 1일차에 한 번만 뜨므로,
    /// 그 카드를 답하지 못한 채 놓친 런에서 목록이 영영 잠기는 일이 없어야 한다.
    /// </summary>
    private bool IsIntroPending =>
        !_controller.IsIntroAnswered && _controller.CurrentDayNumber <= FIRST_DAY_NUMBER;

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_GuideQuestSlot>(_slotPrefab, _slotContainer);
    }

    // 구독 직후 현재 값을 한 번 반영한다 - 창이 켜지기 전에 완료된 퀘스트가 이미 있을 수 있고,
    // 그러면 켜지는 순간의 QuestsChanged를 기다릴 것이 없어 빈 목록으로 열린다.
    private void OnEnable()
    {
        if (_controller != null)
        {
            _controller.QuestsChanged.AddListener(Refresh);
        }

        if (_settingsService != null)
        {
            _settingsService.OnGuideLevelChanged += Refresh;
        }

        // 포맷 인자가 없는 문구지만 완료·지각 색을 다시 칠해야 하므로 창이 직접 다시 그린다.
        StringTable.OnLanguageChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (_controller != null)
        {
            _controller.QuestsChanged.RemoveListener(Refresh);
        }

        if (_settingsService != null)
        {
            _settingsService.OnGuideLevelChanged -= Refresh;
        }

        StringTable.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_controller == null || _slotPool == null)
        {
            return;
        }

        BuildSortedQuests();

        for (int i = 0; i < _sortedQuests.Count; i++)
        {
            GuideQuestSO quest = _sortedQuests[i];
            UI_GuideQuestSlot slot = _slotPool.Get(i);
            slot.Setup(
                quest.TitleLocKey,
                _controller.IsCompleted(quest),
                _controller.IsOverdue(quest),
                () => QuestClicked.Invoke(quest));
        }

        _slotPool.DeactivateFrom(_sortedQuests.Count);

        // 보일 것이 없거나, 수준이 "없음"이거나, 아직 조언자에게 답하기 전이면
        // 빈 액자가 떠 있지 않도록 창째로 숨긴다.
        if (_windowRoot != null)
        {
            _windowRoot.SetActive(_sortedQuests.Count > 0 && !IsHiddenByGuideLevel && !IsIntroPending);
        }

        if (_slotContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    /// <summary>
    /// 지각(핵심) → 오늘 → 지난 미완료 → 완료 순으로 줄을 세운다.
    /// 같은 묶음 안에서는 카탈로그 순서를 지킨다 - 매 갱신마다 줄이 뒤바뀌면 읽는 사람이 자리를 잃는다.
    /// </summary>
    private void BuildSortedQuests()
    {
        _sortedQuests.Clear();
        _sortedQuests.AddRange(_controller.VisibleQuests);

        // 카탈로그 순서를 보조 키로 써서 안정 정렬을 만든다(List.Sort는 안정 정렬이 아니다).
        var originalOrder = new Dictionary<GuideQuestSO, int>();

        for (int i = 0; i < _sortedQuests.Count; i++)
        {
            originalOrder[_sortedQuests[i]] = i;
        }

        _sortedQuests.Sort((left, right) =>
        {
            int byGroup = ResolveSortGroup(left).CompareTo(ResolveSortGroup(right));

            if (byGroup != 0)
            {
                return byGroup;
            }

            // 같은 묶음 안에서는 이른 일차부터. 카탈로그 등록 순서에 기대면 나중에 추가한 퀘스트가
            // 일차와 무관하게 뒤로 밀린다(실제로 2일차 항목이 7일차 뒤에 붙었다).
            int byDay = left.RecommendedDay.CompareTo(right.RecommendedDay);
            return byDay != 0 ? byDay : originalOrder[left].CompareTo(originalOrder[right]);
        });
    }

    private int ResolveSortGroup(GuideQuestSO quest)
    {
        if (_controller.IsCompleted(quest))
        {
            return SORT_COMPLETED;
        }

        if (_controller.IsOverdue(quest))
        {
            return SORT_OVERDUE;
        }

        return quest.RecommendedDay == _controller.CurrentDayNumber ? SORT_TODAY : SORT_PENDING;
    }
}
