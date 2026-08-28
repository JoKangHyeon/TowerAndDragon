using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 도감 창. 좌측에 지금까지 해금한 항목 목록, 우측에 고른 항목의 본문을 띄운다.
/// 골격은 UI_ResearchWindow와 같다 - blocker 루트(전체화면 dim Image)에 붙어 바깥 클릭 닫기와
/// IExclusiveMode 조정을 겸한다.
///
/// 탭 두 개(도움말 / 적 정보)가 같은 좌측 목록·우측 본문을 나눠 쓴다. 항목 타입이 서로 달라도
/// (HelpEntrySO vs MonsterData) 목록·상세 렌더 경로는 하나만 둔다 - 두 벌로 늘리면
/// MarkSelectedViewed가 경고하는 재진입 파손 지점도 두 배가 된다.
///
/// <b>잠긴 항목은 목록에 넣지 않는다.</b> 회색 실루엣을 깔면 아직 만나지 않은 시스템의 존재를
/// 미리 알려주게 되어 "플레이하면서 하나씩 해금되는 느낌"이라는 요구와 정면으로 어긋난다.
/// 대신 헤더의 "해금 N / 전체 M" 한 줄이 "더 있다"는 정보를 대신한다.
/// </summary>
public sealed class UI_HelpWindow : MonoBehaviour, IExclusiveMode
{
    // 탭 - 좌측 목록·우측 본문이 이 값에 따라 어느 카탈로그를 보여줄지만 바뀐다.
    private enum CodexTab
    {
        Help,
        Monster,
    }

    // 도움말·적 정보 두 탭의 항목을 한 목록 렌더 경로로 그리기 위한 공통 모양(직렬화 대상 아님).
    // Entry/Monster는 탭에 따라 둘 중 하나만 채워진다 - RenderDetail이 어느 쪽이 채워졌는지로 분기한다.
    private readonly struct CodexRow
    {
        public readonly string Id; // HelpProfile 기록 키(EntryId 또는 MonsterData.NameLocKey)
        public readonly string TitleLocKey;
        public readonly MonsterIcon Icon;
        public readonly string CategoryLocKey;
        public readonly HelpEntrySO Entry;
        public readonly MonsterData Monster;

        private CodexRow(
            string id, string titleLocKey, MonsterIcon icon, string categoryLocKey,
            HelpEntrySO entry, MonsterData monster)
        {
            Id = id;
            TitleLocKey = titleLocKey;
            Icon = icon;
            CategoryLocKey = categoryLocKey;
            Entry = entry;
            Monster = monster;
        }

        public static CodexRow ForHelp(HelpEntrySO entry) => new CodexRow(
            entry.EntryId,
            entry.TitleLocKey,
            new MonsterIcon(entry.Icon, Color.white),
            HelpLocKeys.CategoryLocKey(entry.Category),
            entry,
            null);

        public static CodexRow ForMonster(in MonsterCodexCatalogSO.Row row) => new CodexRow(
            row.Data.NameLocKey,
            row.Data.NameLocKey,
            row.Icon,
            HelpLocKeys.MonsterCategoryLocKey(row.Category),
            null,
            row.Data);
    }

    // 갈래별로 나눠 훑어 카탈로그 목록 순서를 갈래 안에서 그대로 유지한다 - List.Sort는 안정 정렬이
    // 아니라서, 정렬 키가 하나뿐인(SortOrder가 없는) 몬스터 목록은 정렬 대신 이 순서로 통과시킨다.
    private static readonly MonsterCodexCategory[] MONSTER_CATEGORY_ORDER =
    {
        MonsterCodexCategory.Ground, MonsterCodexCategory.Air, MonsterCodexCategory.Boss,
    };

    [Header("Dependencies")]
    [SerializeField] private HelpCatalogSO _catalog;
    [SerializeField] private UIManager _uiManager;

    [Header("List")]
    [Tooltip("목록 슬롯 프리팹(Slot_Help). 도움말·적 정보 두 탭이 함께 쓴다.")]
    [SerializeField] private UI_HelpListSlot _slotPrefab;

    [Tooltip("갈래 머리글 프리팹(Slot_HelpCategory). 비우면 갈래로 묶지 않고 평면 목록이 된다.")]
    [WiringOptional]
    [SerializeField] private TMP_Text _categoryHeaderPrefab;

    [Tooltip("슬롯이 들어갈 부모. 스크롤 뷰의 Content를 직접 지정한다 - " +
             "프리팹의 부모에서 파생시키면 스크롤 영역 밖에 쌓인다.")]
    [SerializeField] private Transform _slotContainer;

    [Tooltip("현재 탭에 해금한 항목이 하나도 없을 때 목록 자리에 띄울 안내. 없어도 창은 동작한다.")]
    [WiringOptional]
    [SerializeField] private GameObject _emptyNotice;

    [Tooltip("_emptyNotice에 붙은 LocalizedText. 도움말/적 정보 탭마다 안내 문구가 달라 코드가 키를 " +
             "바꿔 준다. 없으면 프리팹에 고정된 문구(도움말용)가 두 탭 모두에 그대로 보인다.")]
    [WiringOptional]
    [SerializeField] private LocalizedText _emptyNoticeText;

    [Header("Detail")]
    [SerializeField] private TMP_Text _detailTitleText;
    [SerializeField] private TMP_Text _detailBodyText;

    [Tooltip("항목의 그림. 그림이 없는 항목에서는 오브젝트째 꺼진다. 적 정보 탭에서는 인게임 스프라이트를 그대로 쓴다.")]
    [SerializeField] private Image _detailIllustration;

    [Header("Header")]
    [SerializeField] private TMP_Text _countText;

    [Header("Tab - Help (TabMenu/Button_Help)")]
    [SerializeField] private Button _helpTabButton;

    [Tooltip("지금 고른 탭임을 표시하는 배경. Slot_Help의 _selectedHighlight와 같은 패턴 - " +
             "아이콘·글자색을 바꾸지 않고 배경 오버레이 하나만 켰다 끈다.")]
    [SerializeField] private GameObject _helpTabHighlight;

    [Tooltip("도움말 탭에 아직 펼쳐 보지 않은 항목이 있음을 알리는 붉은 점.")]
    [WiringOptional]
    [SerializeField] private GameObject _helpTabDot;

    [Header("Tab - Monster (TabMenu/Button_Monster)")]
    [Tooltip("적 정보 탭 전체 목록.")]
    [SerializeField] private MonsterCodexCatalogSO _monsterCatalog;
    [SerializeField] private Button _monsterTabButton;

    [Tooltip("지금 고른 탭임을 표시하는 배경. _helpTabHighlight와 같은 역할.")]
    [SerializeField] private GameObject _monsterTabHighlight;

    [Tooltip("적 정보 탭에 아직 펼쳐 보지 않은 항목이 있음을 알리는 붉은 점.")]
    [WiringOptional]
    [SerializeField] private GameObject _monsterTabDot;

    [Header("Close")]
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _blockerButton;
    [SerializeField] private InputActionReference _closeAction;

    private ComponentPool<UI_HelpListSlot> _slotPool;
    private ComponentPool<TMP_Text> _categoryHeaderPool;

    // 탭에 상관없이 재사용하는 단일 목록 - 두 탭을 따로 두면 재진입 파손 지점이 두 배가 된다.
    private readonly List<CodexRow> _rows = new();

    // RebuildHelpRows 안에서만 쓰는 정렬용 버퍼(재사용 - 매 렌더마다 새 리스트를 만들지 않는다).
    private readonly List<HelpEntrySO> _helpRowBuffer = new();

    private CodexTab _currentTab = CodexTab.Help;
    private int _selectedIndex = -1;
    private string _selectedRowId;
    private bool _isOpen;

    private GameSpeedManager GameSpeed => _uiManager != null ? _uiManager.GameSpeed : null;

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_HelpListSlot>(_slotPrefab, _slotContainer);
        _categoryHeaderPool = new ComponentPool<TMP_Text>(_categoryHeaderPrefab, _slotContainer);

        if (_helpTabButton != null)
        {
            _helpTabButton.onClick.AddListener(HandleHelpTabClicked);
        }

        if (_monsterTabButton != null)
        {
            _monsterTabButton.onClick.AddListener(HandleMonsterTabClicked);

            // 적 정보 카탈로그가 배선되지 않은 창(이 프리팹을 재사용하는 일부 테스트 씬)에서는
            // 고를 수 없는 탭을 아예 숨긴다 - 눌러도 아무 일도 안 일어나는 버튼을 남기지 않는다.
            _monsterTabButton.gameObject.SetActive(_monsterCatalog != null);
        }

        if (_exitButton != null)
        {
            _exitButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        // 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장하되,
        // 방금 Open()이 유발한 Awake라면 닫지 않는다(CLAUDE.md 이벤트 초기화 규칙).
        if (!_isOpen)
        {
            gameObject.SetActive(false);
        }
    }

    // 정지는 짝이 보장되는 OnEnable/OnDisable에 건다(UI_ConfigWindow와 같은 이유).
    private void OnEnable()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.AddWindowPause();
        }

        StringTable.OnLanguageChanged += Render;
        HelpProfile.Changed += Render;

        // 창 밖에서(에디터의 열람 이력 초기화 등) 열람 상태가 바뀌어도 붉은 점을 맞춘다.
        // 이 창이 스스로 유발한 발화도 여기로 돌아오는데, 발화 지점이 렌더 루프 밖이라 안전하다.
        HelpProfile.ViewedChanged += Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.RemoveWindowPause();
        }

        StringTable.OnLanguageChanged -= Render;
        HelpProfile.Changed -= Render;
        HelpProfile.ViewedChanged -= Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        if (_uiManager != null && !_uiManager.CanCloseExclusive(this))
        {
            return;
        }

        Close();
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        // SetActive 이전에 세운다 - 첫 활성화라면 이 안에서 Awake가 돌면서 스스로를 닫아버린다.
        _isOpen = true;
        gameObject.SetActive(true);

        ApplyTabVisual(_helpTabHighlight, _currentTab == CodexTab.Help);
        ApplyTabVisual(_monsterTabHighlight, _currentTab == CodexTab.Monster);

        Render();

        // 자동 선택된 첫 항목도 우측에 본문이 실제로 그려지므로 확인 처리한다.
        // Render() '다음'이어야 한다 - 안에 넣으면 렌더 루프 도중 ViewedChanged가 나면서
        // Render가 재진입한다(MarkSelectedViewed 주석 참조).
        MarkSelectedViewed();
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        gameObject.SetActive(false);
    }

    public void ToggleFromEntryPoint()
    {
        // 클릭음을 내지 않는다 - 창을 여닫는 제스처는 Open/Close의 창음만 낸다.
        if (_isOpen)
        {
            Close();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            Open();
        }
    }

    private void HandleHelpTabClicked() => SelectTab(CodexTab.Help);

    private void HandleMonsterTabClicked() => SelectTab(CodexTab.Monster);

    // 탭 선택은 창을 닫아도 유지한다(다시 열면 마지막 탭이 보인다 - UI_DragonWindow.SelectTab과 같음).
    private void SelectTab(CodexTab tab)
    {
        if (_currentTab == tab)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);

        _currentTab = tab;

        ApplyTabVisual(_helpTabHighlight, tab == CodexTab.Help);
        ApplyTabVisual(_monsterTabHighlight, tab == CodexTab.Monster);

        Render();

        // Open()과 같은 이유로 Render() '다음'에 부른다 - 렌더 루프 도중 MarkSelectedViewed를 부르면
        // ViewedChanged가 재진입을 일으킨다(MarkSelectedViewed 주석 참조).
        MarkSelectedViewed();
    }

    private static void ApplyTabVisual(GameObject highlight, bool isSelected)
    {
        if (highlight != null)
        {
            highlight.SetActive(isSelected);
        }
    }

    private void Render()
    {
        RebuildVisibleRows();
        RenderCount();
        RenderList();
        RenderDetail();
        RenderTabDots();
    }

    private void RebuildVisibleRows()
    {
        _rows.Clear();

        if (_currentTab == CodexTab.Help)
        {
            RebuildHelpRows();
        }
        else
        {
            RebuildMonsterRows();
        }

        ResolveSelectedIndex();
    }

    private void RebuildHelpRows()
    {
        if (_catalog == null)
        {
            return;
        }

        _helpRowBuffer.Clear();

        foreach (HelpEntrySO entry in _catalog.Entries)
        {
            // 가시성 규칙은 카탈로그가 든다 - HUD 붉은 점이 같은 규칙을 봐야 하고,
            // 두 곳에 적으면 "점은 있는데 볼 게 없다"가 된다.
            if (HelpCatalogSO.IsVisible(entry))
            {
                _helpRowBuffer.Add(entry);
            }
        }

        // 카탈로그 리스트 순서가 아니라 갈래·정렬값으로 세운다 - 항목을 중간에 끼우는 일이 잦아
        // 리스트 순서에 기대면 등록할 때마다 재정렬이 필요해진다.
        _helpRowBuffer.Sort(CompareHelpEntries);

        foreach (HelpEntrySO entry in _helpRowBuffer)
        {
            _rows.Add(CodexRow.ForHelp(entry));
        }
    }

    private static int CompareHelpEntries(HelpEntrySO left, HelpEntrySO right)
    {
        int byCategory = left.Category.CompareTo(right.Category);
        if (byCategory != 0)
        {
            return byCategory;
        }

        int bySortOrder = left.SortOrder.CompareTo(right.SortOrder);
        return bySortOrder != 0 ? bySortOrder : string.CompareOrdinal(left.EntryId, right.EntryId);
    }

    private void RebuildMonsterRows()
    {
        if (_monsterCatalog == null)
        {
            return;
        }

        foreach (MonsterCodexCategory category in MONSTER_CATEGORY_ORDER)
        {
            foreach (MonsterCodexCatalogSO.Row row in _monsterCatalog.Rows)
            {
                if (row.Category != category || !MonsterCodexCatalogSO.IsVisible(row))
                {
                    continue;
                }

                _rows.Add(CodexRow.ForMonster(row));
            }
        }
    }

    // 고르고 있던 항목이 사라졌거나(탭을 넘어왔거나) 아직 아무것도 고르지 않았으면 첫 항목을 고른다 -
    // 도감은 목록 자체가 콘텐츠라 우측이 빈 채로 열리면 안 된다.
    private void ResolveSelectedIndex()
    {
        _selectedIndex = -1;

        if (!string.IsNullOrEmpty(_selectedRowId))
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Id == _selectedRowId)
                {
                    _selectedIndex = i;
                    break;
                }
            }
        }

        if (_selectedIndex < 0 && _rows.Count > 0)
        {
            _selectedIndex = 0;
        }

        _selectedRowId = _selectedIndex >= 0 ? _rows[_selectedIndex].Id : null;
    }

    private void RenderCount()
    {
        if (_countText == null)
        {
            return;
        }

        int total = _currentTab == CodexTab.Help
            ? (_catalog == null ? 0 : _catalog.Entries.Count)
            : (_monsterCatalog == null ? 0 : _monsterCatalog.TotalCount);

        _countText.text = string.Format(
            StringTable.GetString(HelpLocKeys.UNLOCK_COUNT), _rows.Count, total);
    }

    private void RenderList()
    {
        if (_emptyNotice != null)
        {
            _emptyNotice.SetActive(_rows.Count == 0);
        }

        if (_emptyNoticeText != null)
        {
            _emptyNoticeText.SetKey(
                _currentTab == CodexTab.Help ? HelpLocKeys.EMPTY_LIST : HelpLocKeys.MONSTER_EMPTY_LIST);
        }

        // 머리글과 항목이 서로 다른 풀에서 나오므로, 한 줄씩 형제 순서를 직접 지정해 섞어 넣는다.
        // 풀에 그냥 두면 머리글이 전부 위로, 항목이 전부 아래로 몰린다.
        int siblingIndex = 0;
        int slotCount = 0;
        int headerCount = 0;
        string lastCategoryLocKey = null;

        for (int dataIndex = 0; dataIndex < _rows.Count; dataIndex++)
        {
            CodexRow row = _rows[dataIndex];

            if (_categoryHeaderPrefab != null && lastCategoryLocKey != row.CategoryLocKey)
            {
                TMP_Text header = _categoryHeaderPool.Get(headerCount);
                header.text = StringTable.GetString(row.CategoryLocKey);
                header.transform.SetSiblingIndex(siblingIndex);

                headerCount++;
                siblingIndex++;
                lastCategoryLocKey = row.CategoryLocKey;
            }

            UI_HelpListSlot slot = _slotPool.Get(slotCount);
            slot.Setup(
                row.TitleLocKey, row.Icon, dataIndex == _selectedIndex, !HelpProfile.IsViewed(row.Id),
                dataIndex, Select);
            slot.transform.SetSiblingIndex(siblingIndex);

            slotCount++;
            siblingIndex++;
        }

        _slotPool.DeactivateFrom(slotCount);
        _categoryHeaderPool.DeactivateFrom(headerCount);
    }

    private void Select(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);

        _selectedIndex = rowIndex;
        _selectedRowId = _rows[rowIndex].Id;

        // 렌더보다 먼저, 그리고 반드시 렌더 루프 밖에서 한다. 이 메서드는 슬롯 버튼의 onClick으로
        // 불리므로 RenderList의 순회 중이 아니다.
        MarkSelectedViewed();

        RenderList();
        RenderDetail();
    }

    /// <summary>
    /// 확인 처리의 유일한 지점. 우측에 본문이 실제로 표시되는 항목만 센다 - 최초 조우 팝업은
    /// 세지 않는다(항목 대부분이 AutoPopup이라, 팝업을 세면 붉은 점이 사실상 뜨지 않는다).
    ///
    /// <b>Render 계열 안에서 부르면 안 된다.</b> TryMarkViewed가 ViewedChanged를 발화하고 이 창이
    /// 그것을 구독하므로 Render가 재진입하는데, 그러면 (1) 재사용하는 단일 리스트인 _rows를
    /// 재진입 패스가 Clear해 RenderList의 foreach가 터지고, (2) 머리글·항목이 다른 풀에서 나와
    /// 형제 순서를 직접 지정하는 구조라 두 패스의 인덱스가 어긋나며, (3) 풀은 상태를 초기화하지
    /// 않으므로 DeactivateFrom 개수가 맞지 않아 이전 내용을 든 줄이 남는다.
    /// </summary>
    private void MarkSelectedViewed()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
        {
            HelpProfile.TryMarkViewed(_rows[_selectedIndex].Id);
        }
    }

    private void RenderDetail()
    {
        bool hasSelection = _selectedIndex >= 0 && _selectedIndex < _rows.Count;
        CodexRow row = hasSelection ? _rows[_selectedIndex] : default;

        string title = string.Empty;
        string body = string.Empty;
        MonsterIcon illustration = new MonsterIcon(null, Color.white);

        if (hasSelection && row.Entry != null)
        {
            title = StringTable.GetString(row.Entry.TitleLocKey);
            body = StringTable.GetString(row.Entry.BodyLocKey);
            illustration = new MonsterIcon(row.Entry.Illustration, Color.white);
        }
        else if (hasSelection && row.Monster != null)
        {
            // 적 정보 탭의 본문은 밤 개체 호버·낮 예고 카드와 같은 빌더를 쓴다 - 스탯 표기가 세 곳에서
            // 어긋나지 않는다(MonsterTooltipBuilder 클래스 주석 참고).
            TooltipContent content = MonsterTooltipBuilder.Build(row.Monster);
            title = content.Title;
            body = content.Body;
            illustration = row.Icon;
        }

        if (_detailTitleText != null)
        {
            _detailTitleText.text = title;
        }

        if (_detailBodyText != null)
        {
            _detailBodyText.text = body;
        }

        // 그림이 없는 항목에서 빈 액자가 남지 않도록 오브젝트째 끈다. 색은 매번 함께 대입한다 -
        // 도움말 탭에서는 흰색으로 되돌아가야 앞서 본 적의 틴트가 남지 않는다.
        if (_detailIllustration != null)
        {
            _detailIllustration.sprite = illustration.Sprite;
            _detailIllustration.color = illustration.Tint;
            _detailIllustration.gameObject.SetActive(illustration.Sprite != null);
        }
    }

    private void RenderTabDots()
    {
        if (_helpTabDot != null)
        {
            _helpTabDot.SetActive(_catalog != null && _catalog.HasUnviewedVisibleEntry());
        }

        if (_monsterTabDot != null)
        {
            _monsterTabDot.SetActive(_monsterCatalog != null && _monsterCatalog.HasUnviewedVisibleEntry());
        }
    }
}
