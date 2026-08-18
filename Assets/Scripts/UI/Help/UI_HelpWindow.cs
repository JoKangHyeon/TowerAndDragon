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
/// <b>잠긴 항목은 목록에 넣지 않는다.</b> 회색 실루엣을 깔면 아직 만나지 않은 시스템의 존재를
/// 미리 알려주게 되어 "플레이하면서 하나씩 해금되는 느낌"이라는 요구와 정면으로 어긋난다.
/// 대신 헤더의 "해금 N / 전체 M" 한 줄이 "더 있다"는 정보를 대신한다.
/// </summary>
public sealed class UI_HelpWindow : MonoBehaviour, IExclusiveMode
{
    [Header("Dependencies")]
    [SerializeField] private HelpCatalogSO _catalog;
    [SerializeField] private UIManager _uiManager;

    [Header("List")]
    [Tooltip("목록 슬롯 프리팹(Slot_Help).")]
    [SerializeField] private UI_HelpListSlot _slotPrefab;

    [Tooltip("갈래 머리글 프리팹(Slot_HelpCategory). 비우면 갈래로 묶지 않고 평면 목록이 된다.")]
    [WiringOptional]
    [SerializeField] private TMP_Text _categoryHeaderPrefab;

    [Tooltip("슬롯이 들어갈 부모. 스크롤 뷰의 Content를 직접 지정한다 - " +
             "프리팹의 부모에서 파생시키면 스크롤 영역 밖에 쌓인다.")]
    [SerializeField] private Transform _slotContainer;

    [Tooltip("해금한 항목이 하나도 없을 때 목록 자리에 띄울 안내. 없어도 창은 동작한다.")]
    [WiringOptional]
    [SerializeField] private GameObject _emptyNotice;

    [Header("Detail")]
    [SerializeField] private TMP_Text _detailTitleText;
    [SerializeField] private TMP_Text _detailBodyText;

    [Tooltip("항목의 그림. 그림이 없는 항목에서는 오브젝트째 꺼진다.")]
    [SerializeField] private Image _detailIllustration;

    [Header("Header")]
    [SerializeField] private TMP_Text _countText;

    [Header("Close")]
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _blockerButton;
    [SerializeField] private InputActionReference _closeAction;

    private ComponentPool<UI_HelpListSlot> _slotPool;
    private ComponentPool<TMP_Text> _categoryHeaderPool;

    // 갈래 → 정렬 순서로 정돈한 해금 항목. 매 갱신마다 새 리스트를 만들지 않도록 재사용한다.
    private readonly List<HelpEntrySO> _visibleEntries = new();

    private HelpEntrySO _selectedEntry;
    private bool _isOpen;

    private GameSpeedManager GameSpeed => _uiManager != null ? _uiManager.GameSpeed : null;

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_HelpListSlot>(_slotPrefab, _slotContainer);
        _categoryHeaderPool = new ComponentPool<TMP_Text>(_categoryHeaderPrefab, _slotContainer);

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

        Render();
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

    private void Render()
    {
        RebuildVisibleEntries();
        RenderCount();
        RenderList();
        RenderDetail();
    }

    private void RebuildVisibleEntries()
    {
        _visibleEntries.Clear();

        if (_catalog == null)
        {
            return;
        }

        foreach (HelpEntrySO entry in _catalog.Entries)
        {
            if (entry != null && (entry.UnlockedFromStart || HelpProfile.IsUnlocked(entry.EntryId)))
            {
                _visibleEntries.Add(entry);
            }
        }

        // 카탈로그 리스트 순서가 아니라 갈래·정렬값으로 세운다 - 항목을 중간에 끼우는 일이 잦아
        // 리스트 순서에 기대면 등록할 때마다 재정렬이 필요해진다.
        _visibleEntries.Sort(CompareEntries);

        // 고르고 있던 항목이 사라졌거나 아직 아무것도 고르지 않았으면 첫 항목을 고른다 -
        // 도감은 목록 자체가 콘텐츠라 우측이 빈 채로 열리면 안 된다.
        if (_selectedEntry == null || !_visibleEntries.Contains(_selectedEntry))
        {
            _selectedEntry = _visibleEntries.Count > 0 ? _visibleEntries[0] : null;
        }
    }

    private static int CompareEntries(HelpEntrySO left, HelpEntrySO right)
    {
        int byCategory = left.Category.CompareTo(right.Category);
        if (byCategory != 0)
        {
            return byCategory;
        }

        int bySortOrder = left.SortOrder.CompareTo(right.SortOrder);
        return bySortOrder != 0 ? bySortOrder : string.CompareOrdinal(left.EntryId, right.EntryId);
    }

    private void RenderCount()
    {
        if (_countText == null)
        {
            return;
        }

        int total = _catalog == null ? 0 : _catalog.Entries.Count;
        _countText.text = string.Format(
            StringTable.GetString(HelpLocKeys.UNLOCK_COUNT), _visibleEntries.Count, total);
    }

    private void RenderList()
    {
        if (_emptyNotice != null)
        {
            _emptyNotice.SetActive(_visibleEntries.Count == 0);
        }

        // 머리글과 항목이 서로 다른 풀에서 나오므로, 한 줄씩 형제 순서를 직접 지정해 섞어 넣는다.
        // 풀에 그냥 두면 머리글이 전부 위로, 항목이 전부 아래로 몰린다.
        int rowIndex = 0;
        int slotCount = 0;
        int headerCount = 0;
        HelpCategory? lastCategory = null;

        foreach (HelpEntrySO entry in _visibleEntries)
        {
            if (_categoryHeaderPrefab != null && lastCategory != entry.Category)
            {
                TMP_Text header = _categoryHeaderPool.Get(headerCount);
                header.text = StringTable.GetString(HelpLocKeys.CategoryLocKey(entry.Category));
                header.transform.SetSiblingIndex(rowIndex);

                headerCount++;
                rowIndex++;
                lastCategory = entry.Category;
            }

            UI_HelpListSlot slot = _slotPool.Get(slotCount);
            slot.Setup(entry, entry == _selectedEntry, Select);
            slot.transform.SetSiblingIndex(rowIndex);

            slotCount++;
            rowIndex++;
        }

        _slotPool.DeactivateFrom(slotCount);
        _categoryHeaderPool.DeactivateFrom(headerCount);
    }

    private void Select(HelpEntrySO entry)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        _selectedEntry = entry;
        RenderList();
        RenderDetail();
    }

    private void RenderDetail()
    {
        if (_detailTitleText != null)
        {
            _detailTitleText.text = _selectedEntry == null
                ? string.Empty
                : StringTable.GetString(_selectedEntry.TitleLocKey);
        }

        if (_detailBodyText != null)
        {
            _detailBodyText.text = _selectedEntry == null
                ? string.Empty
                : StringTable.GetString(_selectedEntry.BodyLocKey);
        }

        // 그림이 없는 항목에서 빈 액자가 남지 않도록 오브젝트째 끈다.
        if (_detailIllustration != null)
        {
            _detailIllustration.sprite = _selectedEntry == null ? null : _selectedEntry.Illustration;
            _detailIllustration.gameObject.SetActive(_detailIllustration.sprite != null);
        }
    }
}
