using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 세이브 슬롯 목록 창. 같은 프리팹이 두 가지 모드로 쓰인다.
/// - 불러오기(Load): 타이틀 화면과 인게임 설정 창에서 진입. 슬롯을 고르면 그 슬롯으로 게임을 연다.
/// - 저장(Save): 인게임 설정 창에서만 진입. 슬롯을 고르면 그 슬롯에 저장한다.
/// 창 루트(전면 dim Image)에 부착해 바깥 클릭 닫기까지 겸한다 - UI_ConfigWindow와 같은 구조.
///
/// 불러오기는 이 창이 직접 하지 않는다. 타이틀에서는 SaveLoadRequest에 슬롯만 남기고 씬을 바꾸면
/// 게임 씬의 SaveService.Awake가 그것을 소비하고 GameManager.Start가 복원 경로로 분기한다.
/// 인게임에서는 같은 일을 SaveService.RequestLoadAndReloadScene이 대신한다 - 살아 있는 몬스터·
/// 건물을 정리할 방법이 없어 씬 재로드를 반드시 거쳐야 한다(SaveService 계약 3).
///
/// 의존성 주입이 모드별로 다르다. 타이틀 화면에는 인게임 매니저가 없어 SaveService를 둘 수 없고,
/// 인게임에는 열어야 할 다른 씬이 없다. 그래서 Construct 오버로드로 각자 필요한 것만 받는다.
/// </summary>
public class UI_LoadGameWindow : MonoBehaviour, IExclusiveMode
{
    public enum WindowMode
    {
        Load,
        Save,
    }

    [Tooltip("세이브 슬롯 한 줄 프리팹(Slot_SaveSlot).")]
    [SerializeField] private UI_SaveSlotItem _slotPrefab;

    [Tooltip("생성된 슬롯이 들어갈 부모.")]
    [SerializeField] private Transform _slotContainer;

    [Tooltip("창 제목. 모드에 따라 key를 바꿔 끼운다.")]
    [SerializeField] private LocalizedText _headerLabel;

    [Tooltip("저장 결과·저장 불가 사유를 띄우는 줄. 타이틀 화면 인스턴스에는 없어도 된다.")]
    [SerializeField] private TMP_Text _statusText;

    [Tooltip("우상단 닫기 버튼.")]
    [SerializeField] private Button _closeButton;

    [Tooltip("창 바깥(전면 dim) 클릭으로 닫기. 보통 이 창 루트의 Button.")]
    [SerializeField] private Button _blockerButton;

    [Tooltip("창을 닫는 키 - 보통 Esc.")]
    [SerializeField] private InputActionReference _closeAction;

    private ComponentPool<UI_SaveSlotItem> _slotPool;
    private readonly List<UI_SaveSlotItem> _slots = new();

    // 슬롯을 고른 뒤 열 씬. 타이틀 화면이 Construct로 넣어 준다(UI_VolumeRow와 같은 주입 방식) -
    // 창 프리팹이 특정 씬 이름을 직렬화해 들고 있지 않도록.
    private string _gameSceneName;

    // 인게임 설정 창이 Construct로 넣어 준다. 타이틀 화면에서는 null이고, 그 값이 곧
    // "여기는 인게임인가"의 판정이 된다.
    private SaveService _saveService;

    private WindowMode _mode = WindowMode.Load;

    // 덮어쓰기 확인 대기 중인 슬롯. 확인 창을 따로 두지 않고 같은 슬롯을 한 번 더 누르게 한다.
    private int _pendingOverwriteSlotIndex = SavePaths.INVALID_SLOT_INDEX;

    // 마지막 저장 시도의 결과 문구. null이면 상태 줄은 모드 기본 안내로 돌아간다.
    private string _statusLocKey;

    // 디스크 쓰기가 끝날 때까지 슬롯을 잠근다. SaveService도 동시 저장을 막지만, 거기서 돌아오는
    // AlreadyRunning 실패 문구를 먼저 시작한 저장의 성공 문구가 나중에 덮어써
    // 저장되지 않은 슬롯이 저장된 것처럼 보인다.
    private bool _isSavingSlot;

    private bool _isOpen;

    private bool CanSaveNow => _saveService != null && _saveService.CanSave;

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_SaveSlotItem>(_slotPrefab, _slotContainer);

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += Refresh;

        if (_closeAction != null)
        {
            // 액션을 켜는 것은 GlobalInputBootstrap의 몫이다 - 여기서 켜면 창을 한 번 연 뒤로
            // 세션 내내 켜진 채 남아 다른 창의 동작이 이 창을 열었는지에 좌우된다.
            _closeAction.action.performed += OnCloseActionPerformed;
        }

        // 구독 직후 현재 값을 한 번 반영한다(CLAUDE.md 이벤트 초기화 규칙).
        Refresh();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Refresh;

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }

        // 확인 대기 상태를 창 밖으로 들고 나가지 않는다 - 다시 열었을 때 한 번 눌렀던 슬롯이
        // 곧바로 덮어써지면 안 된다.
        _pendingOverwriteSlotIndex = SavePaths.INVALID_SLOT_INDEX;
        _statusLocKey = null;

        // 창이 닫혀 있는 동안 슬롯 수만큼의 텍스처를 들고 있을 이유가 없다.
        ReleaseThumbnails();
    }

    /// <summary>슬롯을 골랐을 때 열 씬을 지정한다. 타이틀 화면이 한 번 호출한다.</summary>
    public void Construct(string gameSceneName)
    {
        _gameSceneName = gameSceneName;
    }

    /// <summary>저장·인게임 로드에 쓸 서비스를 지정한다. 인게임 설정 창이 한 번 호출한다.</summary>
    public void Construct(SaveService saveService)
    {
        _saveService = saveService;
    }

    // 목록 갱신은 OnEnable이 맡는다 - 활성화와 갱신이 두 번 일어나지 않도록.
    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        gameObject.SetActive(false);
    }

    public void OpenForLoad() => OpenInMode(WindowMode.Load);

    public void OpenForSave() => OpenInMode(WindowMode.Save);

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    // Open()이 SetActive → OnEnable → Refresh로 이어지므로, 모드는 그보다 먼저 확정해야 한다.
    private void OpenInMode(WindowMode mode)
    {
        _mode = mode;
        _pendingOverwriteSlotIndex = SavePaths.INVALID_SLOT_INDEX;
        _statusLocKey = null;

        bool wasAlreadyActive = gameObject.activeSelf;
        Open();

        // 이미 켜져 있었다면 SetActive가 상태를 바꾸지 않아 OnEnable이 불리지 않는다.
        // 그대로 두면 목록과 제목이 직전 모드 그대로 남으므로 여기서 직접 다시 그린다.
        if (wasAlreadyActive)
        {
            Refresh();
        }
    }

    private void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }

    private void Refresh()
    {
        if (_slotPool == null)
        {
            return;
        }

        RenderHeader();
        RenderStatus();

        IReadOnlyList<SaveSlotInfo> slots = SaveSlotQuery.GetSlots();
        _slots.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            UI_SaveSlotItem item = _slotPool.Get(i);
            item.Setup(
                slots[i],
                IsSlotSelectable(slots[i]),
                slots[i].SlotIndex == _pendingOverwriteSlotIndex,
                HandleSlotSelected,
                HandleSlotDeleted);
            _slots.Add(item);
        }

        _slotPool.DeactivateFrom(slots.Count);

        if (_slotContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private void RenderHeader()
    {
        if (_headerLabel != null)
        {
            _headerLabel.SetKey(_mode == WindowMode.Save
                ? SaveLocKeys.SAVE_WINDOW_HEADER
                : TitleLocKeys.LOAD_WINDOW_HEADER);
        }
    }

    private void RenderStatus()
    {
        if (!WiringGuard.Require(_statusText, nameof(_statusText), this))
        {
            return;
        }

        string locKey = _statusLocKey;

        // 아직 아무것도 시도하지 않았는데 저장할 수 없는 상태라면(주로 밤), 슬롯을 눌러 보기 전에
        // 왜 전부 잠겨 있는지 먼저 알려 준다. 저장이 도는 동안에도 CanSave는 false지만,
        // 그건 "지금은 저장할 수 없다"가 아니라 진행 중이라는 뜻이라 안내를 띄우지 않는다.
        if (locKey == null && _mode == WindowMode.Save && !CanSaveNow && !_isSavingSlot)
        {
            locKey = SaveLocKeys.ResolveSaveFailureLocKey(SaveFailureReason.NotSaveablePhase);
        }

        _statusText.text = locKey == null ? string.Empty : StringTable.GetString(locKey);
    }

    private bool IsSlotSelectable(SaveSlotInfo info)
    {
        if (_mode == WindowMode.Save)
        {
            // 빈 슬롯도 손상된 슬롯도 저장 대상이다(손상 슬롯을 덮어쓰는 것이 곧 복구다).
            // 자동저장 슬롯만 뺀다 - 다음 낮에 자동저장이 덮어써 수동 세이브가 사라진다.
            return info.SlotIndex != SaveService.AUTO_SAVE_SLOT_INDEX && CanSaveNow && !_isSavingSlot;
        }

        return !info.IsEmpty && !info.IsCorrupted;
    }

    private void ReleaseThumbnails()
    {
        foreach (UI_SaveSlotItem item in _slots)
        {
            if (item != null)
            {
                item.ReleaseThumbnail();
            }
        }
    }

    private void HandleSlotSelected(int slotIndex)
    {
        if (_mode == WindowMode.Save)
        {
            HandleSaveSlotSelected(slotIndex);
            return;
        }

        HandleLoadSlotSelected(slotIndex);
    }

    private void HandleSaveSlotSelected(int slotIndex)
    {
        if (_saveService == null)
        {
            Debug.LogError("[UI_LoadGameWindow] SaveService가 주입되지 않았습니다. Construct 호출을 확인하세요.");
            return;
        }

        if (_isSavingSlot)
        {
            return;
        }

        // 지울 것이 있는 슬롯은 같은 자리를 한 번 더 눌러야 저장한다. 다른 슬롯을 누르면
        // 확인 대상이 그쪽으로 옮겨 가므로, 잘못 누른 확인이 저장으로 이어지지 않는다.
        if (HasDataInSlot(slotIndex) && _pendingOverwriteSlotIndex != slotIndex)
        {
            _pendingOverwriteSlotIndex = slotIndex;
            _statusLocKey = null;
            Refresh();
            return;
        }

        _pendingOverwriteSlotIndex = SavePaths.INVALID_SLOT_INDEX;

        // 잠금은 저장을 시작하기 전에 걸고 화면에도 바로 반영한다 - 쓰기가 끝날 때까지
        // 남은 슬롯이 눌리는 상태로 보이면 안 된다.
        _isSavingSlot = true;
        Refresh();

        SaveToSlotAsync(slotIndex).Forget();
    }

    private async UniTaskVoid SaveToSlotAsync(int slotIndex)
    {
        SaveResult result = await _saveService.SaveAsync(
            slotIndex, false, this.GetCancellationTokenOnDestroy());

        _isSavingSlot = false;

        // 기다리는 동안 창이 닫혔다면(Esc·dim 클릭·부모 비활성) 여기서 멈춘다. 그대로 두면
        // OnDisable이 반납한 썸네일을 Refresh가 도로 만들어 붙들고, 슬롯도 다시 켜진다.
        if (!isActiveAndEnabled)
        {
            return;
        }

        _statusLocKey = result.IsSuccess
            ? SaveLocKeys.SLOT_SAVED
            : SaveLocKeys.ResolveSaveFailureLocKey(result.Reason);

        // 성공이면 새 저장 시각·일차·썸네일이, 실패면 원래 값이 그대로 다시 그려진다.
        Refresh();
    }

    private void HandleLoadSlotSelected(int slotIndex)
    {
        // 인게임에서는 씬을 다시 로드해야 살아 있는 몬스터·투사체·건물이 정리된다(SaveService 계약 3).
        if (_saveService != null)
        {
            _saveService.RequestLoadAndReloadScene(slotIndex);
            return;
        }

        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[UI_LoadGameWindow] 열 씬이 지정되지 않았습니다. Construct 호출을 확인하세요.");
            return;
        }

        SaveLoadRequest.Request(slotIndex);
        SceneManager.LoadScene(_gameSceneName);
    }

    private void HandleSlotDeleted(int slotIndex)
    {
        if (SaveSlotQuery.TryDelete(slotIndex))
        {
            // 지워진 슬롯에는 덮어쓸 것이 없다.
            if (_pendingOverwriteSlotIndex == slotIndex)
            {
                _pendingOverwriteSlotIndex = SavePaths.INVALID_SLOT_INDEX;
            }

            Refresh();
        }
    }

    private static bool HasDataInSlot(int slotIndex) =>
        SaveSlotQuery.TryGetSlot(slotIndex, out SaveSlotInfo info) && !info.IsEmpty;
}
