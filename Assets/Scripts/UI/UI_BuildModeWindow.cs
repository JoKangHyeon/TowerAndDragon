using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

// 건물 배치 및 철거, 재이동 디버깅용 -> 추후 수정될 수 있음
public class UI_BuildModeWindow : MonoBehaviour, IExclusiveMode
{
    // 필터 탭 하나. 선택되면 Menu_Focus가 활성, 아니면 Menu_Default가 활성이 된다.
    // 이 탭(카테고리)이 가진 건물들이 선택 시 슬롯 목록으로 생성된다.
    [System.Serializable]
    private struct FilterTab
    {
        public Button Button;
        public GameObject MenuFocus;
        public GameObject MenuDefault;
        public UI_BuildingSlot SlotPrefab;   // 이 카테고리 슬롯 프리팹 (Slot_Tower / Slot_Building / Slot_Factory)
        public Building[] Buildings;
    }

    // 상단 활성 버튼 묶음 (이동 / 철거).
    [System.Serializable]
    private struct ActiveButtons
    {
        public Button Move;
        public Button Remove;
    }

    [SerializeField]
    private ActiveButtons _activeButtons;

    [SerializeField]
    private FilterTab[] _filterTabs;

    [Header("건물 슬롯")]
    [Tooltip("생성된 슬롯이 들어갈 부모 (Scroll View의 Content). 슬롯 프리팹은 각 탭(FilterTab)에서 지정.")]
    [SerializeField]
    private Transform _slotContainer;

    [SerializeField]
    private GameObject _buildModePanel;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [SerializeField]
    private UIManager _uiManager;

    [Tooltip("밤이 시작되면 빌드모드 패널을 자동으로 닫기 위해 구독한다.")]
    [SerializeField]
    private CycleManager _cycleManager;

    [Header("패널 열림/닫힘 연출")]
    [SerializeField]
    private float _slideDuration = 0.5f;
    [Tooltip("열릴 때 시작 오프셋(홈 기준). 여기서 홈으로 슬라이드 인.")]
    [SerializeField]
    private Vector2 _openFromOffset = new Vector2(-50f, 0f);
    [Tooltip("닫힐 때 도착 오프셋(홈 기준). 홈에서 여기로 슬라이드 아웃 후 비활성화.")]
    [SerializeField]
    private Vector2 _closeToOffset = new Vector2(-500f, 0f);

    [Tooltip("빌드모드 패널을 닫는 액션 - 보통 ESC.")]
    [SerializeField]
    private InputActionReference _closeAction;

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private bool _isOpen;
    private Tween _panelTween;
    private int _currentFilterIndex;
    private bool _initialized;

    private readonly List<UI_BuildingSlot> _spawnedSlots = new();

    // 슬롯은 탭을 고를 때마다 새로 만들어지므로, 슬롯을 가리키려는 안내는 이 이벤트를 듣고 다시 조준해야 한다.
    // UI_DragonInventoryWindow.OnSlotViewChanged와 같은 용도·같은 이름이다.
    public UnityEvent OnSlotViewChanged = new();

    // 어느 탭이 골라졌는지 그 탭 버튼으로 알린다 - 인덱스로 넘기면 호출부가 순서를 알아야 하고,
    // 탭 순서가 바뀌면 조용히 어긋난다. 버튼이면 안내가 가리키던 대상과 그대로 비교할 수 있다.
    public UnityEvent<RectTransform> OnTabSelected = new();

    /// <summary>
    /// 현재 탭에 그려진 슬롯 중 이 건물 프리팹에 해당하는 것. 슬롯은 런타임 생성이라
    /// GuideAnchor로는 가리킬 수 없어서 창이 직접 돌려준다.
    /// </summary>
    public bool TryGetSlotRect(Building prefab, out RectTransform slotRect)
    {
        slotRect = null;

        if (prefab == null)
        {
            return false;
        }

        // 슬롯에게 직접 물어본다 - Buildings 배열과 인덱스를 맞추는 방식은 끊긴 항목을 건너뛰는 순간
        // 어긋나고, 어긋났다는 사실이 드러나지 않은 채 엉뚱한 슬롯을 가리킨다.
        foreach (UI_BuildingSlot slot in _spawnedSlots)
        {
            if (slot == null || slot.Prefab != prefab)
            {
                continue;
            }

            slotRect = (RectTransform)slot.transform;
            return true;
        }

        return false;
    }

    private void Awake()
    {
        bool wasInitialized = _initialized;
        EnsureInitialized();

        if (!wasInitialized && _buildModePanel != null)
        {
            _buildModePanel.SetActive(false);
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        if (_activeButtons.Remove != null)
        {
            _activeButtons.Remove.onClick.AddListener(() =>
            {
                SoundManager.Play(SoundId.UiButtonClick);

                if (_buildingPlacementController != null)
                {
                    _buildingPlacementController.RemoveSelectedBuilding();
                }
            });
        }

        if (_activeButtons.Move != null)
        {
            _activeButtons.Move.onClick.AddListener(() =>
            {
                SoundManager.Play(SoundId.UiButtonClick);

                if (_buildingPlacementController != null)
                {
                    _buildingPlacementController.EnterMoveMode();
                }
            });
        }

        for (int i = 0; i < _filterTabs.Length; i++)
        {
            int index = i;
            if (_filterTabs[i].Button != null)
            {
                // 클릭음은 SelectFilter가 아니라 여기서 낸다 - SelectFilter는 초기 탭 지정에도 호출된다.
                _filterTabs[i].Button.onClick.AddListener(() =>
                {
                    SoundManager.Play(SoundId.UiButtonClick);
                    SelectFilter(index);
                });
            }
        }

        if (_buildModePanel == null)
        {
            _buildModePanel = gameObject;
        }

        _panelRect = _buildModePanel.GetComponent<RectTransform>();
        if (_panelRect != null)
        {
            _homePos = _panelRect.anchoredPosition;
        }

        if (_filterTabs.Length > 0)
        {
            SelectFilter(0);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
        }

        _initialized = true;
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }
    }

    // 밤 시작 시 패널이 열려 있으면 닫는다.
    private void HandleNightStart(int cycle)
    {
        if (_isOpen)
        {
            CloseBuildPanel();
        }
    }

    private void Update()
    {
        EnsureInitialized();

        if (_buildingPlacementController == null)
        {
            HandleCloseInput();
            return;
        }

        // 새끼용은 전용 창(UI_BabyDragonManageWindow/UI_DragonInventoryWindow)에서만 이동/철거한다 -
        // 여기서도 같이 반응하면 같은 대상에 버튼이 두 벌 뜬다.
        Building selected = _buildingPlacementController.SelectedBuilding;
        Building target = selected is BabyDragonTower ? null : selected;

        // 이동 모드 진입/종료(클릭 이동, 취소, 우클릭 취소 등)에 맞춰 Move 버튼 표시를 매 프레임 동기화
        if (_activeButtons.Move != null)
        {
            _activeButtons.Move.gameObject.SetActive(!_buildingPlacementController.IsMoving);
            _activeButtons.Move.interactable = _buildingPlacementController.CanMoveNow(target);
        }

        // 이동/철거 불가 건물(성, 주둔지 등) 선택 시 버튼을 비활성화해 클릭해도 아무 반응 없는 상황을 방지
        if (_activeButtons.Remove != null)
        {
            _activeButtons.Remove.interactable = _buildingPlacementController.CanRemoveNow(target);
        }

        HandleCloseInput();
    }

    // ESC 입력 시 빌드모드 패널을 닫고 배치/이동 중이던 상태도 함께 취소한다.
    private void HandleCloseInput()
    {
        if (!_isOpen)
            return;

        if (_closeAction != null && _closeAction.action.WasPerformedThisFrame() &&
            (_uiManager == null || _uiManager.CanCloseExclusive(this)))
        {
            CloseBuildPanel();
        }
    }

    // BuildMode 버튼 토글. 열 때는 UIManager를 거쳐 다른 배타 모드(점령 등)를 정리한다.
    public void ToggleFromEntryPoint()
    {
        // 클릭음을 내지 않는다 - 창을 여닫는 제스처는 OpenBuildPanel/CloseBuildPanel의 창음만 낸다.
        EnsureInitialized();

        // 밤에는 건설 모드를 켤 수 없다(창이 아예 열리지 않는다). 끄는 것은 항상 허용.
        // 경고 메시지는 띄우지 않는다 - Warning_window에 건설용 메시지 오브젝트가 없다.
        if (!_isOpen && _cycleManager != null &&
            _cycleManager.CurrentCycle == CycleManager.CycleState.Night)
        {
            return;
        }

        if (_isOpen)
        {
            CloseBuildPanel();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            OpenBuildPanel();
        }
    }

    private void OpenBuildPanel()
    {
        EnsureInitialized();

        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        _panelTween?.Kill();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (_buildModePanel == null || _panelRect == null)
        {
            return;
        }

        _buildModePanel.SetActive(true);
        // 홈에서 왼쪽으로 벗어난 위치에서 시작해 홈으로 슬라이드 인.
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_buildModePanel);

        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.ShowOccupiedTiles();
        }

        // 낮/밤이 바뀐 채로 재오픈될 수 있으므로 슬롯을 다시 그려 interactable을 최신 상태로 맞춘다
        // (UI_DragonInventoryWindow.OpenPanel과 동일한 관례).
        if (_filterTabs.Length > 0)
        {
            SelectFilter(_currentFilterIndex);
        }
    }

    private void CloseBuildPanel()
    {
        EnsureInitialized();

        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.CancelAll();
            _buildingPlacementController.HideOccupiedTiles();
        }

        if (_buildModePanel == null || _panelRect == null)
        {
            return;
        }

        // 홈에서 왼쪽으로 슬라이드 아웃한 뒤 패널을 비활성화한다.
        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_buildModePanel)
            .OnComplete(() => _buildModePanel.SetActive(false));
    }

    // 선택된 탭만 Focus 상태로, 나머지는 Default 상태로 만들고, 그 탭의 건물 슬롯 목록을 다시 생성한다.
    private void SelectFilter(int index)
    {
        if (_filterTabs == null || _filterTabs.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, _filterTabs.Length - 1);
        _currentFilterIndex = index;

        for (int i = 0; i < _filterTabs.Length; i++)
        {
            bool isSelected = i == index;
            if (_filterTabs[i].MenuFocus != null)
            {
                _filterTabs[i].MenuFocus.SetActive(isSelected);
            }

            if (_filterTabs[i].MenuDefault != null)
            {
                _filterTabs[i].MenuDefault.SetActive(!isSelected);
            }
        }

        RebuildSlots(_filterTabs[index].SlotPrefab, _filterTabs[index].Buildings);

        Button selectedButton = _filterTabs[index].Button;
        OnTabSelected?.Invoke(selectedButton == null ? null : (RectTransform)selectedButton.transform);
    }

    // 컨테이너의 기존 슬롯을 지우고, 주어진 슬롯 프리팹으로 건물 목록만큼 슬롯을 새로 생성한다.
    private void RebuildSlots(UI_BuildingSlot slotPrefab, Building[] buildings)
    {
        foreach (UI_BuildingSlot slot in _spawnedSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        _spawnedSlots.Clear();

        // 슬롯을 지운 것도 조준 대상이 사라진 변화이므로 이 경로에서도 알린다.
        if (slotPrefab == null || _slotContainer == null || buildings == null)
        {
            OnSlotViewChanged?.Invoke();
            return;
        }

        bool isPlaceable = _buildingPlacementController == null || _buildingPlacementController.IsDayForBuildActions;

        foreach (Building building in buildings)
        {
            // 참조가 끊긴 항목(프리팹에서 컴포넌트를 다시 만든 경우 등)은 건너뛴다.
            // 그대로 넘기면 아이콘을 읽다 예외가 나면서 뒤따르는 슬롯까지 통째로 만들어지지 않는다.
            if (building == null)
            {
                Debug.LogError($"[UI_BuildModeWindow] 건물 목록에 끊긴 참조가 있어 슬롯을 건너뜁니다 " +
                               $"(탭의 {buildings.Length}개 중 하나). 인스펙터에서 다시 지정해야 합니다.", this);
                continue;
            }

            UI_BuildingSlot slot = Instantiate(slotPrefab, _slotContainer);
            slot.Setup(building, OnSlotSelected);
            slot.SetInteractable(isPlaceable);
            _spawnedSlots.Add(slot);
        }

        OnSlotViewChanged?.Invoke();
    }

    // 슬롯 클릭 시 해당 건물을 배치 대상으로 선택 (기존 building buttons에서 옮겨온 기능).
    private void OnSlotSelected(Building prefab)
    {
        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.SelectBuilding(prefab);
        }
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => OpenBuildPanel();
    void IExclusiveMode.Close()
    {
        if (_isOpen)
            CloseBuildPanel();
    }
}
