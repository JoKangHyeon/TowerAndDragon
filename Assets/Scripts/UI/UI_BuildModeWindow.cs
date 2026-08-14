using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

// 건물 배치 및 철거, 재이동 디버깅용 -> 추후 수정될 수 있음
public class UI_BuildModeWindow : MonoBehaviour, IExclusiveMode, IExclusiveModeEntryGuard
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

    // 모든 탭이 띄우는 건물 프리팹 - 연구 해금 여부와 무관한 전체 목록이다.
    // BuildingCatalogTools가 "지을 수는 있는데 저장되지 않는 건물"을 찾는 데 쓴다.
    public IEnumerable<Building> PlaceableBuildings
    {
        get
        {
            if (_filterTabs == null)
            {
                yield break;
            }

            foreach (FilterTab tab in _filterTabs)
            {
                if (tab.Buildings == null)
                {
                    continue;
                }

                foreach (Building building in tab.Buildings)
                {
                    if (building != null)
                    {
                        yield return building;
                    }
                }
            }
        }
    }

    [Header("건물 슬롯")]
    [Tooltip("생성된 슬롯이 들어갈 부모 (Scroll View의 Content). 슬롯 프리팹은 각 탭(FilterTab)에서 지정.")]
    [SerializeField]
    private Transform _slotContainer;

    [SerializeField]
    private GameObject _buildModePanel;

    [Tooltip("타워 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창(Popup_Tower_Info). " +
        "스크롤 뷰의 Viewport 아래에 두면 마스크에 잘리므로 그 바깥에 배치한다.")]
    [SerializeField]
    private UI_TowerInfoPopup _towerInfoPopup;

    [Tooltip("생산건물 슬롯에 마우스를 올렸을 때 뜨는 정보 창(Popup_Factory_Info). 배치 위치는 위와 같다.")]
    [SerializeField]
    private UI_FactoryInfoPopup _factoryInfoPopup;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [SerializeField]
    private UIManager _uiManager;

    [Tooltip("밤이 시작되면 빌드모드 패널을 자동으로 닫기 위해 구독한다.")]
    [SerializeField]
    private CycleManager _cycleManager;

    [Tooltip("밤에 건설 모드를 켜려 했을 때 경고 메시지를 띄울 창.")]
    [SerializeField]
    private UI_WarningWindow _warningWindow;

    [Tooltip("연구로 해금되는 타워를 거르기 위해 참조한다. 비워두면 모든 타워가 그대로 보인다.")]
    [WiringOptional]
    [SerializeField]
    private ResearchManager _researchManager;

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
    private readonly List<IBuildModeInteractionQuery> _interactionQueries = new();

    public void AddInteractionQuery(IBuildModeInteractionQuery query)
    {
        if (query != null && !_interactionQueries.Contains(query))
        {
            _interactionQueries.Add(query);
        }
    }

    public void RemoveInteractionQuery(IBuildModeInteractionQuery query) =>
        _interactionQueries.Remove(query);

    // 자원 매니저는 따로 배선하지 않고 이미 배선된 배치 컨트롤러의 것을 나눠 쓴다 - 창이 있는 씬마다
    // 손으로 이어야 하면 빠뜨린 씬에서 비용 색이 조용히 멈춘다.
    // (UnityEngine.Resources와 이름이 겹치지 않도록 Build 접두사를 붙였다.)
    private ResourceManager BuildResources =>
        _buildingPlacementController != null ? _buildingPlacementController.Resources : null;

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
                if (!CanRemoveSelectedBuilding())
                {
                    return;
                }

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
                if (!CanMoveSelectedBuilding())
                {
                    return;
                }

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
                    RectTransform filterTab = (RectTransform)_filterTabs[index].Button.transform;
                    if (!CanSelectFilter(filterTab))
                    {
                        return;
                    }

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

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleResearchCompleted);
        }

        if (BuildResources != null)
        {
            BuildResources.ResourceChanged.AddListener(HandleResourceChanged);
        }

        _initialized = true;
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchCompleted);
        }

        if (BuildResources != null)
        {
            BuildResources.ResourceChanged.RemoveListener(HandleResourceChanged);
        }
    }

    // 보유량이 바뀌면 비용 색과 슬롯 활성 상태가 곧바로 낡는다(생산 정산, 다른 건물 건설 등) -
    // 슬롯을 다시 만들지 않고 표시만 다시 맞춘다. 재생성하면 OnSlotViewChanged가 발화해
    // 튜토리얼 조준이 흔들리고 호버 팝업이 닫힌다.
    private void HandleResourceChanged(ResourceType type, int amount) => RefreshSlotStates();

    // 비용 색(모자란 자원은 빨갛게)과 슬롯 활성 상태(못 지으면 흐리게)는 "지금 지을 수 있는가"라는
    // 같은 판정을 다른 방식으로 보여주는 것이므로 한곳에서 함께 맞춘다 - 따로 갱신하면
    // 색은 빨간데 슬롯은 눌리는 식으로 서로 어긋난다.
    private void RefreshSlotStates()
    {
        foreach (UI_BuildingSlot slot in _spawnedSlots)
        {
            if (slot != null)
            {
                slot.RefreshAffordability();
                slot.SetInteractable(_buildingPlacementController == null ||
                    _buildingPlacementController.CanBuildNow(slot.Prefab));
            }
        }
    }

    // 타워 해금 연구가 끝나면 현재 탭을 다시 그려 새 슬롯이 즉시 나타나게 한다.
    //
    // 완료된 노드가 실제로 타워를 해금할 때만 다시 그린다. 모든 완료 노드에 반응하면
    //  (1) SelectFilter가 OnTabSelected를 발화하는데 TutorialRunner가 이를 실제 탭 클릭으로 보고
    //      단계를 넘겨버리고,
    //  (2) 세이브 복원(ResearchManager.RestoreProgress)이 완료 노드마다 NodeCompleted를 재발화하므로
    //      불러올 때 슬롯 전체가 완료 노드 수만큼 파괴·재생성된다.
    private void HandleResearchCompleted(ResearchNodeData node)
    {
        if (_filterTabs.Length == 0 || node == null || !UnlocksAnyTower(node))
        {
            return;
        }

        SelectFilter(_currentFilterIndex);
    }

    private static bool UnlocksAnyTower(ResearchNodeData node)
    {
        foreach (ResearchEffectSO effect in node.Effects)
        {
            if (effect != null && effect.GetUnlockedTower() != null)
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// 밤에는 건설 모드를 켤 수 없다 - 버튼·단축키 어느 경로로 열려 해도 이 판정을 지나고,
    /// 막을 때는 그 이유를 경고창으로 알린다(UI_ConquestWindow.CanEnterConquestMode와 같은 모양).
    /// CycleManager가 없는 씬(튜토리얼·테스트)에서는 막지 않는다.
    /// </summary>
    public bool CanEnterNow()
    {
        if (_cycleManager == null || _cycleManager.CurrentCycle != CycleManager.CycleState.Night)
        {
            return true;
        }

        if (_warningWindow != null)
        {
            _warningWindow.Show(UI_WarningWindow.MessageId.Build);
        }

        return false;
    }

    // BuildMode 버튼 토글. 열 때는 UIManager를 거쳐 다른 배타 모드(점령 등)를 정리한다.
    public void ToggleFromEntryPoint()
    {
        // 클릭음을 내지 않는다 - 창을 여닫는 제스처는 OpenBuildPanel/CloseBuildPanel의 창음만 낸다.
        EnsureInitialized();

        if (_isOpen)
        {
            // 닫기는 현재 UI 관문이 허용할 때만 받는다.
            if (_uiManager == null || _uiManager.CanCloseExclusive(this))
            {
                CloseBuildPanel();
            }
        }
        else if (_uiManager != null)
        {
            // 밤 금지 판정은 여기서 하지 않는다 - OpenExclusive가 CanEnterNow를 물으므로
            // 단축키로 켜는 경로와 같은 한 곳에서 판정된다(여기서 또 물으면 경고창이 두 번 뜬다).
            _uiManager.OpenExclusive(this);
        }
        else if (CanEnterNow())
        {
            // UIManager가 없는 씬은 물어볼 관문이 없으니 여기서 직접 판정한다.
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
        HideSlotInfo();
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
        // 슬롯이 파괴되면 OnPointerExit가 오지 않는다 - 남아 있던 팝업이 떠 있는 채로 굳지 않도록 먼저 닫는다.
        HideSlotInfo();

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

            if (!IsUnlocked(building))
            {
                continue;
            }

            UI_BuildingSlot slot = Instantiate(slotPrefab, _slotContainer);
            slot.Setup(building, OnSlotSelected, BuildResources, HandleSlotHoverChanged);
            _spawnedSlots.Add(slot);
        }

        // 활성 상태는 낮/밤과 보유 자원 양쪽에 달려 있어 슬롯마다 따로 계산하지 않는다.
        RefreshSlotStates();

        OnSlotViewChanged?.Invoke();
    }

    // Hide research-locked towers until they are discovered.
    // 연구로 해금해야 하는 타워인데 아직 해금되지 않았으면 슬롯 자체를 만들지 않는다.
    // 잠긴 슬롯을 흐리게 보여주지 않는 이유: 역설계 타워는 해당 랜드마크를 점령하기 전까지
    // 존재 자체가 스포일러이므로, 발견하는 재미를 남긴다.
    // 연구 매니저가 없는 씬(튜토리얼·테스트)에서는 전부 노출한다.
    private bool IsUnlocked(Building building)
    {
        if (_researchManager == null || building is not Tower tower)
        {
            return true;
        }

        return _researchManager.IsTowerUnlocked(tower.Data);
    }

    // 슬롯 호버 - 건물 종류에 맞는 정보 창을 그 슬롯 오른쪽에 띄운다.
    // 세 카테고리가 같은 UI_BuildingSlot을 쓰므로 어떤 건물인지는 여기서 가른다
    // (정보 창이 없는 종류는 아무것도 띄우지 않는다).
    private void HandleSlotHoverChanged(UI_BuildingSlot slot, bool isHovered)
    {
        // 두 창이 동시에 뜨지 않도록 항상 먼저 정리한다 - 타워에서 생산건물로 바로 옮겨가는 경우가 있다.
        HideSlotInfo();

        if (!isHovered || slot == null)
        {
            return;
        }

        if (slot.Prefab is Tower tower && tower.Data != null)
        {
            if (_towerInfoPopup != null)
            {
                _towerInfoPopup.Show(tower.Data, (RectTransform)slot.transform);
            }

            return;
        }

        if (slot.Prefab is Factory factory && _factoryInfoPopup != null)
        {
            _factoryInfoPopup.Show(factory, (RectTransform)slot.transform);
        }
    }

    private void HideSlotInfo()
    {
        if (_towerInfoPopup != null)
        {
            _towerInfoPopup.Hide();
        }

        if (_factoryInfoPopup != null)
        {
            _factoryInfoPopup.Hide();
        }
    }

    // 슬롯 클릭 시 해당 건물을 배치 대상으로 선택 (기존 building buttons에서 옮겨온 기능).
    private void OnSlotSelected(Building prefab)
    {
        if (_buildingPlacementController != null && CanSelectBuilding(prefab))
        {
            _buildingPlacementController.SelectBuilding(prefab);
        }
    }

    private bool CanSelectFilter(RectTransform filterTab)
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanSelectFilter(filterTab))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanSelectBuilding(Building prefab)
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanSelectBuilding(prefab))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanMoveSelectedBuilding()
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanMoveSelectedBuilding())
            {
                return false;
            }
        }

        return true;
    }

    private bool CanRemoveSelectedBuilding()
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanRemoveSelectedBuilding())
            {
                return false;
            }
        }

        return true;
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => OpenBuildPanel();
    void IExclusiveMode.Close()
    {
        if (_isOpen)
            CloseBuildPanel();
    }
}
